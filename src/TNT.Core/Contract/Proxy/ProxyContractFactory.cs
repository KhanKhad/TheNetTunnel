using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using TNT.Core.Exceptions.ContractImplementation;
using TNT.Core.Presentation;
using TNT.Core.Presentation.Deserializers;

namespace TNT.Core.Contract.Proxy
{
    public static class ProxyContractFactory
    {
        private static int _exemmplarCounter;

        public static T CreateProxyContract<T>(IInterlocutor interlocutor, out Type finalType, out Dictionary<int, string> actionHandlers)
        {
            var interfaceType = typeof(T);
            TypeBuilder typeBuilder =  CreateProxyTypeBuilder<T>();

            typeBuilder.AddInterfaceImplementation(interfaceType);

            const string interlocutorFieldName = "_interlocutor";
            var outputApiFieldInfo = typeBuilder.DefineField(
                                        interlocutorFieldName, 
                                        typeof(IInterlocutor),
                                        FieldAttributes.Private);
            
            var sayMehodInfo = interlocutor.GetType().GetMethod("Say", new[] {typeof(int), typeof(object[])});
            var sayAsyncMehodInfo = interlocutor.GetType().GetMethod("SayAsync", new[] {typeof(int), typeof(object[])}); 
            
            var askMehodInfo = interlocutor.GetType().GetMethod("Ask", new[] {typeof(int), typeof(object[])});
            var askAsyncMehodInfo = interlocutor.GetType().GetMethod("AskAsync", new[] {typeof(int), typeof(object[])});

            var contractMemebers = ParseContractInterface(typeof(T));// new ContractsMemberInfo(typeof(T));

            foreach (var eventInfo in interfaceType.GetEvents())
                throw new InvalidContractMemeberException(eventInfo, interfaceType);


            #region interface methods implementation

            foreach (var method in contractMemebers.GetMethods())
            {
                var methodBuilder = EmitHelper.ImplementInterfaceMethod(method.Value, typeBuilder);

                var returnType = method.Value.ReturnType;
                MethodInfo askOrSayMethodInfo = null;

                if (returnType == typeof(void))
                {
                    askOrSayMethodInfo = sayMehodInfo;
                }
                else if (returnType == typeof(Task))
                {
                    askOrSayMethodInfo = sayAsyncMehodInfo;
                }
                else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var actualReturnType = returnType.GenericTypeArguments[0];
                    askOrSayMethodInfo = askAsyncMehodInfo.MakeGenericMethod(actualReturnType);
                }
                else
                {
                    askOrSayMethodInfo = askMehodInfo.MakeGenericMethod(returnType);
                }

                EmitHelper.GenerateSayOrAskMethodBody(
                    messageTypeId: method.Key,
                    interlocutorSayOrAskMethodInfo: askOrSayMethodInfo,
                    interlocutorFieldInfo: outputApiFieldInfo,
                    methodBuilder: methodBuilder,
                    callParameters: method.Value.GetParameters().Select(p=>p.ParameterType).ToArray());
            }

            #endregion

            var constructorCodeGeneration = new List<Action<ILGenerator>>();

            #region interface delegate properties implementation

            actionHandlers = new Dictionary<int, string>();

            foreach (var property in contractMemebers.GetProperties())
            {
                var propertyBuilder = EmitHelper.ImplementInterfaceProperty(typeBuilder, property.Value);

                var delegateInfo = ReflectionHelper.GetDelegateInfoOrNull(propertyBuilder.PropertyBuilder.PropertyType);
                if (delegateInfo == null)
                    //the property is not an delegate
                    throw new InvalidContractMemeberException(property.Value, interfaceType);

                // Create handler for every delegate property
                var handleMethodNuilder = ImplementAndGenerateHandleMethod(
                    typeBuilder, 
                    delegateInfo,
                    propertyBuilder.FieldBuilder);

                actionHandlers.Add(property.Key, handleMethodNuilder.Name);
            }
            #endregion

            EmitHelper.ImplementPublicConstructor(
                typeBuilder, 
                new [] {outputApiFieldInfo},
                constructorCodeGeneration);

            finalType = typeBuilder.CreateTypeInfo().AsType();

            var instance = (T)Activator.CreateInstance(finalType, interlocutor);

            return instance;
        }

