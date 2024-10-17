using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TNT.Core.Contract;

namespace CommonTestTools.Contracts
{
    public interface ISingleOperationContract
    {
        [TntMessage(1)] void Say();
        [TntMessage(4)] int Ask();
        [TntMessage(11)] Task SayAsync();
        [TntMessage(14)] Task<int> AskAsync();
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
