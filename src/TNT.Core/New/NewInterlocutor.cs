using System;
using System.Collections.Generic;
using System.Text;
using TNT.Core.New.Tcp;

namespace TNT.Core.New
{
    public class NewInterlocutor
    {
        public TntTcpClient TcpChannel;
        public Responser Responser;
        public NewInterlocutor(TntTcpClient channel)
        {
            Responser = new Responser(channel);
        }

        public void Start()
        {
            Responser.Start();
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