        public static ContractInfo ParseContractInterface(Type contractInterfaceType)
        {
            var memberInfos = new ContractInfo(contractInterfaceType);

            foreach (var methodInfo in contractInterfaceType.GetMethods())
            {
                if (methodInfo.IsSpecialName) continue;

                var attribute = methodInfo.GetCustomAttribute<TntMessage>();
                if (attribute == null)
                    throw new ContractMemberAttributeMissingException(contractInterfaceType, methodInfo.Name);

                memberInfos.ThrowIfAlreadyContainsId(attribute.Id, methodInfo);
                memberInfos.AddInfo(attribute.Id, methodInfo);

            }
            foreach (var propertyInfo in contractInterfaceType.GetProperties())
            {
                var attribute = propertyInfo.GetCustomAttribute<TntMessage>();
                if (attribute == null)
                    throw new ContractMemberAttributeMissingException(contractInterfaceType, propertyInfo.Name);

                memberInfos.ThrowIfAlreadyContainsId(attribute.Id, propertyInfo);
                memberInfos.AddInfo(attribute.Id, propertyInfo);
            }
            return memberInfos;
        }

        private static TypeBuilder CreateProxyTypeBuilder<T>()
        {
            var typeCount = Interlocked.Increment(ref _exemmplarCounter);
            return EmitHelper.CreateTypeBuilder(typeof(T).Name + "_" + typeCount);
        }

        private static MethodBuilder ImplementAndGenerateHandleMethod(
            TypeBuilder typeBuilder,
            DelegatePropertyInfo delegatePropertyInfo,
            FieldInfo delegateFieldInfo)
        {
            /* ******************For Say Calls:******************
             * 
             * Action<int,DateTime,object, string> _originDelegatePropertyField;
             * 
             * public Action<int,DateTime,object, string,string> originDelegateProperty{ 
             *       get{return _originDelegatePropertyField;}
             *       set{_originDelegatePropertyField = value;}
             * }
             * 
             * private string Handle_originDelegatePropertyField(object[] arguments)
             * {
             *   ///we generate this code:
             *   var originDelegate = _originDelegatePropertyField;
		     *   if(originDelegate == null)
			 *      return 
		     *
			 *   originDelegate((int)  arguments[0], 
			 *	             (DateTime) arguments[1], 
			 *	             (object)   arguments[2],
			 *		         (string)   arguments[3]);
             *  }
             *
             * ******************For Ask Calls:******************
             * 
             * Func<int,DateTime,object, string,string> _originDelegatePropertyField;
             * 
             * public Func<int,DateTime,object, string,string> originDelegateProperty{ 
             *       get{return _originDelegatePropertyField;}
             *       set{_originDelegatePropertyField = value;}
             * }
             * 
             * private string Handle_originDelegatePropertyField(object[] arguments)
             * {
             *   ///we generate this code:
             *   var originDelegate = _originDelegatePropertyField;
		     *   
             *   string returnValue = null; ///а должно быть default(string)
             *   
             *   if(originDelegate!=null)
			 *     returnValue =  originDelegate((int)  arguments[0], 
			 *	             (DateTime) arguments[1], 
			 *	             (object)   arguments[2],
			 *		         (string)   arguments[3]);
             *
             *     return returnValue;
             *  }
             *  
             *  
             *  
             *  
             */

            var id = Interlocked.Increment(ref _exemmplarCounter);

            var hasParameters = delegatePropertyInfo.ParameterTypes.Length > 0;
            var parameters = hasParameters ? delegatePropertyInfo.ParameterTypes : Array.Empty<Type>();

            //build Handle method:
            var handleMethodBuilder = typeBuilder.DefineMethod(
                name: "Handle" + delegateFieldInfo.Name + id,
                attributes: MethodAttributes.Public,
                returnType: delegatePropertyInfo.ReturnType,
                parameterTypes: parameters);

            //
            ILGenerator ilGen = handleMethodBuilder.GetILGenerator();

            var endLabel = ilGen.DefineLabel();
            var callLabel = ilGen.DefineLabel();

            var localDelegateValue = ilGen.DeclareLocal(delegateFieldInfo.FieldType);
            var hasReturnType = delegatePropertyInfo.ReturnType != typeof(void);

            LocalBuilder returnValue = null;

            if (hasReturnType)
            {
                returnValue = ilGen.DeclareLocal(delegatePropertyInfo.ReturnType);
                ilGen.Emit(OpCodes.Ldloca_S, returnValue);
                ilGen.Emit(OpCodes.Initobj, delegatePropertyInfo.ReturnType);
            }

            //if (originDelegate == null)
            //return
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, delegateFieldInfo);

