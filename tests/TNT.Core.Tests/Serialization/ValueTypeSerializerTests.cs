using System.IO;
using NUnit.Framework;
using TheNetTunnel.Presentation.Deserializers;
using TheNetTunnel.Presentation.Serializers;

namespace TheNetTunnel.Tests.Serialization;

[TestFixture]
public class PrimitiveSerializerTests
{
    [TestCase(3452341.12)]
    [TestCase(12)]
    [TestCase(0)]
    [TestCase(0.001)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void Double_SerializeAndBack_ValuesAreEquel(double value)
    {
        if(!double.IsNaN(value))
            Assert.That(value == SerializeAndBack(value));
        else Assert.That(double.IsNaN(value));
    }



    [TestCase(true)]
    [TestCase(false)]
    public void Bool_SerializeAndBack_ValuesAreEquel(bool value)
    {
        Assert.That(value == SerializeAndBack(value));
    }


    private static T SerializeAndBack<T>(T value) where T : struct
    {
        using var result = new MemoryStream();
        var primitiveSerializator = new ValueTypeSerializer<T>();
        primitiveSerializator.SerializeT(value, result);

        result.Position = 0;

        return new ValueTypeDeserializer<T>().DeserializeT(result, (int)primitiveSerializator.Size);
    }

}
