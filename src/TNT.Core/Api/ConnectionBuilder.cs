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
    public class ConnectionBuilder<TContract, TChannel> 
            where TChannel : IChannel
            where TContract: class 
    {
        private readonly PresentationBuilder<TContract> _contractBuilder;
        private readonly Func<TChannel>                 _channelFactory;
        private readonly Func<Task<TChannel>>      _channelFactoryAsync;

        public  ConnectionBuilder(PresentationBuilder<TContract> contractBuilder, Func<TChannel> channelFactory)
        {
            _contractBuilder = contractBuilder;
            _channelFactory  = channelFactory;
        }
        public ConnectionBuilder(PresentationBuilder<TContract> contractBuilder, Func<Task<TChannel>> channelFactory)
        {
            _contractBuilder = contractBuilder;
            _channelFactoryAsync = channelFactory;
        }
        public async Task<IConnection<TContract, TChannel>> BuildAsync()
        {
            TChannel channel;

            if (_channelFactoryAsync != null)
            {
                channel = await _channelFactoryAsync();
            }
            else
            {
                channel = _channelFactory();
            }

            var light = new Transporter(channel);

            TContract contract = _contractBuilder.OriginContractFactory == null
                ? CreateProxyContract(light)
                : CreateOriginContract(light);

            _contractBuilder.ContractInitializer(contract, channel);

            if (channel.IsConnected)
                channel.AllowReceive = true;

            return new Connection<TContract, TChannel>(contract, channel, _contractBuilder.ContractFinalizer);
        }
        public IConnection<TContract, TChannel> Build()
        {
            TChannel channel;

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

            var light   = new Transporter(channel);

            TContract contract = _contractBuilder.OriginContractFactory == null
                ? CreateProxyContract(light, channel)
                : CreateOriginContract(light, channel);

            _contractBuilder.ContractInitializer(contract, channel);

            if (channel.IsConnected)
                channel.AllowReceive = true;

            return new Connection<TContract, TChannel>(contract, channel, _contractBuilder.ContractFinalizer);
        }

        private TContract CreateOriginContract(Transporter light, TChannel channel)
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

            var messenger = new Messenger(
                light,
                SerializerFactory.CreateDefault(_contractBuilder.UserSerializationRules.ToArray()),
                DeserializerFactory.CreateDefault(_contractBuilder.UserDeserializationRules.ToArray()),
                outputMessages: outputMessages.ToArray(),
                inputMessages:  inputMessages.ToArray()
            );

            var interlocutor = new Interlocutor(messenger, dispatcher, _contractBuilder.MaxAnswerTimeoutDelay);

            var newInterlocutor = new NewInterlocutor(reflectionBuilder, new TntTcpClient(new IPEndPoint(IPAddress.Loopback, 124)));

            TContract contract = _contractBuilder.OriginContractFactory(light.Channel);
            OriginContractLinker.Link(contract, interlocutor);
            return contract;
        }
        private TContract CreateProxyContract(Transporter light, TChannel channel)
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

            var messenger = new Messenger(
                light,
                SerializerFactory.CreateDefault(_contractBuilder.UserSerializationRules.ToArray()),
                DeserializerFactory.CreateDefault(_contractBuilder.UserDeserializationRules.ToArray()),
                outputMessages: outputMessages.ToArray(),
                inputMessages: inputMessages.ToArray()
            );

            var interlocutor = new Interlocutor(messenger, dispatcher, _contractBuilder.MaxAnswerTimeoutDelay);

            var contract = ProxyContractFactory.CreateProxyContract<TContract>(interlocutor);
            return contract;
        }
        
    }
}