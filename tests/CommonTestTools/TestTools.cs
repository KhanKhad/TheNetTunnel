using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CommonTestTools;

public static class TestTools
{
    public static void AssertTrue(Func<bool> condition, int maxAwaitIntervalMs, string message = null)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > maxAwaitIntervalMs)
            {
                Assert.Fail(message);
                return;
            }
            Thread.Sleep(1);
        }
    }
    public static Task AssertNotBlocks(Action  action, int maxTimeout = 1000)
    {
        var task = Task.Factory.StartNew(action);
        Assert.IsTrue(task.Wait(maxTimeout), "call is blocked");
        return task;

    }
    public static Task<T> AssertNotBlocks<T>(Func<T> func, int maxTimeout = 100000)
    {
        var task = Task.Factory.StartNew(func);
        Assert.IsTrue(task.Wait(maxTimeout), "call is blocked");
        return task;

    }
    public static void AssertThrowsAndNotBlocks<TException>(Action action)
    {
        var task = AssertTryCatchAndTaskNotBlocks(action);
        Assert.IsInstanceOfType<TException>(task.Result);
    }

    public static async Task AssertThrowsAndNotBlocksAsync<TException>(Func<Task> action)
    {
        var result = await AssertTryCatchAndTaskNotBlocksAsync(action);
        Assert.IsInstanceOfType<TException>(result);
    }

    public static Task<Exception> AssertTryCatchAndTaskNotBlocks(Action action)
    {
        return AssertNotBlocks(
            ()=> {   try
                {
                    action();
                }
                catch (Exception e)
                {
                    return e;
                }
                return null;
            });
    }
    public static async Task<Exception> AssertTryCatchAndTaskNotBlocksAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception e)
        {
            return e;
        }

        return null;
    }
}