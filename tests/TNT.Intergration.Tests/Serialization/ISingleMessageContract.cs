using TNT.Core.Contract;

namespace TNT.IntegrationTests.Serialization;

public interface ISingleMessageContract<TMessageArg>
{
    [TntMessage(1)] bool Ask(TMessageArg message);
}