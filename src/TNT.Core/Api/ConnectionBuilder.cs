using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TNT.Core.Contract;
using TNT.Core.Contract.Origin;
using TNT.Core.Contract.Proxy;
using TNT.Core.New;
using TNT.Core.New.Tcp;
using TNT.Core.Presentation;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.Serializers;
using TNT.Core.Transport;

namespace TNT.Core.Api
{
    public class ConnectionBuilder<TContract> 
            where TContract: class 
    {
        private readonly PresentationBuilder<TContract> _contractBuilder;
        private readonly Func<IChannel>                 _channelFactory;
        private readonly Func<Task<IChannel>>      _channelFactoryAsync;

        public  ConnectionBuilder(PresentationBuilder<TContract> contractBuilder, Func<IChannel> channelFactory)
        {
            _contractBuilder = contractBuilder;
            _channelFactory  = channelFactory;
        }
        public ConnectionBuilder(PresentationBuilder<TContract> contractBuilder, Func<Task<IChannel>> channelFactory)
        {
            _contractBuilder = contractBuilder;
            _channelFactoryAsync = channelFactory;
        }
        public async Task<IConnection<TContract>> BuildAsync()
        {
            IChannel channel;

            if (_channelFactoryAsync != null)
            {
                channel = await _channelFactoryAsync();
            }
            else
            {
                channel = _channelFactory();
            }

            TContract contract = _contractBuilder.OriginContractFactory == null
                ? CreateProxyContract(channel)
                : CreateOriginContract(channel);

            _contractBuilder.ContractInitializer(contract, channel);

            channel.Start();

            return new Connection<TContract>(contract, channel, _contractBuilder.ContractFinalizer);
        }
        public IConnection<TContract> Build()
        {
            IChannel channel;

            if (_channelFactory != null)
            {
                channel = _channelFactory();
            }
            else
            {
                var task = _channelFactoryAsync();
                task.Wait();
                channel = task.Result;
            }

            TContract contract = _contractBuilder.OriginContractFactory == null
                ? CreateProxyContract(channel)
                : CreateOriginContract(channel);

            _contractBuilder.ContractInitializer(contract, channel);

            channel.Start();

            return new Connection<TContract>(contract, channel, _contractBuilder.ContractFinalizer);
        }

        private TContract CreateOriginContract(IChannel channel)
        {
            var memebers = ProxyContractFactory.ParseContractInterface(typeof(TContract));
            var dispatcher = _contractBuilder.ReceiveDispatcherFactory();
            var inputMessages = memebers.GetMethods().Select(m => new MessageTypeInfo
            {
                ReturnType = m.Value.ReturnType,
                ArgumentTypes = m.Value.GetParameters().Select(p => p.ParameterType).ToArray(),
                MessageId = (short)m.Key
            });

            var outputMessages = memebers.GetProperties().Select(m => new MessageTypeInfo
            {
                ArgumentTypes = Contract.ReflectionHelper.GetDelegateInfoOrNull(m.Value.PropertyType).ParameterTypes,
                ReturnType = Contract.ReflectionHelper.GetDelegateInfoOrNull(m.Value.PropertyType).ReturnType,
                MessageId = (short)m.Key
            });

            var reflectionBuilder = new NewReflectionHelper(
                SerializerFactory.CreateDefault(_contractBuilder.UserSerializationRules.ToArray()),
                DeserializerFactory.CreateDefault(_contractBuilder.UserDeserializationRules.ToArray()),
                outputMessages: outputMessages.ToArray(),
                inputMessages: inputMessages.ToArray());

            var newInterlocutor = new NewInterlocutor(reflectionBuilder, dispatcher, (TntTcpClient)channel);

            newInterlocutor.Start();

            TContract contract = _contractBuilder.OriginContractFactory(channel);
            OriginContractLinker.Link(contract, newInterlocutor);
            return contract;
        }
        private TContract CreateProxyContract(IChannel channel)
        {
            var memebers   = ProxyContractFactory.ParseContractInterface(typeof(TContract));
            var dispatcher = _contractBuilder.ReceiveDispatcherFactory();

            var outputMessages = memebers.GetMethods().Select(m => new MessageTypeInfo
            {
                ReturnType    = m.Value.ReturnType,
                ArgumentTypes = m.Value.GetParameters().Select(p => p.ParameterType).ToArray(),
                MessageId     = (short)m.Key
            });

            var inputMessages = memebers.GetProperties().Select(m => new MessageTypeInfo
            {
                ArgumentTypes = ReflectionHelper.GetDelegateInfoOrNull(m.Value.PropertyType).ParameterTypes,
                ReturnType    = ReflectionHelper.GetDelegateInfoOrNull(m.Value.PropertyType).ReturnType,
                MessageId     = (short)m.Key
            });

            var reflectionBuilder = new NewReflectionHelper(
                SerializerFactory.CreateDefault(_contractBuilder.UserSerializationRules.ToArray()),
                DeserializerFactory.CreateDefault(_contractBuilder.UserDeserializationRules.ToArray()),
                outputMessages: outputMessages.ToArray(),
                inputMessages: inputMessages.ToArray());

            var newInterlocutor = new NewInterlocutor(reflectionBuilder, dispatcher, (TntTcpClient)channel);

            newInterlocutor.Start();

            var contract = ProxyContractFactory.CreateProxyContract<TContract>(newInterlocutor);
            return contract;
        }
        
    }
}