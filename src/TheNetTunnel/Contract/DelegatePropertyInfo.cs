using System;
using System.Reflection;

namespace TheNetTunnel.Contract
{
    public class DelegatePropertyInfo
    {
        public MethodInfo DelegateInvokeMethodInfo { get; set; }
        public Type[] ParameterTypes { get; set; }
        public Type ReturnType { get; set; }
    }
}