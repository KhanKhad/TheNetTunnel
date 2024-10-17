using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CommonTestTools;

public static class TestTools
{
    public static async Task AssertNotBlocks(Action action, int timeout = 1000)
    {
        var task = Task.Run(action);
        var delayTask = Task.Delay(timeout);

        var result = await Task.WhenAny(task, delayTask);

        Assert.IsTrue(result == task, "call is blocked");

        await task;
    }
    public static async Task<T> AssertNotBlocks<T>(Func<T> action, int timeout = 1000)
    {
        var task = Task.Run(action);
        var delayTask = Task.Delay(timeout);

        var result = await Task.WhenAny(task, delayTask);

        Assert.IsTrue(result == task, "call is blocked");

        return await task;
    }
    public static async Task AssertNotBlocks(Func<Task> action, int timeout = 1000)
    {
        var task = Task.Run(async () => await action());

        var delayTask = Task.Delay(timeout);

        var result = await Task.WhenAny(task, delayTask);

        Assert.IsTrue(result == task, "call is blocked");

        await task;
    }
    public static async Task<T> AssertNotBlocks<T>(Func<Task<T>> action, int timeout = 1000)
    {
        var task = Task.Run(async () =>  await action());
        var delayTask = Task.Delay(timeout);

        var result = await Task.WhenAny(task, delayTask);
        Assert.IsTrue(result == task, "call is blocked");

        return await task;
    }

    public static async Task AssertThrowsAndNotBlocks<TException>(Action action, int timeout = 1000)
    {
        var task = Task.Run(action);
        var delayTask = Task.Delay(timeout);

        var result = await Task.WhenAny(task, delayTask);

        if (result == task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType<TException>(e);
            }
        }
        else Assert.Fail("call is blocked");
    }
    public static async Task AssertThrowsAndNotBlocks<TException>(Func<Task> action, int timeout = 1000)
    {
        var task = Task.Run(async () => await action());
        var delayTask = Task.Delay(timeout);

        var result = await Task.WhenAny(task, delayTask);

        if (result == task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType<TException>(e);
            }
        }
        else Assert.Fail("call is blocked");
    }
}