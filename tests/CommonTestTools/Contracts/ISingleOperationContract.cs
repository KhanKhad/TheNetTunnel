using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TheNetTunnel.Contract;

namespace CommonTestTools.Contracts
{
    public interface ISingleOperationContract
    {
        [TntMessageAttribute(1)] void Say();
        [TntMessageAttribute(4)] int Ask();
        [TntMessageAttribute(11)] Task SayAsync();
        [TntMessageAttribute(14)] Task<int> AskAsync();
    }

    public class SingleOperationContract : ISingleOperationContract
    {
        public int _callsCount;

        public int Ask()
        {
            Thread.Sleep(1000);
            Interlocked.Increment(ref _callsCount);
            return 0;
        }

        public async Task<int> AskAsync()
        {
            await Task.Delay(1000);
            Interlocked.Increment(ref _callsCount);
            return 0;
        }

        public void Say()
        {
            Thread.Sleep(1000);
            Interlocked.Increment(ref _callsCount);
        }

        public async Task SayAsync()
        {
            await Task.Delay(1000);
            Interlocked.Increment(ref _callsCount);
        }
    }
}
