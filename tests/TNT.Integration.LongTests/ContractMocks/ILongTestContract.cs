using System;
using System.Threading.Tasks;
using TNT;
using TNT.Core.Contract;

namespace Tnt.LongTests.ContractMocks;

public interface ILongTestContract<TMessageArg>
{
    [TntMessageAttribute(1)] void Say(TMessageArg s);
    [TntMessageAttribute(2)] bool Ask(TMessageArg message);

    [TntMessageAttribute(3)] Task SayAsync(TMessageArg s);
    [TntMessageAttribute(4)] Task<bool> AskAsync(TMessageArg s);


    [TntMessageAttribute(101)] Action<TMessageArg> OnSay { get; set; }
    [TntMessageAttribute(102)] Func<TMessageArg, bool> OnAsk { get; set; }

    [TntMessageAttribute(103)] Func<TMessageArg, Task> OnSayAsync { get; set; }
    [TntMessageAttribute(104)] Func<TMessageArg, Task<bool>> OnAskAsync { get; set; }
}