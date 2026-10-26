using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TheNetTunnel.Diagnostics;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Presentation;
using TheNetTunnel.Tcp;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Tls
{
    /// <summary>
    /// TCP channel whose traffic is encrypted with TLS via <see cref="SslStream"/>.
    /// The handshake happens in <see cref="Start"/>/<see cref="StartAsync"/>, so the
    /// TNT hello exchange and everything after it already travels encrypted.
    /// </summary>
    public class TntTlsChannel : IChannel
    {
        private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

        private readonly TcpClient Client;
        private readonly IPEndPoint IPEndPoint;

        private readonly X509Certificate2 _serverCertificate;
        private readonly HashSet<string> _expectedServerThumbprints;
        private readonly string _targetHost;

        private SslStream _sslStream;

        // Thumbprint of the certificate the server presented, captured in the
        // validation callback so it survives a failed handshake.
        private string _presentedServerThumbprint;

        private int _bytesReceived;
        private int _bytesSent;

        public Channel<TcpData> ResponsesChannel { get; }
        public string RemoteEndpointName { get; private set; }
        public string LocalEndpointName { get; private set; }

        /// <summary>
        /// Certificate presented by the remote side. Set after the handshake;
        /// null on the server side (clients do not present certificates).
        /// </summary>
        public X509Certificate2 RemoteCertificate { get; private set; }

        /// <summary>
        /// SHA-256 thumbprint of <see cref="RemoteCertificate"/> (see <see cref="TntThumbprint"/>);
        /// null until the handshake completes or when the remote side presented no certificate.
        /// </summary>
        public string RemoteThumbprint => RemoteCertificate == null ? null : TntThumbprint.Of(RemoteCertificate);

        public event Action<object, ErrorMessage> OnDisconnect;

        public bool IsConnected => Client.Connected && (_sslStream?.IsAuthenticated ?? false);

        public int BytesReceived => Volatile.Read(ref _bytesReceived);

        public int BytesSent => Volatile.Read(ref _bytesSent);

        private Task _internalReadAsync;
        private CancellationTokenSource _internalReadCts;

        /// <summary>
        /// Client side: connects to <paramref name="endPoint"/> and authenticates the server.
        /// </summary>
        public TntTlsChannel(IPEndPoint endPoint, TntClientTlsOptions options) : this()
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            Client = new TcpClient();
            IPEndPoint = endPoint;
            _expectedServerThumbprints = NormalizeThumbprints(options.ExpectedServerThumbprints);
            _targetHost = options.TargetHost ?? endPoint.Address.ToString();
        }

        /// <summary>
        /// Server side: wraps an accepted <paramref name="client"/> and presents the certificate.
        /// </summary>
        public TntTlsChannel(TcpClient client, TntServerTlsOptions options) : this()
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            Client = client;
            _serverCertificate = options.Certificate;
        }

        private TntTlsChannel()
        {
            ResponsesChannel = Channel.CreateUnbounded<TcpData>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true,
            });
        }

        private volatile bool _alreadyStarted;
        public void Start()
        {
            StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task StartAsync()
        {
            if (_alreadyStarted)
                return;
            _alreadyStarted = true;

            if (!Client.Connected)
                await Client.ConnectAsync(IPEndPoint.Address, IPEndPoint.Port).ConfigureAwait(false);

            SetEndPoints();
            Client.NoDelay = true;

            try
            {
                await AuthenticateAsync().ConfigureAwait(false);
            }
            catch
            {
                Client.Dispose();
                throw;
            }

            _internalReadCts = new CancellationTokenSource();
            _internalReadAsync = Task.Run(async () => await InternalReadAsync(_internalReadCts.Token));
        }

        private async Task AuthenticateAsync()
        {
            using var handshakeCts = new CancellationTokenSource(HandshakeTimeout);

            try
            {
                if (_serverCertificate != null)
                {
                    _sslStream = new SslStream(Client.GetStream(), false);
                    await _sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions()
                    {
                        ServerCertificate = _serverCertificate,
                    }, handshakeCts.Token).ConfigureAwait(false);
                }
                else
                {
                    _sslStream = new SslStream(Client.GetStream(), false, ValidateServerCertificate);
                    await _sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions()
                    {
                        TargetHost = _targetHost,
                    }, handshakeCts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                throw new SslAuthenticateException(
                    $"TLS handshake with {RemoteEndpointName} failed: {e.Message}",
                    _presentedServerThumbprint, e);
            }

            if (_sslStream.RemoteCertificate != null)
                RemoteCertificate = new X509Certificate2(_sslStream.RemoteCertificate);
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            _presentedServerThumbprint = certificate == null ? null : TntThumbprint.Of(certificate);

            // No pins: accept whatever the server presents, ignoring sslPolicyErrors
            // (self-signed certificates and name mismatches included).
            if (_expectedServerThumbprints == null || _expectedServerThumbprints.Count == 0)
                return true;

            if (_presentedServerThumbprint == null)
                return false;

            return _expectedServerThumbprints.Contains(_presentedServerThumbprint);
        }

        private static HashSet<string> NormalizeThumbprints(string[] thumbprints)
        {
            if (thumbprints == null)
                return null;

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var thumbprint in thumbprints)
            {
                var normalized = TntThumbprint.Normalize(thumbprint);
                if (normalized != null)
                    set.Add(normalized);
            }

            return set;
        }

        // SslStream decrypts into its own buffer, so socket.Available says nothing
        // about how many plaintext bytes are pending; a fixed chunk is used instead.
        private const int ReceiveChunkSize = 16 * 1024;

        private async Task InternalReadAsync(CancellationToken token)
        {
            string disconnectReason = null;

            while (!token.IsCancellationRequested && Client.Connected)
            {
                byte[] buffer = null;
                var handedOff = false;
                try
                {
                    buffer = ArrayPool<byte>.Shared.Rent(ReceiveChunkSize);

                    var bytesToRead = await _sslStream.ReadAsync(buffer, token).ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                        break;

                    if (bytesToRead == 0)
                    {
                        disconnectReason = "Remote endpoint closed the connection";
                        break;
                    }

                    Interlocked.Add(ref _bytesReceived, bytesToRead);

                    var data = new TcpData()
                    {
                        Bytes = buffer,
                        Length = bytesToRead
                    };

                    await ResponsesChannel.Writer.WriteAsync(data, token).ConfigureAwait(false);
                    handedOff = true;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    TntLog.Warning(nameof(TntTlsChannel), "Error while receiving data from tls stream", e);
                    disconnectReason = $"Connection lost while receiving data: {e.Message}";
                    break;
                }
                finally
                {
                    if (buffer != null && !handedOff)
                        ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            ResponsesChannel.Writer.TryComplete(new Exception(disconnectReason));
        }

        // Writes are not synchronized here on purpose: the only writer is the
        // Interlocutor send loop, which drains its channel sequentially.
        public async Task WriteAsync(ReadOnlyMemory<byte> data)
        {
            if (!IsConnected)
            {
                if (!_alreadyStarted)
                    throw new ConnectionIsNotEstablishedYet("tls channel is not connected yet");

                throw new ConnectionIsLostException("tls channel is not connected");
            }

            try
            {
                await _sslStream.WriteAsync(data).ConfigureAwait(false);

                Interlocked.Add(ref _bytesSent, data.Length);
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new ConnectionIsLostException("Failed to send data: tls channel is lost", innerException: ex);
            }
        }

        private void SetEndPoints()
        {
            if (!Client.Connected) return;

            RemoteEndpointName = EndPointToText(Client.Client.RemoteEndPoint);
            LocalEndpointName = EndPointToText(Client.Client.LocalEndPoint);
        }

        private static string EndPointToText(EndPoint endPoint)
        {
            if (endPoint is IPEndPoint ip)
            {
                return ip.AddressFamily == AddressFamily.InterNetworkV6
                    ? $"[{ip.Address}]:{ip.Port}"
                    : $"{ip.Address}:{ip.Port}";
            }

            return endPoint?.ToString() ?? string.Empty;
        }

        private int _disconnected;

        public void DisconnectBecauseOf(ErrorMessage exceptionMessage)
        {
            if (Interlocked.CompareExchange(ref _disconnected, 1, 0) != 0)
                return;

            _internalReadCts?.Cancel();

            if (_internalReadAsync != null)
            {
                try { _internalReadAsync.Wait(); }
                catch { }
            }

            _internalReadCts?.Dispose();

            ResponsesChannel.Writer.TryComplete();
            // Disposing the SslStream also closes the underlying NetworkStream.
            _sslStream?.Dispose();
            Client.Dispose();

            OnDisconnect?.Invoke(this, exceptionMessage);
        }

        public void Disconnect()
        {
            DisconnectBecauseOf(null);
        }

        public void Dispose()
        {
            Disconnect();
            OnDisconnect = null;
        }
    }
}
