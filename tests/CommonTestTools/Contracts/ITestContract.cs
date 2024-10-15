using System;
using System.Threading.Tasks;
using TNT;
using TNT.Core.Contract;

namespace CommonTestTools.Contracts;

public interface ITestContract
{
    #region CommonSyncMessages
    [TntMessage(1)] void Say();
    [TntMessage(2)] void Say(string s);
    [TntMessage(3)] void Say(string s, int i, long l);
    [TntMessage(4)] int Ask();
    [TntMessage(5)] string Ask(string s);
    [TntMessage(6)] string Ask(string s, int i, long l);
    #endregion

    #region CommonAsyncMessages
    [TntMessage(11)] Task SayAsync();
    [TntMessage(12)] Task SayAsync(string s);
    [TntMessage(13)] Task SayAsync(string s, int i, long l);
    [TntMessage(14)] Task<int> AskAsync();
    [TntMessage(15)] Task<string> AskAsync(string s);
    [TntMessage(16)] Task<string> AskAsync(string s, int i, long l);
    #endregion


    #region CommonSyncMessagesWithException
    [TntMessage(21)] void SayWithException();
    [TntMessage(22)] string AskWithException(string s);
    #endregion

    #region CommonSyncMessagesWithException
    [TntMessage(31)] Task SayWithExceptionAsync(string s);
    [TntMessage(32)] Task<string> AskWithExceptionAsync();
    #endregion



    #region Sync Actions/Funcs
    [TntMessage(101)] Action OnSay { get; set; }
    [TntMessage(102)] Action<string> OnSayS { get; set; }
    [TntMessage(103)] Action<string, int, long> OnSaySIL { get; set; }
    [TntMessage(104)] Func<int> OnAsk { get; set; }
    [TntMessage(105)] Func<string, string> OnAskS { get; set; }
    [TntMessage(106)] Func<string,int, long, string> OnAskSIL { get; set; }
    #endregion

    #region Async Actions/Funcs
    [TntMessage(114)] Func<Task<int>> FuncTask { get; set; }
    [TntMessage(115)] Func<string, Task<string>> FuncTaskWithResult { get; set; }
    [TntMessage(116)] Func<string, int, long, Task<string>> FuncTaskWithResultIL { get; set; }
    #endregion
}