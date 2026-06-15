using System;
using System.IO;
using CommonTestTools.Contracts;
using NUnit.Framework;
using TheNetTunnel;
using TheNetTunnel.Contract.Proxy;
using TheNetTunnel.Exceptions.Remote;
using TheNetTunnel.Presentation;
using TheNetTunnel.Transport;

namespace TheNetTunnel.Tests.Serialization
{
    /// <summary>
    /// Edge cases of the wire protocol: malformed, truncated and unknown messages,
    /// plus roundtrips not covered by <see cref="MessagesSerializationTests"/>.
    /// </summary>
    [TestFixture]
    public class MessagesDeserializationEdgeTests
    {
        private MessagesSerializer _serializer;
        private MessagesDeserializer _deserializer;
        private ReceivePduQueue _assembler;

        [SetUp]
        public void SetUp()
        {
            var descriptor = new MethodsDescriptor();
            descriptor.CreateDescription(ProxyContractFactory.ParseContractInterface(typeof(ITestContract)));

            _serializer = new MessagesSerializer(descriptor);
            _deserializer = new MessagesDeserializer(descriptor);
            _assembler = new ReceivePduQueue();
        }

        private MessageDeserializeResult Roundtrip(TntMessage message)
        {
            using var serialized = _serializer.SerializeTntMessage(message);
            _assembler.Enqueue(serialized.ToArray());

            using var wire = _assembler.DequeueOrNull();
            Assert.That(wire, Is.Not.Null, "Frame was not assembled");

            return _deserializer.Deserialize(wire);
        }

        [Test]
        public void HelloRequest_Roundtrip_PreservesVersions()
        {
            var origin = new HelloMessageRequest
            {
                MyVersion = new Version(1, 2, 3),
                MinimalVersion = new Version(1, 0, 0),
            };

            var result = Roundtrip(new TntMessage
            {
                AskId = 17,
                MessageId = 0,
                MessageType = MessageType.HelloMessageRequest,
                Result = origin,
            });

            Assert.That(result.IsSuccessful, Is.True);
            var request = (HelloMessageRequest)result.MessageOrNull.Result;
            Assert.That(request.MyVersion, Is.EqualTo(origin.MyVersion));
            Assert.That(request.MinimalVersion, Is.EqualTo(origin.MinimalVersion));
            Assert.That(result.MessageOrNull.AskId, Is.EqualTo(17));
        }

        [Test]
        public void HelloResponse_Roundtrip_PreservesRejectionReason()
        {
            var origin = HelloMessageResponse.ConnectionsLimit();

            var result = Roundtrip(new TntMessage
            {
                AskId = 3,
                MessageId = 0,
                MessageType = MessageType.HelloMessageResponse,
                Result = origin,
            });

            Assert.That(result.IsSuccessful, Is.True);
            var response = (HelloMessageResponse)result.MessageOrNull.Result;
            Assert.That(response.AvailableForWork, Is.False);
            Assert.That(response.UnavailabilityReason, Is.EqualTo(origin.UnavailabilityReason));
        }

        [Test]
        public void MultiArgumentRequest_Roundtrip()
        {
            // ITestContract id 3: Say(string s, int i, long l)
            var args = new object[] { "проверка", 5, long.MaxValue };

            var result = Roundtrip(new TntMessage
            {
                AskId = 1,
                MessageId = 3,
                MessageType = MessageType.RequestMessage,
                Result = args,
            });

            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.MessageOrNull.Result, Is.EqualTo(args));
        }

        [Test]
        public void IntResponse_Roundtrip()
        {
            // ITestContract id 4: int Ask()
            var result = Roundtrip(new TntMessage
            {
                AskId = 1,
                MessageId = 4,
                MessageType = MessageType.SuccessfulResponseMessage,
                Result = 42,
            });

            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.MessageOrNull.Result, Is.EqualTo(42));
        }

        [Test]
        public void RequestWithUnknownContractId_ReturnsContractError_WithoutDisconnect()
        {
            using var wire = new MemoryStream();
            wire.WriteByte(InterlocutorProperties.DefaultContractId); // contractId
            wire.WriteShort(999);                              // unknown contract id
            wire.WriteShort((short)MessageType.RequestMessage);
            wire.WriteInt(7);                                  // askId
            wire.Position = 0;

            var result = _deserializer.Deserialize(wire);

            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorMessageOrNull.ErrorType, Is.EqualTo(ErrorType.ContractSignatureError));
            Assert.That(result.ErrorMessageOrNull.AskId, Is.EqualTo(7));
            Assert.That(result.NeedToDisconnect, Is.False,
                "An unknown contract id is the peer's bug, not protocol corruption");
        }

        [Test]
        public void SerializedRequestWithUnknownContractId_IsRejectedOnDeserialization()
        {
            // The serializer itself replaces an unknown request with an error payload.
            var result = Roundtrip(new TntMessage
            {
                AskId = 7,
                MessageId = 999,
                MessageType = MessageType.RequestMessage,
                Result = new object[0],
            });

            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorMessageOrNull.ErrorType, Is.EqualTo(ErrorType.ContractSignatureError));
        }

        [Test]
        public void UnknownMessageType_RequestsDisconnect()
        {
            using var wire = new MemoryStream();

            wire.WriteByte(InterlocutorProperties.DefaultContractId); // contractId
            wire.WriteShort(1);
            wire.WriteShort(12345);                            // unknown message type
            wire.WriteInt(7);
            wire.Position = 0;

            var result = _deserializer.Deserialize(wire);

            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.NeedToDisconnect, Is.True);
            Assert.That(result.ErrorMessageOrNull.ErrorType, Is.EqualTo(ErrorType.SerializationError));
        }

        [Test]
        public void EmptyMessage_RequestsDisconnect()
        {
            using var wire = new MemoryStream();

            var result = _deserializer.Deserialize(wire);

            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.NeedToDisconnect, Is.True);
        }

        [Test]
        public void TruncatedHeader_RequestsDisconnect()
        {
            using var wire = new MemoryStream();
            wire.WriteShort(1);                                // messageId only, no type/askId
            wire.Position = 0;

            var result = _deserializer.Deserialize(wire);

            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.NeedToDisconnect, Is.True);
        }

        [Test]
        public void CorruptedRequestPayload_RequestsDisconnect()
        {
            using var wire = new MemoryStream();

            wire.WriteByte(InterlocutorProperties.DefaultContractId); // contractId
            wire.WriteShort(3);                                // Say(string, int, long)
            wire.WriteShort((short)MessageType.RequestMessage);
            wire.WriteInt(7);
            wire.WriteByte(0xFF);                              // garbage instead of arguments
            wire.Position = 0;

            var result = _deserializer.Deserialize(wire);

            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.NeedToDisconnect, Is.True);
            Assert.That(result.ErrorMessageOrNull.ErrorType, Is.EqualTo(ErrorType.SerializationError));
        }
    }
}
