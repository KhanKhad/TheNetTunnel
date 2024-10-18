using System;
using System.Threading.Tasks;
using TNT;
using TNT.Core.Contract;

namespace Tnt.LongTests.ContractMocks;

public interface ILongTestContract<TMessageArg>
{
    [TntMessage(1)] void Say(TMessageArg s);
    [TntMessage(2)] bool Ask(TMessageArg message);

    [TntMessage(3)] Task SayAsync(TMessageArg s);
    [TntMessage(4)] Task<bool> AskAsync(TMessageArg s);


    [TntMessage(101)] Action<TMessageArg> OnSay { get; set; }
    [TntMessage(102)] Func<TMessageArg, bool> OnAsk { get; set; }

    [TntMessage(103)] Func<TMessageArg, Task> OnSayAsync { get; set; }
    [TntMessage(104)] Func<TMessageArg, Task<bool>> OnAskAsync { get; set; }
}