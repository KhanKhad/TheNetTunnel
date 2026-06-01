using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace TheNetTunnel.ReceiveDispatching
{
    /// <summary>
    /// Caches compiled accessors so contract methods are invoked through
    /// expression-tree delegates instead of <see cref="MethodInfo.Invoke"/>,
    /// and <see cref="Task{TResult}.Result"/> is read without per-call reflection.
    /// </summary>
    internal static class DelegateCache
    {
        private static readonly ConcurrentDictionary<MethodInfo, Func<object, object[], object>> _invokers = new();
        private static readonly ConcurrentDictionary<Type, Func<Task, object>> _taskResultReaders = new();

        public static object Invoke(MethodInfo method, object instance, object[] args)
            => _invokers.GetOrAdd(method, BuildInvoker)(instance, args);

        public static object ReadTaskResult(Task task)
            => _taskResultReaders.GetOrAdd(task.GetType(), BuildTaskResultReader)(task);

        private static Func<object, object[], object> BuildInvoker(MethodInfo method)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            var parameters = method.GetParameters();
            var argExpressions = new Expression[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var indexed = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                argExpressions[i] = Expression.Convert(indexed, parameters[i].ParameterType);
            }

            var instanceCast = method.IsStatic
                ? null
                : Expression.Convert(instanceParam, method.DeclaringType);

            var call = Expression.Call(instanceCast, method, argExpressions);

            Expression body = method.ReturnType == typeof(void)
                ? Expression.Block(call, Expression.Constant(null, typeof(object)))
                : Expression.Convert(call, typeof(object));

            return Expression.Lambda<Func<object, object[], object>>(body, instanceParam, argsParam).Compile();
        }

        private static Func<Task, object> BuildTaskResultReader(Type taskType)
        {
            var resultProperty = taskType.GetProperty(nameof(Task<object>.Result));

            // Non-generic Task has no Result property.
            if (resultProperty == null)
                return _ => null;

            var taskParam = Expression.Parameter(typeof(Task), "task");
            var typedTask = Expression.Convert(taskParam, taskType);
            var resultAccess = Expression.Property(typedTask, resultProperty);
            var body = Expression.Convert(resultAccess, typeof(object));

            return Expression.Lambda<Func<Task, object>>(body, taskParam).Compile();
        }
    }
}
