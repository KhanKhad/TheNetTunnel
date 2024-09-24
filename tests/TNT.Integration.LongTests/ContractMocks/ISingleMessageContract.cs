using TNT;
using TNT.Core.Contract;

namespace Tnt.LongTests.ContractMocks;

public interface ISingleMessageContract<TMessageArg>
{
    [TntMessage(1)] bool Ask(TMessageArg message);
}