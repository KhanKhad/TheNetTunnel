using System;

namespace TheNetTunnel.Exceptions.ContractImplementation
{
    public class TypeCannotBeDeserializedException : Exception
    {
        private readonly Type _t;

        public TypeCannotBeDeserializedException(Type t) :
            base("Type " + t.Name + " cannot be deseserialized.")
        {
            _t = t;
        }
    }
}