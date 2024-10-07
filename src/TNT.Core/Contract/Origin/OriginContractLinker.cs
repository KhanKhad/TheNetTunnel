using System;
using System.Reflection;
using System.Threading.Tasks;
using TNT.Core.Exceptions.ContractImplementation;
using TNT.Core.Presentation;
using TNT.Core.Presentation.Serializers;

namespace TNT.Core.Contract.Origin
{
    public static class OriginContractLinker
    {
        public static ContractInfo Link<TInterface>(TInterface contract, IInterlocutor interlocutor)
        {
            var contractType = contract.GetType();

            var interfaceType = typeof(TInterface);

            ContractInfo contractMemebers = GetContractMemebers(contractType, interfaceType);
            
            foreach (var method in contractMemebers.GetMethods())
            {
                var returnType = method.Value.ReturnParameter.ParameterType;

                if (returnType == typeof(void))
                {
                    //Say handler method:
                    interlocutor.SetIncomeSayCallHandler(method.Key, method.Value);
                }
                else if (returnType == typeof(Task))
                {
                    interlocutor.SetIncomeSayCallAsyncHandler(method.Key, method.Value);
                }
                else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    interlocutor.SetIncomeAskCallAsyncHandler(method.Key, method.Value);
                }
                else
                {
                    //Ask handler method:
                    interlocutor.SetIncomeAskCallHandler(method.Key, method.Value);
                }



                /*if (method.Value.ReturnParameter.ParameterType == typeof(void))
                {
                    //Say handler method:
                    interlocutor.SetIncomeSayCallHandler(method.Key, args => method.Value.Invoke(contract, args));
                }
                else
                {
                    //Ask handler method:
                    interlocutor.SetIncomeAskCallHandler(method.Key, args => method.Value.Invoke(contract, args));
                }*/
            }
            OriginCallbackDelegatesHandlerFactory.CreateFor(contractMemebers, contract, interlocutor);
            return contractMemebers;
        }

        public static ContractInfo GetContractMemebers(Type contractType, Type interfaceType)
        {
            var contractMemebers = new ContractInfo(interfaceType);
            foreach (var meth in interfaceType.GetMethods())
            {
                if (meth.IsSpecialName)
                    continue;

                var overrided = ReflectionHelper.GetOverridedMethodOrNull(contractType, meth);

                if (overrided == null)
                    continue;

                var attribute = meth.GetCustomAttribute<TntMessage>();
                if (attribute == null)
                    throw new ContractMemberAttributeMissingException(interfaceType, meth.Name);

                contractMemebers.ThrowIfAlreadyContainsId(attribute.Id, overrided);
                contractMemebers.AddInfo(attribute.Id, overrided);
            }
            foreach (var propertyInfo in interfaceType.GetTypeInfo().GetProperties())
            {
                var attribute = propertyInfo.GetCustomAttribute<TntMessage>();

                if (attribute == null)
                    throw new ContractMemberAttributeMissingException(interfaceType, propertyInfo.Name);

                var overrided = ReflectionHelper.GetOverridedPropertyOrNull(contractType, propertyInfo);
                if (overrided == null)
                    continue;

                contractMemebers.ThrowIfAlreadyContainsId(attribute.Id, overrided);
                contractMemebers.AddInfo(attribute.Id, overrided);
            }

            return contractMemebers;
        }
    }
}
