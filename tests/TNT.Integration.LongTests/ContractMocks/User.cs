using NUnit.Framework;
using ProtoBuf;

namespace Tnt.LongTests.ContractMocks;

[ProtoContract]
public class User
{
    [ProtoMember(1)]
    public string Name;
    [ProtoMember(2)]
    public int Age;
    [ProtoMember(3)]
    public byte[] Payload;

    public void AssertIsSameTo(User user)
    {
        Assert.That(user.Name == Name);
        Assert.That(user.Age == Age);
        Assert.That(user.Payload.Length == Payload.Length);

        for (int i = 0; i < Payload.Length; i++)
        {
            Assert.That(user.Payload[i] == Payload[i]);
        }
    }
}