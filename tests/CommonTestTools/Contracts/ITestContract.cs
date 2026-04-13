using System;
using System.Threading.Tasks;
using TNT;
using TNT.Core.Contract;

namespace CommonTestTools.Contracts;

public interface ITestContract
{
    #region CommonSyncMessages
    [TntMessageAttribute(1)] void Say();
    [TntMessageAttribute(2)] void Say(string s);
    [TntMessageAttribute(3)] void Say(string s, int i, long l);
    [TntMessageAttribute(4)] int Ask();
    [TntMessageAttribute(5)] string Ask(string s);
    [TntMessageAttribute(6)] string Ask(string s, int i, long l);
    #endregion

    #region CommonAsyncMessages
    [TntMessageAttribute(11)] Task SayAsync();
    [TntMessageAttribute(12)] Task SayAsync(string s);
    [TntMessageAttribute(13)] Task SayAsync(string s, int i, long l);
    [TntMessageAttribute(14)] Task<int> AskAsync();
    [TntMessageAttribute(15)] Task<string> AskAsync(string s);
    [TntMessageAttribute(16)] Task<string> AskAsync(string s, int i, long l);
    #endregion


    #region CommonSyncMessagesWithException
    [TntMessageAttribute(21)] void SayWithException();
    [TntMessageAttribute(22)] string AskWithException(string s);
    #endregion

    #region CommonSyncMessagesWithException
    [TntMessageAttribute(31)] Task SayWithExceptionAsync(string s);
    [TntMessageAttribute(32)] Task<string> AskWithExceptionAsync();
    #endregion



    #region Sync Actions/Funcs
    [TntMessageAttribute(101)] Action OnSay { get; set; }
    [TntMessageAttribute(102)] Action<string> OnSayS { get; set; }
    [TntMessageAttribute(103)] Action<string, int, long> OnSaySIL { get; set; }
    [TntMessageAttribute(104)] Func<int> OnAsk { get; set; }
    [TntMessageAttribute(105)] Func<string, string> OnAskS { get; set; }
    [TntMessageAttribute(106)] Func<string,int, long, string> OnAskSIL { get; set; }
    #endregion

    #region Async Actions/Funcs
    [TntMessageAttribute(114)] Func<Task> FuncTask { get; set; }
    [TntMessageAttribute(115)] Func<Task<int>> FuncTaskWithResult { get; set; }
    [TntMessageAttribute(116)] Func<string, Task<string>> FuncTaskWithResultAndParam { get; set; }
    [TntMessageAttribute(117)] Func<string, int, long, Task<string>> FuncTaskWithResultIL { get; set; }
    #endregion
}