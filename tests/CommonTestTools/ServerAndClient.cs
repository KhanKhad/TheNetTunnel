using CommonTestTools.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TheNetTunnel.Api;
using TheNetTunnel.Tcp;

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

        public static async Task<ServerAndClient<TProxyContractInterface, TOriginContractInterface, TOriginContractImplementation>> CreateAsync(int port = 12345)            
        {
            var server = TntBuilder
            .UseContract<TOriginContractInterface, TOriginContractImplementation>()
            .CreateTcpServer(IPAddress.Loopback, port);

            server.Start();

            var waitForAClientTask = server.WaitForAClient();

            var clientSide = await TntBuilder
               .UseContract<TProxyContractInterface>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, port);

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
