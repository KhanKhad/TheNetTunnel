using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TNT.SpeedTest.Contracts;

namespace TNT.SpeedTest;

public static class Helper
{
    public static byte[] GenerateArray(int size)
    {
        var rnd = new Random(size);
        var ans = new byte[size];
        rnd.NextBytes(ans);
        return ans;
    }

    public static string GenerateString(int size)
    {
        var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
        
        var data = GenerateArray(size);

        var result = new StringBuilder(size);
        foreach (byte b in data)
        {
            result.Append(chars[b % (chars.Length)]);
        }
        return result.ToString();
    }
    public static  ProtoStruct GenerateProtoStruct(int size)
    {
        var rnd = new Random(size);

        List<ProtoStructItem> items = new List<ProtoStructItem>(size);
        for (int i = 0; i < size; i++)
        {
            var str = new ProtoStructItem
            {
                Byte = (byte)(rnd.Next() % 0xFF),
                Integer = rnd.Next(),
                IntegerArray = new int[4] { rnd.Next(), rnd.Next(), rnd.Next(), rnd.Next() },
                Long = rnd.Next(),
                Text = "piu piu, superfast",
                Time = DateTime.Now
            };
            items.Add(str);
        }
        return new ProtoStruct()
        {
            Members = items.ToArray()
        };
    }
}