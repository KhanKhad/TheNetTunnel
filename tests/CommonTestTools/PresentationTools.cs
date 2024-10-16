﻿using CommonTestTools.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TNT.Core.Api;
using TNT.Core.Tcp;

namespace CommonTestTools
{
    public static class PresentationTools
    {
        public static async Task CreateConnectedServerAndClient<TContract, TImplementation>(IConnection<TContract> serverSide,
            IConnection<TContract> clientSide) 

            where TContract : class
            where TImplementation : TContract, new()
        {
            var server = TntBuilder
            .UseContract<TContract, TImplementation>()
            .CreateTcpServer(IPAddress.Loopback, 12345);

            server.Start();

            clientSide = await TntBuilder
               .UseContract<TContract>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);


            serverSide = await server.WaitForAClient();
        }
    }


    public class ServerAndClient<TProxyContractInterface, TOriginContractInterface, TOriginContractImplementation> : IDisposable
        where TProxyContractInterface : class
        where TOriginContractInterface : class
        where TOriginContractImplementation : class, TOriginContractInterface, new()
    {

        public TntTcpServer<TOriginContractInterface> TntTcpServer {  get; set; }

        public IConnection<TOriginContractInterface> ServerSideConnection { get; set; }
        public IConnection<TProxyContractInterface> ClientSideConnection { get; set; }

        public static async Task<ServerAndClient<TOriginContractInterface, TOriginContractInterface, TOriginContractImplementation>> Create()            
        {
            var server = TntBuilder
            .UseContract<TOriginContractInterface, TOriginContractImplementation>()
            .CreateTcpServer(IPAddress.Loopback, 12345);

            server.Start();

            var clientSide = await TntBuilder
               .UseContract<TOriginContractInterface>()
               .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

            var serverSide = await server.WaitForAClient();

            var result = new ServerAndClient<TOriginContractInterface, TOriginContractInterface, TOriginContractImplementation>()
            {
                TntTcpServer = server,
                ClientSideConnection = clientSide,
                ServerSideConnection = serverSide
            };

            return result;
        }

        //public static async Task<ServerAndClient<TProxyContractInterface, TOriginContractInterface, TOriginContractImplementation>> Create()
        //{
        //    var server = TntBuilder
        //    .UseContract<TContract, TImplementation>()
        //    .CreateTcpServer(IPAddress.Loopback, 12345);

        //    server.Start();

        //    var clientSide = await TntBuilder
        //       .UseContract<TContract>()
        //       .CreateTcpClientConnectionAsync(IPAddress.Loopback, 12345);

        //    var serverSide = await server.WaitForAClient();

        //    var result = new ServerAndClient<TContract, TImplementation>()
        //    {
        //        TntTcpServer = server,
        //        ClientSideConnection = clientSide,
        //        ServerSideConnection = serverSide
        //    };

        //    return result;
        //}


        public void Dispose()
        {
            TntTcpServer.Dispose();
            ClientSideConnection.Dispose();
        }
    }
}
