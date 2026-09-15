using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using TheNetTunnel.Api;
using TheNetTunnel.Contract;
using TheNetTunnel.Tls;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Tcp
{
    public static class TcpHelper
    {
        public static TntTcpServer<TContract> CreateTcpServer<TContract>(
            this ContractBuilder<TContract> builder, IPAddress ip, int port, int maxConnections = -1)
            where TContract : class
        {
            return new TntTcpServer<TContract>(builder, new IPEndPoint(ip, port), maxConnections);
        }

        /// <summary>
        /// Connect to remote tcp point
        /// </summary>
        /// <exception cref="SocketException">Connection failed</exception>
        public static IConnection<TContract> CreateTcpClientConnection<TContract>(
            this ContractBuilder<TContract> builder, IPAddress ip, int port)
            where TContract : class
        {
            return CreateTcpClientConnection(builder, new IPEndPoint(ip, port));
        }

        public static Task<IConnection<TContract>> CreateTcpClientConnectionAsync<TContract>(
            this ContractBuilder<TContract> builder, IPAddress ip, int port)
            where TContract : class
        {
            return CreateTcpClientConnectionAsync(builder, new IPEndPoint(ip, port));
        }



        public static IConnection<TContract> CreateTcpClientConnection<TContract>(
            this ContractBuilder<TContract> builder, IPEndPoint endPoint)
            where TContract : class

        {
            return builder.UseChannelFactory(() => CreateClientChannel(builder, endPoint)).Build();
        }

        public static Task<IConnection<TContract>> CreateTcpClientConnectionAsync<TContract>(
             this ContractBuilder<TContract> builder, IPEndPoint endPoint)
             where TContract : class

        {
            return builder.UseChannelFactory(() => CreateClientChannel(builder, endPoint)).BuildAsync();
        }

        private static IChannel CreateClientChannel<TContract>(ContractBuilder<TContract> builder, IPEndPoint endPoint)
            where TContract : class
        {
            if (builder.ClientTlsOptions != null)
                return new TntTlsChannel(endPoint, builder.ClientTlsOptions);

            return new TntTcpClient(endPoint);
        }
    }
}
