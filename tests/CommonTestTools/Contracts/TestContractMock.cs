using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CommonTestTools.Contracts;

public class TestContractMock : ITestContract
{
    public const int AskReturns = 42;

    public int SayCalledCount { get; set; }

    public ConcurrentBag<string> SaySCalled { get; } = new ConcurrentBag<string>();

    public void Say()
    {
        SayCalledCount++;
    }

    public Task SayAsync()
    {
        SayCalledCount++;
        return Task.CompletedTask;
    }
    
    public void Say(string s)
    {
        SayCalledCount++;
        SaySCalled.Add(s);
    }
    public Task SayAsync(string s)
    {
        SayCalledCount++;
        SaySCalled.Add(s);
        return Task.CompletedTask;
    }

    public void Say(string s, int i, long l)
    {

    }

    public Task SayAsync(string s, int i, long l)
    {
        return Task.CompletedTask;
    }

    public int Ask()
    {
        return AskReturns;
    }

    public Task<int> AskAsync()
    {
        return Task.FromResult(AskReturns);
    }

    public string Ask(string s)
    {
        return s;
    }

    public Task<string> AskAsync(string s)
    {
        return Task.FromResult(s);
    }


    private Func<string, int, long, string> _whenAskSILCalled = (s, i, l) => "0";
    public void WhenAskSILCalledCall(Func<string, int, long, string> whenAskSILCalled)
    {
        _whenAskSILCalled = whenAskSILCalled;
    }
    public string Ask(string s, int i, long l)
    {
        return _whenAskSILCalled(s, i, l);
    }

    public Task<string> AskAsync(string s, int i, long l)
    {
        throw new NotImplementedException();
    }

    public void SayWithException()
    {
        throw new Exception("SayWithException");
    }
    public void SayWithException(string s)
    {
        throw new Exception("SayWithException");
    }

    public string AskWithException(string s)
    {
        throw new Exception("AskWithException");
    }

    public Task SayWithExceptionAsync(string s)
    {
        throw new Exception("SayWithExceptionAsync");
    }

    public Task<string> AskWithExceptionAsync()
    {
        throw new Exception("AskWithExceptionAsync");
    }

    public Action OnSay { get; set; }
    public Action<string> OnSayS { get; set; }
    public Action<string, int, long> OnSaySIL { get; set; }
    public Func<int> OnAsk { get; set; }
    public Func<string, string> OnAskS { get; set; }
    public Func<string, int, long, string> OnAskSIL { get; set; }
    public Func<Task<int>> FuncTaskWithResult { get; set ; }
    public Func<string, Task<string>> FuncTaskWithResultAndParam { get; set; }
    public Func<string, int, long, Task<string>> FuncTaskWithResultIL { get; set; }
    public Func<Task> FuncTask { get; set; }
}