            ilGen.Emit(OpCodes.Stloc, localDelegateValue);
            ilGen.Emit(OpCodes.Ldloc, localDelegateValue);

            ilGen.Emit(OpCodes.Brfalse, endLabel);

            ilGen.Emit(OpCodes.Ldloc, localDelegateValue);

            if (!hasParameters)
                ilGen.Emit(OpCodes.Br, callLabel);

            for (int i = 0; i < delegatePropertyInfo.ParameterTypes.Length; i++)
            {
                ilGen.Emit(OpCodes.Ldarg_S, i + 1);
            }

            ilGen.MarkLabel(callLabel);

            ilGen.Emit(OpCodes.Callvirt, delegatePropertyInfo.DelegateInvokeMethodInfo);

            if (hasReturnType)
                ilGen.Emit(OpCodes.Stloc, returnValue);

            ilGen.MarkLabel(endLabel);

            if (hasReturnType)
                ilGen.Emit(OpCodes.Ldloc, returnValue);

            ilGen.Emit(OpCodes.Ret);
            
            return handleMethodBuilder;


            //var hasReturnType = delegatePropertyInfo.ReturnType != typeof(void);
            //LocalBuilder returnValue = null;

            //if (hasReturnType)
            //{
            //    //create local variable returnValue, equals zero
            //    returnValue = ilGen.DeclareLocal(delegatePropertyInfo.ReturnType);
            //    //we need set default(delegatePropertyInfo.ReturnType), but somehow it works. Little bit strange...
            //    ilGen.Emit(OpCodes.Ldnull);
            //    ilGen.Emit(OpCodes.Stloc, returnValue);
            //}

            //ilGen.Emit(OpCodes.Ldarg_0);

            ////check weather delegate == null
            //ilGen.Emit(OpCodes.Ldfld, delegateFieldInfo);
            //var delegateFieldValue = ilGen.DeclareLocal(delegateFieldInfo.FieldType);

            //ilGen.Emit(OpCodes.Stloc, localDelegateValue);
            //ilGen.Emit(OpCodes.Ldloc, localDelegateValue);

            //ilGen.Emit(OpCodes.Ldnull);
            //ilGen.Emit(OpCodes.Ceq);

            //var finishLabel = ilGen.DefineLabel();
            ////if field == null  than return
            //ilGen.Emit(OpCodes.Brtrue_S, finishLabel);

            //ilGen.Emit(OpCodes.Ldloc, localDelegateValue);

            
            //ilGen.Emit(OpCodes.Ldarg_1);
            //ilGen.Emit(OpCodes.Castclass, objectArrayType);

            //ilGen.Emit(OpCodes.Stloc, arguments);

            //int i = 0;
            ////fill the stack with call-arguments
            //foreach (var parameterType in delegatePropertyInfo.ParameterTypes)
            //{
            //    ilGen.Emit(OpCodes.Ldloc, arguments);
            //    ilGen.Emit(OpCodes.Ldc_I4, i);
            //    ilGen.Emit(OpCodes.Ldelem_Ref);

            //    if (parameterType.GetTypeInfo().IsValueType)
            //        ilGen.Emit(OpCodes.Unbox_Any, parameterType);
            //    else /*if (parameterType!= typeof(object))*/
            //        ilGen.Emit(OpCodes.Castclass, parameterType);
            //    i++;
            //}

            //ilGen.Emit(OpCodes.Callvirt, delegatePropertyInfo.DelegateInvokeMethodInfo);

            //if (hasReturnType)
            //{
            //    //set delegate call result to variable "returnValue"
            //    ilGen.Emit(OpCodes.Stloc, returnValue);
            //}
            //ilGen.MarkLabel(finishLabel);

            //if (hasReturnType)
            //    ilGen.Emit(OpCodes.Ldloc, returnValue);

            //ilGen.Emit(OpCodes.Ret);

            //return handleMethodBuilder;
        }
    }
}
