using System;
using TNT.Core.Transport;

// ReSharper disable once CheckNamespace
namespace TNT.Core.Api
{
    public static class TntBuilder
    {
        public static ContractBuilder<TContract> UseContract<TContract>()
            where TContract : class
        {
            return new ContractBuilder<TContract>();
        }

        public static ContractBuilder<TContract> UseContract<TContract, TImplementation>() 
            where TContract: class 
            where TImplementation: TContract, new()
        {
            return UseContract<TContract>((c) => new TImplementation());
        }
        public static ContractBuilder<TContract> UseContract<TContract>(TContract implementation)
            where TContract : class
        {
            return UseContract((c) => implementation);
        }
        public static ContractBuilder<TContract> UseContract<TContract>(Func<TContract> implementationFactory)
            where TContract : class
        {
            return UseContract((c) => implementationFactory());
        }

        public static ContractBuilder<TContract> UseContract<TContract>(Func<IChannel, TContract> implementationFactory)
            where TContract : class
        {
            return new ContractBuilder<TContract>(implementationFactory);
        }
    }
}