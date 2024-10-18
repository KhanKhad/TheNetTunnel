using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Tnt.LongTests.ContractMocks;

public class LongTestContract<TMessageArg> : ILongTestContract<TMessageArg>
{
    public Action<TMessageArg> OnSay { get; set; }
    public Func<TMessageArg, bool> OnAsk { get; set; }
    public Func<TMessageArg, Task> OnSayAsync { get; set; }
    public Func<TMessageArg, Task<bool>> OnAskAsync { get; set; }

    public ConcurrentBag<TMessageArg> Messages = new(); 

    public int _callsCount;
    public void Say(TMessageArg message)
    {
        Messages.Add(message);
        Interlocked.Increment(ref _callsCount);
    }
    public bool Ask(TMessageArg message)
    {
        Messages.Add(message);
        Interlocked.Increment(ref _callsCount);
        return true;
    }

    public async Task SayAsync(TMessageArg message)
    {
        await Task.Yield();
        Messages.Add(message);
        Interlocked.Increment(ref _callsCount);
    }
    public async Task<bool> AskAsync(TMessageArg message)
    {
        await Task.Yield();
        Messages.Add(message);
        Interlocked.Increment(ref _callsCount);
        return true;
    }    
}