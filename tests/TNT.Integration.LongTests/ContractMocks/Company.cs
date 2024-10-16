using NUnit.Framework;
using ProtoBuf;

namespace Tnt.LongTests.ContractMocks;

[ProtoContract]
public class Company
{
    [ProtoMember(3)]
    public string Name;
    [ProtoMember(2)]
    public int Id;
    [ProtoMember(1)]
    public User[] Users;
    public void AssertIsSameTo(Company company)
    {
        Assert.That(company.Name == Name);
        Assert.That(Id == company.Id);
        Assert.That(Users.Length == company.Users.Length);
        for (int i = 0; i < Users.Length; i++)
        {
            Users[i].AssertIsSameTo(company.Users[i]);
        }
    }
}