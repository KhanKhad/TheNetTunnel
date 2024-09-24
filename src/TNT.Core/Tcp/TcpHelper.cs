using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TNT.Core.Api;

namespace TNT.Core.Tcp
{
    public static class TcpHelper
    {
        public static TcpChannelServer<TContract> CreateTcpServer<TContract>(
            this PresentationBuilder<TContract> builder, IPAddress ip, int port)
            where TContract : class
        {
            return new TcpChannelServer<TContract>(builder, new IPEndPoint(ip, port));
        }

        public static IConnection<TContract, TcpChannel> CreateTcpClientConnection<TContract>(
            this PresentationBuilder<TContract> builder, IPEndPoint endPoint)
            where TContract : class

        {
            return builder.UseChannel(() =>
            {
                var channel = new TcpChannel();
                channel.Connect(endPoint);
                return channel;
            }).Build();
        }
       

        /// <summary>
        /// Connect to remote tcp point
        /// </summary>
        /// <exception cref="SocketException">Connection failed</exception>
        public static IConnection<TContract, TcpChannel> CreateTcpClientConnection<TContract>(
            this PresentationBuilder<TContract> builder, IPAddress ip, int port)
            where TContract : class
        {
            return CreateTcpClientConnection(builder, new IPEndPoint(ip, port));
        }
        public static Task<IConnection<TContract, TcpChannel>> CreateTcpClientConnectionAsync<TContract>(
            this PresentationBuilder<TContract> builder, IPEndPoint endPoint)
            where TContract : class
        {
            return builder.UseChannelAsync(async () =>
            {
                var channel = new TcpChannel();
                await channel.ConnectAsync(endPoint);
                return channel;
            }).BuildAsync();
        }

        /// <summary>
        /// Connect to remote tcp point
        /// </summary>
        /// <exception cref="SocketException">Connection failed</exception>
        public static Task<IConnection<TContract, TcpChannel>> CreateTcpClientConnectionAsync<TContract>(
            this PresentationBuilder<TContract> builder, IPAddress ip, int port)
            where TContract : class
        {
            return CreateTcpClientConnectionAsync(builder, new IPEndPoint(ip, port));
        }
    }
}
