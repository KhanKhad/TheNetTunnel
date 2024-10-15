using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TNT.Core.Api;

namespace TNT.Core.Tcp
{
    public static class TcpHelper
    {
        public static TntTcpServer<TContract> CreateTcpServer<TContract>(
            this ContractBuilder<TContract> builder, IPAddress ip, int port)
            where TContract : class
        {
            return new TntTcpServer<TContract>(builder, new IPEndPoint(ip, port));
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
            return builder.UseChannelFactory(() => {

                var channel = new TntTcpClient(endPoint);
                return channel;

            }).Build();
        }

        public static Task<IConnection<TContract>> CreateTcpClientConnectionAsync<TContract>(
             this ContractBuilder<TContract> builder, IPEndPoint endPoint)
             where TContract : class

        {
            return builder.UseChannelFactory(() => {

                var channel = new TntTcpClient(endPoint);
                return channel;

            }).BuildAsync();
        }
    }
}
