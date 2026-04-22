using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using TheNetTunnel.Contract.Origin;
using TheNetTunnel.Contract.Proxy;
using TheNetTunnel.Presentation;
using TheNetTunnel.Presentation.Deserializers;
using TheNetTunnel.Presentation.Serializers;
using TheNetTunnel.Transport;
using System.Linq;
using TheNetTunnel.Contract;
using TheNetTunnel.ReceiveDispatching;

namespace TheNetTunnel.Api
{
    public class ContractBuilder<TContract> where TContract:class
    {
        private IDispatcher _receiveDispatcher;
        private int _maxAnsDelay = 30000;

        public List<DeserializationRule> UserDeserializationRules { get; } = new List<DeserializationRule>();

        public List<SerializationRule>   UserSerializationRules   { get; } = new List<SerializationRule>();

        private IChannel _channel;
        private Func<IChannel> _channelFactory;
        private Func<Task<IChannel>> _channelFactoryAsync;
        private MethodsDescriptor _methodsDescriptor;
        private bool _fullmode;

        /// <summary>
        /// Contract implementation
        /// </summary>
        public Func<IChannel, TContract> OriginContractFactory { get; }

        internal ContractBuilder()
        {
            OriginContractFactory = null;
        }
        internal ContractBuilder(Func<IChannel, TContract> contractFactory)
        {
            OriginContractFactory = contractFactory ?? throw new ArgumentNullException(nameof(contractFactory));
        }

        public ContractBuilder<TContract> SetMaxAnsTimeout(int delay)
        {
            _maxAnsDelay = delay;
            return this;
        }

        #region Dispatcher
        public ContractBuilder<TContract> UseReceiveDispatcher(IDispatcher dispatcher)
        {
            _receiveDispatcher = dispatcher;
            return this;
        }
        public ContractBuilder<TContract> UseSingleOperationDispatcher()
        {
            _receiveDispatcher = new ReceiveDispatcher();
            return this;
        }
        public ContractBuilder<TContract> UseMultiOperationDispatcher()
        {
            _receiveDispatcher = new ReceiveDispatcher(false);
            return this;
        }
        #endregion

        #region UserSerializers
        public ContractBuilder<TContract> UseSerializer(SerializationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            UserSerializationRules.Add(rule);
            return this;
        }

        public ContractBuilder<TContract> UseDeserializer(DeserializationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            UserDeserializationRules.Add(rule);
            return this;
        }
        #endregion

        #region Channel
        public ContractBuilder<TContract> UseChannel(IChannel channel) 
        {
            _channel = channel;
            return this;
        }

        public ContractBuilder<TContract> UseChannelFactory(Func<IChannel> channelFactory) 
        {
            _channelFactory = channelFactory;
            return this;
        }
        public ContractBuilder<TContract> UseAsyncChannelFactory(Func<Task<IChannel>> channelFactory)
        {
            _channelFactoryAsync = channelFactory;
            return this;
        }
        #endregion

        public ContractBuilder<TContract> SetFullMode()
        {
            _fullmode = true;
            return this;
        }

        public async Task<IConnection<TContract>> BuildAsync()
        {
            IChannel channel = null;

            if (_channel != null)
                channel = _channel;
            else if (_channelFactoryAsync != null)
                channel = await _channelFactoryAsync();
            else if(_channelFactory != null)
                channel = _channelFactory();

            if(channel == null)
                throw new ArgumentNullException(nameof(_channel));

            var dispatcher = _receiveDispatcher ?? new ReceiveDispatcher();

            (TContract contract, IInterlocutor interlocutor) = OriginContractFactory == null
                ? CreateProxyContract(channel, dispatcher)
                : CreateOriginContract(channel, dispatcher);

            await channel.StartAsync();

            if (OriginContractFactory == null)
            {
                var (AvailableForWork, UnavailabilityReason) = await interlocutor.SendHelloMessageAsync();

                if (!AvailableForWork)
                {
                    await interlocutor.DisposeAsync();
                    throw new Exception($"Interlocutor is not available for work. Unavailability reason: {UnavailabilityReason}");
                }
            }

            return new Connection<TContract>(contract, channel, interlocutor);
        }
        public IConnection<TContract> Build()
        {
            IChannel channel = null;

            if (_channel != null)
                channel = _channel;
            else if (_channelFactoryAsync != null)
                channel = _channelFactoryAsync().Result;
            else if (_channelFactory != null)
                channel = _channelFactory();

            if (channel == null)
                throw new ArgumentNullException(nameof(_channel));

            var dispatcher = _receiveDispatcher ?? new ReceiveDispatcher();

            (TContract contract, IInterlocutor interlocutor) = OriginContractFactory == null
                ? CreateProxyContract(channel, dispatcher)
                : CreateOriginContract(channel, dispatcher);

            channel.Start();

            if (OriginContractFactory == null)
            {
                var (AvailableForWork, UnavailabilityReason) = interlocutor.SendHelloMessageAsync().GetAwaiter().GetResult();

                if (!AvailableForWork)
                {
                    interlocutor.Dispose();
                    throw new Exception($"Interlocutor is not available for work. Unavailability reason: {UnavailabilityReason}");
                }
            }

            return new Connection<TContract>(contract, channel, interlocutor);
        }

