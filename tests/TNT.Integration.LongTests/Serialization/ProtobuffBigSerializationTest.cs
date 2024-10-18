using System.IO;
using System.Threading.Tasks;
using CommonTestTools;
using NUnit.Framework;
using Tnt.LongTests.ContractMocks;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.Serializers;
using TNT.Integration.LongTests;

namespace Tnt.LongTests.Serialization;

[TestFixture]
public class ProtobuffBigSerializationTest
{
    [Test]
    public void PacketOf500Kb_Serialization_deserializesSame()
    {
        SerializeAndDeserializeProtobuffMessage(1000);
    }

    [Test]
    public void PacketOf2mb_Serialization_deserializesSame()
    {
        SerializeAndDeserializeProtobuffMessage(2000);
    }

    [Test]
    public void PacketOf10mb_Serialization_deserializesSame()
    {
        SerializeAndDeserializeProtobuffMessage(5000);
    }
    
    [Test]
    public void PacketOf50mb_Serialization_deserializesSame()
    {
        SerializeAndDeserializeProtobuffMessage(10000);
    }

    [Test]
    public async Task PacketOf500Kb_transmitsViaTcp()
    {
        await CheckProtobuffEchoTransaction(1000);
    }
    [Test]
    public async Task PacketOf2mb_transmitsViaTcp()
    {
        await CheckProtobuffEchoTransaction(2000);
    }
    [Test]
    public async Task PacketOf10mb_transmitsViaTcp()
    {
        await CheckProtobuffEchoTransaction(5000);
    }
    [Test]
    public async Task PacketOf50mb_transmitsViaTcp()
    {
        await CheckProtobuffEchoTransaction(10000);
    }

    private static void SerializeAndDeserializeProtobuffMessage(int companySize)
    {
        var company = IntegrationTestsHelper.CreateCompany(companySize);
        using var stream = new MemoryStream();
        var serializer = new ProtoSerializer<Company>();
        serializer.SerializeT(company, stream);
        stream.Position = 0;
        var deserializer = new ProtoDeserializer<Company>();
        var deserialized = deserializer.DeserializeT(stream, (int)stream.Length);
        company.AssertIsSameTo(deserialized);
    }

    private static async Task CheckProtobuffEchoTransaction(int itemsSize)
    {
        using var serverAndClient = await ServerAndClient<ILongTestContract<Company>, ILongTestContract<Company>, LongTestContract<Company>>.Create();

        EventAwaiter<Company> callAwaiter = new EventAwaiter<Company>();

        var bag = ((LongTestContract<Company>)serverAndClient.ServerSideConnection.Contract).Messages;

        var company = IntegrationTestsHelper.CreateCompany(itemsSize);

        serverAndClient.ClientSideConnection.Contract.Ask(company);

        Assert.That(bag.TryTake(out var message), Is.Not.Null);

        message.AssertIsSameTo(company);
    }
}