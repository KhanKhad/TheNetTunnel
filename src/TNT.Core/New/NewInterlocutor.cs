using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using TNT.Core.Exceptions.Local;
using TNT.Core.New.Tcp;

namespace TNT.Core.New
{
    public class NewInterlocutor
    {
        public TntTcpClient TcpChannel;
        public Responser Responser;

        private MessagesSerializer _messagesSerializer;

        private volatile short _maxAskId = 0;
        private readonly int _maxAnsDelay;

        public NewInterlocutor(NewReflectionHelper reflectionHelper, TntTcpClient channel, int maxAnsDelay = 3000)
        {
            _maxAnsDelay = maxAnsDelay;

            _messagesSerializer = new MessagesSerializer(reflectionHelper);
            Responser = new Responser(reflectionHelper, _messagesSerializer, channel);
        }

        public void Start()
        {
            Responser.Start();
        }

        public void Say(int messageId, object[] values)
        {
            var message = _messagesSerializer.SerializeSayMessage((short)messageId, values);
            TcpChannel.Write(message.ToArray());
        }

        public T Ask<T>(int messageId, object[] values)
        {
            short newId;

            unchecked
            {
                newId = _maxAskId++;
            }

            var message = _messagesSerializer.SerializeAskMessage((short)messageId, newId, values);
            
            var awaiter = Responser.GetAsyncMessageAwaiter(newId);

            TcpChannel.Write(message.ToArray());

            if (awaiter.Wait(_maxAnsDelay))
                return (T)awaiter.Result;

            else throw new CallTimeoutException((short)messageId, newId);
        }




        public void SetIncomeAskCallHandler<T>(int messageId, Func<object[], T> callback)
        {
            Responser.SetIncomeAskCallHandler(messageId, callback);
        }
        public void SetIncomeSayCallHandler(int messageId, Action<object[]> callback)
        {
            Responser.SetIncomeSayCallHandler(messageId, callback);
        }
    }
}