        private (TContract contract, IInterlocutor interlocutor) CreateOriginContract(IChannel channel, IDispatcher dispatcher)
        {
            TContract contract = OriginContractFactory(channel);

            var contractType = contract.GetType();

            var interfaceType = typeof(TContract);

            var contractMemebers = OriginContractLinker.GetContractMemebers(contractType, interfaceType);

            if (_methodsDescriptor == null)
            {
                _methodsDescriptor = new MethodsDescriptor();
                _methodsDescriptor.CreateDescription(ProxyContractFactory.ParseContractInterface(typeof(TContract)));

                foreach (var method in contractMemebers.GetMethods())
                {
                    _methodsDescriptor.SetHandler(method.Key, method.Value);
                }

                _methodsDescriptor.SetContract(contract);
            }

            var interlocutorProperties = CreateProperties();
            interlocutorProperties.ServerMode = true;

            var interlocutor = new Interlocutor(dispatcher, channel, interlocutorProperties);
            interlocutor.Initialize(_methodsDescriptor);

            dispatcher.SetContract(contract);
            dispatcher.Start();

            OriginCallbackDelegatesHandlerFactory.CreateFor(contractMemebers, contract, interlocutor);

            interlocutor.Start();

            return (contract, interlocutor);
        }

        private (TContract contract, IInterlocutor interlocutor) CreateProxyContract(IChannel channel, IDispatcher dispatcher)
        {
            var interlocutorProperties = CreateProperties();

            var interlocutor = new Interlocutor(dispatcher, channel, interlocutorProperties);
            var contract = ProxyContractFactory.CreateProxyContract<TContract>(interlocutor, out var finalType, out var actionHandlers);

            if(_methodsDescriptor == null)
            {
                _methodsDescriptor = new MethodsDescriptor();
                _methodsDescriptor.CreateDescription(ProxyContractFactory.ParseContractInterface(typeof(TContract)));

                foreach (var actionHandler in actionHandlers)
                    _methodsDescriptor.SetHandler(actionHandler.Key, finalType.GetMethod(actionHandler.Value));

                _methodsDescriptor.SetContract(contract);
            }

            dispatcher.SetContract(contract);
            dispatcher.Start();

            interlocutor.Initialize(_methodsDescriptor);
            interlocutor.Start();

            return (contract, interlocutor);
        }

        private InterlocutorProperties CreateProperties()
        {
            var type = typeof(TContract);

            var interlocutorProperties = new InterlocutorProperties()
            {
                ClientVersion = type.GetCustomAttribute<TntClientVersion>()?.Version ?? new Version(1, 0, 0),
                ServerVersion = type.GetCustomAttribute<TntServerVersion>()?.Version ?? new Version(1, 0, 0),
                MinimalClientVersion = type.GetCustomAttribute<TntMinimalClientVersion>()?.Version ?? new Version(1, 0, 0),
                MinimalServerVersion = type.GetCustomAttribute<TntMinimalServerVersion>()?.Version ?? new Version(1, 0, 0),
                Fullmode = _fullmode,
                DefaultMaxAnsDelay = _maxAnsDelay,
                DefaultPingInterval = 5000,
            };

            return interlocutorProperties;
        }
    }
}