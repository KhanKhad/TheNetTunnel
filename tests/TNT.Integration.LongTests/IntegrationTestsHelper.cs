using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CommonTestTools.Contracts;
using Moq;
using Tnt.LongTests.ContractMocks;
using TNT;
using TNT.Core.Api;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.Serializers;

namespace TNT.Integration.LongTests;

public static class IntegrationTestsHelper
{
    public static Company CreateCompany(int usersCount)
    {
        var rnd = new Random();
        var users = new List<User>();
        for (var i = 0; i < usersCount; i++)
        {
            var usr = new User
            {
                Age = i,
                Name = "Some user with name of Masha#" + i,
                Payload = new byte[i],
            };
            rnd.NextBytes(usr.Payload);
            users.Add(usr);
        }
        var company = new Company
        {
            Name = "Microzoft",
            Id = 42,
            Users = users.ToArray()
        };
        return company;
    }

    public static byte[] CreateArray(int length, byte value)
    {
        var array = new byte[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = value;
        }

        return array;
    }
}