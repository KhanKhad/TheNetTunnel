using System;

// ReSharper disable once CheckNamespace
namespace TheNetTunnel.Contract
{
    [AttributeUsage( AttributeTargets.Method
        | AttributeTargets.Property, AllowMultiple = false, Inherited= true)]
    public class TntMessageAttribute: Attribute
    {
        private readonly ushort _id;

        public TntMessageAttribute(ushort id)
        {
            _id = id;
        }

        public ushort Id => _id;
    }
}