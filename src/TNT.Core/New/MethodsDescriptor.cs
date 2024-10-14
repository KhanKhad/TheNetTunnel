using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TNT.Core.Contract;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.Serializers;

namespace TNT.Core.New
{
    public class MethodsDescriptor
    {
        public Dictionary<int, MethodDesctiption> DescribedMethods;

        public SerializerFactory SerializerFactory;
        public DeserializerFactory DeserializerFactory;


        public MethodsDescriptor() 
        {
            DescribedMethods = new Dictionary<int, MethodDesctiption>();

            SerializerFactory = SerializerFactory.CreateDefault(Array.Empty<SerializationRule>());
            DeserializerFactory = DeserializerFactory.CreateDefault(Array.Empty<DeserializationRule>());
        }

        private object _contract;
        public void SetContract<TContract>(TContract contract) where TContract : class
        {
            _contract = contract;
        }

        public void CreateDescription(ContractInfo memebers)
        {
            foreach (var member in memebers.Memebers)
            {
                var description = MethodDesctiption.Create(SerializerFactory, DeserializerFactory, member.Value);
                
                if(DescribedMethods.ContainsKey(member.Key))
                    throw new Exception($"There is a message with same id: {member.Key}");

                DescribedMethods.Add(member.Key, description);
            }
        }

        public void SetHandler(int messageId, MethodInfo handler)
        {
            if (DescribedMethods.TryGetValue(messageId, out var methodDesctiption))
                methodDesctiption.MethodHandler = handler;
            else throw new Exception($"There isnt any message with {messageId} id");
        }
    }



    public class MethodDesctiption
    {
        public Type[] ArgumentTypes;

        /// <summary>
        /// maybe Task
        /// </summary>
        public Type ReturnType;

        /// <summary>
        /// Return type we should serialize/deserialize
        /// </summary>
        public Type ReturnTypeS;

        public bool HasReturnType {  get; set; }
        public ISerializer ReturnTypeSerializer {  get; set; }
        public IDeserializer ReturnTypeDeserializer {  get; set; }


        public bool HasArguments { get; set; }
        public int ArgumentsCount {  get; set; }
        public ISerializer ArgumentsSerializer { get; set; }
        public IDeserializer ArgumentsDeserializer { get; set; }

        public MethodInfo MethodHandler { get; set; }
        public MethodTypes MethodType { get; set; }
        public MethodDesctiption()
        {

        }

        public static MethodDesctiption Create(SerializerFactory serializerFactory, 
            DeserializerFactory deserializerFactory, MemberInfo member)
        {
            var result = new MethodDesctiption();

            if (member is MethodInfo methodInfo) 
            {
                result.ReturnType = methodInfo.ReturnType;
                result.ArgumentTypes = methodInfo.GetParameters()
                    .Select(p => p.ParameterType).ToArray();

            } 
            else if(member is PropertyInfo propertyInfo)
            {
                var delegateInfo = ReflectionHelper.GetDelegateInfoOrNull(propertyInfo.PropertyType);

                result.ArgumentTypes = delegateInfo.ParameterTypes;
                result.ReturnType = delegateInfo.ReturnType;
            }
            else throw new Exception("MethodDesctiption.Create(): Unknown member type");

            var returnType = result.ReturnType;

            result.HasReturnType = !(returnType == typeof(void) || returnType == typeof(Task));

            if (result.HasReturnType)
            {
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var actualReturnType = returnType.GenericTypeArguments[0];
                    result.ReturnTypeS = actualReturnType;
                    result.MethodType = MethodTypes.AsyncWithResult;
                }
                else
                {
                    result.ReturnTypeS = returnType;
                    result.MethodType = MethodTypes.SyncWithResult;
                }

                result.ReturnTypeSerializer = serializerFactory.Create(result.ReturnTypeS);
                result.ReturnTypeDeserializer = deserializerFactory.Create(result.ReturnTypeS);
            }
            else
            {
                if (returnType == typeof(Task))
                    result.MethodType = MethodTypes.AsyncWithoutResult;
                else result.MethodType = MethodTypes.SyncWithoutResult;
            }

            result.HasArguments = result.ArgumentTypes.Any();

            if (result.HasArguments)
            {
                result.ArgumentsCount = result.ArgumentTypes.Length;
                result.ArgumentsSerializer = serializerFactory.Create(result.ArgumentTypes);
                result.ArgumentsDeserializer = deserializerFactory.Create(result.ArgumentTypes);
            }

            return result;
        }
    }

    public enum MethodTypes
    {
        SyncWithoutResult,
        SyncWithResult,
        AsyncWithoutResult,
        AsyncWithResult,
    }
}
