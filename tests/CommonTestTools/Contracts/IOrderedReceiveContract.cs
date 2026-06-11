using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TheNetTunnel.Contract;

namespace CommonTestTools.Contracts
{
    public interface IOrderedReceiveContract
    {
        [TntMessageAttribute(1)] void Say(int value);
        [TntMessageAttribute(2)] Task SayAsync(int value);
    }

    public class OrderedReceiveContract : IOrderedReceiveContract
    {
        public readonly ConcurrentQueue<int> ReceivedValues = new ConcurrentQueue<int>();

        public void Say(int value)
        {
            //jitter makes reordering of concurrently handled messages very likely
            if (value % 5 == 0)
                Thread.Sleep(1);

            ReceivedValues.Enqueue(value);
        }

        public async Task SayAsync(int value)
        {
            if (value % 5 == 0)
                await Task.Delay(1);

            ReceivedValues.Enqueue(value);
        }
    }
}
