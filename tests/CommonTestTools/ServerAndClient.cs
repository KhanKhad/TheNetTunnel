using CommonTestTools.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TheNetTunnel.Api;
using TheNetTunnel.Tcp;
using TheNetTunnel.Tls;

namespace CommonTestTools
{
    public class ServerAndClient<TProxyContractInterface, TOriginContractInterface, TOriginContractImplementation> : IDisposable
        where TProxyContractInterface : class
        where TOriginContractInterface : class
        where TOriginContractImplementation : class, TOriginContractInterface, new()
    {

        public TntTcpServer<TOriginContractInterface> TntTcpServer {  get; set; }

        public IConnection<TOriginContractInterface> ServerSideConnection { get; set; }
        public IConnection<TProxyContractInterface> ClientSideConnection { get; set; }

        public static async Task<ServerAndClient<TProxyContractInterface, TOriginContractInterface, TOriginContractImplementation>> CreateAsync(
            int port = 12345, TntServerTlsOptions serverTls = null, TntClientTlsOptions clientTls = null)
        {
            var serverBuilder = TntBuilder
                .UseContract<TOriginContractInterface, TOriginContractImplementation>();

            if (serverTls != null)
                serverBuilder.UseTls(serverTls);

            var server = serverBuilder.CreateTcpServer(IPAddress.Loopback, port);

            server.Start();

            var waitForAClientTask = server.WaitForAClient();

            var clientBuilder = TntBuilder
                .UseContract<TProxyContractInterface>();

            if (clientTls != null)
                clientBuilder.UseTls(clientTls);

            var clientSide = await clientBuilder.CreateTcpClientConnectionAsync(IPAddress.Loopback, port);

            var serverSide = await waitForAClientTask;

            var result = new ServerAndClient<TProxyContractInterface, TOriginContractInterface, TOriginContractImplementation>()
            {
                TntTcpServer = server,
                ClientSideConnection = clientSide,
                ServerSideConnection = serverSide
            };

            return result;
        }

        public void Dispose()
        {
            TntTcpServer.Dispose();
            ClientSideConnection.Dispose();
        }
    }
}
