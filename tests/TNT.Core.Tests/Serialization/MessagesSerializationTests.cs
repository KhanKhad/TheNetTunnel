using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNT.Core.Presentation;
using System.Diagnostics.Contracts;
using TNT.Core.Contract.Proxy;
using TNT.Core.Contract;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Transport;
using TNT.Core.Exceptions.Remote;

namespace TNT.Core.Tests.Serialization
{
    [TestFixture]
    public class MessagesSerializationTests
    {
        private MessagesSerializer _messagesSerializer;
        private MessagesDeserializer _messagesDeserializer;
        private ReceivePduQueue _receiveMessageAssembler;

        [SetUp]
        public void SetUp()
        {
            var methodsDescriptor = new MethodsDescriptor();
            methodsDescriptor.CreateDescription(ProxyContractFactory.ParseContractInterface(typeof(ITestContract)));
            
            _messagesSerializer = new MessagesSerializer(methodsDescriptor);
            _messagesDeserializer = new MessagesDeserializer(methodsDescriptor);
            _receiveMessageAssembler = new ReceivePduQueue();
        }

        [TestCase(0, 0)]
        [TestCase(int.MaxValue, 0)]
        [TestCase(0, short.MaxValue)]
        [TestCase(int.MinValue, 0)]
        [TestCase(0, short.MinValue)]
        public void ComparePingMessages(int askId, short messageId)
        {
            var origin = new NewTntMessage()
            {
                AskId = askId,
                MessageId = 0,
                MessageType = TntMessageType.PingMessage,
                Result = messageId,
            };

            var serialized = _messagesSerializer.SerializeTntMessage(origin);

            _receiveMessageAssembler.Enqueue(serialized.ToArray());

            var msg = _receiveMessageAssembler.DequeueOrNull();

            var deserialized = _messagesDeserializer.Deserialize(msg);

            Assert.That(deserialized.IsSuccessful);
            Assert.That(deserialized.MessageOrNull, Is.Not.Null);

            CompareTntMessages(origin, deserialized.MessageOrNull);
        }

        [TestCase(0, 0)]
        [TestCase(int.MaxValue, 0)]
        [TestCase(0, short.MaxValue)]
        [TestCase(int.MinValue, 0)]
        [TestCase(0, short.MinValue)]
        public void ComparePingResponseMessages(int askId, short messageId)
        {
            var origin = new NewTntMessage()
            {
                AskId = askId,
                MessageId = 0,
                MessageType = TntMessageType.PingResponseMessage,
                Result = messageId,
            };

            var serialized = _messagesSerializer.SerializeTntMessage(origin);

            _receiveMessageAssembler.Enqueue(serialized.ToArray());

            var msg = _receiveMessageAssembler.DequeueOrNull();

            var deserialized = _messagesDeserializer.Deserialize(msg);

            Assert.That(deserialized.IsSuccessful);
            Assert.That(deserialized.MessageOrNull, Is.Not.Null);

            CompareTntMessages(origin, deserialized.MessageOrNull);
        }



        [TestCase(0, 2, new[] { "123" })]
        [TestCase(int.MaxValue, 2, new[] { "123" })]
        [TestCase(int.MinValue, 2, new[] { "123" })]

        public void CompareRequestMessages(int askId, short messageId, object[] args)
        {
            var origin = new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.RequestMessage,
                Result = args,
            };

            var serialized = _messagesSerializer.SerializeTntMessage(origin);

            _receiveMessageAssembler.Enqueue(serialized.ToArray());

            var msg = _receiveMessageAssembler.DequeueOrNull();

            var deserialized = _messagesDeserializer.Deserialize(msg);

            Assert.That(deserialized.IsSuccessful);
            Assert.That(deserialized.MessageOrNull, Is.Not.Null);

            CompareTntMessages(origin, deserialized.MessageOrNull);
        }

        [TestCase(0, 5, "123")]
        [TestCase(int.MaxValue, 5, "123")]
        [TestCase(int.MinValue, 5, "123")]

        public void CompareSuccessfulResponseMessage(int askId, short messageId, object res)
        {
            var origin = new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.SuccessfulResponseMessage,
                Result = res,
            };

            var serialized = _messagesSerializer.SerializeTntMessage(origin);

            _receiveMessageAssembler.Enqueue(serialized.ToArray());

            var msg = _receiveMessageAssembler.DequeueOrNull();

            var deserialized = _messagesDeserializer.Deserialize(msg);

            Assert.That(deserialized.IsSuccessful);
            Assert.That(deserialized.MessageOrNull, Is.Not.Null);

            CompareTntMessages(origin, deserialized.MessageOrNull);
        }


        [TestCase(0, 5, ErrorType.UnhandledUserExceptionError)]
        [TestCase(int.MaxValue, 5, ErrorType.UnhandledUserExceptionError)]
        [TestCase(int.MinValue, 5, ErrorType.UnhandledUserExceptionError)]

        public void CompareFatalFailedResponseMessage(int askId, short messageId, ErrorType errorType)
        {
            var error = new ErrorMessage(messageId, askId, errorType, string.Empty);

            var origin = new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.FatalFailedResponseMessage,
                Result = error,
            };

            var serialized = _messagesSerializer.SerializeTntMessage(origin);

            _receiveMessageAssembler.Enqueue(serialized.ToArray());

            var msg = _receiveMessageAssembler.DequeueOrNull();

            var deserialized = _messagesDeserializer.Deserialize(msg);

            Assert.That(deserialized.IsSuccessful);
            Assert.That(deserialized.MessageOrNull, Is.Not.Null);

            CompareTntMessages(origin, deserialized.MessageOrNull);
        }

        [TestCase(0, 5, ErrorType.UnhandledUserExceptionError)]
        [TestCase(int.MaxValue, 5, ErrorType.UnhandledUserExceptionError)]
        [TestCase(int.MinValue, 5, ErrorType.UnhandledUserExceptionError)]

        public void CompareFailedResponseMessage(int askId, short messageId, ErrorType errorType)
        {
            var error = new ErrorMessage(messageId, askId, errorType, string.Empty);

            var origin = new NewTntMessage()
            {
                AskId = askId,
                MessageId = messageId,
                MessageType = TntMessageType.FailedResponseMessage,
                Result = error,
            };

            var serialized = _messagesSerializer.SerializeTntMessage(origin);

            _receiveMessageAssembler.Enqueue(serialized.ToArray());

            var msg = _receiveMessageAssembler.DequeueOrNull();

            var deserialized = _messagesDeserializer.Deserialize(msg);

            Assert.That(deserialized.IsSuccessful);
            Assert.That(deserialized.MessageOrNull, Is.Not.Null);

            CompareTntMessages(origin, deserialized.MessageOrNull);
        }

        public void CompareTntMessages(NewTntMessage first, NewTntMessage second)
        {
            Assert.That(first.MessageId == second.MessageId);
            Assert.That(first.MessageType == second.MessageType);
            Assert.That(first.AskId == second.AskId);
            Assert.That(first.Result, Is.EqualTo(second.Result));
        }

    }
}
