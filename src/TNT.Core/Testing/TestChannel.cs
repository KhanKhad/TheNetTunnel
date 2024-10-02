using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;
using TNT.Core.New.Tcp;
using TNT.Core.Presentation;
using TNT.Core.Transport;

namespace TNT.Core.Testing
{
    public class TestChannel: IChannel
    {
        private readonly bool _threadQueue;
        private bool _wasConnected;
        private bool _allowReceive;
        private readonly ConcurrentQueue<byte[]> _receiveQueue = new ConcurrentQueue<byte[]>();
        private  int _bytesReceived;
        private int _bytesSent;
        private readonly AutoResetEvent _newDataReveived = new AutoResetEvent(false);
        private Thread _receiveThreadOrNull;
        /// <summary>
        /// Потокобезопасная имитация сетевого взаимодействия.
        /// </summary>
        public static TestChannel CreateThreadSafe()=> new TestChannel(threadQueue: true);
        /// <summary>
        /// Не потокобезопасен. Можно использовать только в однопоточной среде
        /// </summary>
        public static TestChannel CreateSingleThread() => new TestChannel(threadQueue: false);

        private TestChannel(bool threadQueue = true)
        {
            _threadQueue = threadQueue;
        }

        public void ImmitateReceive(byte[] message)
        {
            _receiveQueue.Enqueue(message);
            if(_threadQueue)
            {
                _newDataReveived.Set();
            }
            else
            {
                HandleReceiveQueue();
            }
        }

        private void ThreadVoid()
        {
            while (IsConnected)
            {
                _newDataReveived.WaitOne(100);
                while(!_receiveQueue.IsEmpty)
                    HandleReceiveQueue();
            }
        }

        private void HandleReceiveQueue()
        {
            _receiveQueue.TryDequeue(out var msg);
            if(msg==null)
                return;
            if(!IsConnected)
                return;
            _bytesReceived += msg.Length;
            OnReceive?.Invoke(this, msg);
        }
        public void ImmitateConnect()
        {
            if(IsConnected)
                throw  new InvalidOperationException("Cannot to immitate connect while IsConnected = true");
            _wasConnected = true;
            IsConnected = true;
        }

        public void ImmitateDisconnect()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Cannot to immitate disconnect while IsConnected = false");
            IsConnected = false;
            OnDisconnect?.Invoke(this, null);
        }

        public event Action<object, byte[]> OnWrited;

        public bool IsConnected { get; private set; }

        public bool AllowReceive

        {
            get => _allowReceive;
            set
            {
                if(_allowReceive==value)
                    return;
                
                _allowReceive = value;

                if (_allowReceive)
                {
                    if (!_threadQueue)
                        HandleReceiveQueue();
                    else
                    {
                        this._receiveThreadOrNull = new Thread((s) => ThreadVoid());
                        _receiveThreadOrNull.Start();
                    }
                }
                AllowReceiveChanged?.Invoke(this,value);
            }
        }

        public event Action<IChannel, bool> AllowReceiveChanged; 
        public event Action<object, byte[]> OnReceive;
        public event Action<object, ErrorMessage> OnDisconnect;
        public void Disconnect()
        {
          DisconnectBecauseOf(null);
        }
        public void DisconnectBecauseOf(ErrorMessage error)
        {
            if (IsConnected)
            {
                IsConnected = false;
                OnDisconnect?.Invoke(this, error);
            }
        }
        
        public void Write(byte[] array, int offset, int length)
        {
            if (!_wasConnected)
                throw new ConnectionIsNotEstablishedYet();
            if (!IsConnected)
                throw new ConnectionIsLostException();

            Interlocked.Add(ref _bytesSent, length);

            var buf = new byte[length];
            Buffer.BlockCopy(array, offset, buf, 0, length);

            OnWrited?.Invoke(this, buf);
        }

        public int BytesReceived => _bytesReceived;

        public int BytesSent => _bytesSent;

        public string RemoteEndpointName { get; }
        public string LocalEndpointName { get; }

        public Channel<TcpData> ResponsesChannel => throw new NotImplementedException();

        public Task WriteAsync(byte[] data, int offset, int length)
        {
            return Task.Run(() => Write(data, offset, length));
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        public Task WriteAsync(byte[] data)
        {
            throw new NotImplementedException();
        }

        public void Write(byte[] array)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
