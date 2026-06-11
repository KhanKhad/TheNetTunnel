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
    public class ContractBuilder<TContract> : IDisposable where TContract:class
    {
        private IDispatcher _receiveDispatcher;
        private int _maxAnsDelay = 30000;
        private int _maxFrameLength = ReceivePduQueue.DefaultMaxFrameLength;

        public List<DeserializationRule> UserDeserializationRules { get; } = new List<DeserializationRule>();

        public List<SerializationRule>   UserSerializationRules   { get; } = new List<SerializationRule>();

        private IChannel _channel;
        private Func<IChannel> _channelFactory;
        private Func<Task<IChannel>> _channelFactoryAsync;
        private MethodsDescriptor _methodsDescriptor;

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

        /// <summary>
        /// Maximum allowed size, in bytes, of a single incoming frame payload.
        /// Frames declaring a larger (or negative) length are rejected before any
        /// allocation and the connection is dropped. Defaults to
        /// <see cref="ReceivePduQueue.DefaultMaxFrameLength"/> (64 MB).
        /// </summary>
        public ContractBuilder<TContract> SetMaxFrameLength(int maxFrameLength)
        {
            if (maxFrameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength));

            _maxFrameLength = maxFrameLength;
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

        public async Task<IConnection<TContract>> BuildAsync(bool fullmode = false)
        {
            IChannel channel = null;

            if (_channel != null)
                channel = _channel;
            else if (_channelFactoryAsync != null)
                channel = await _channelFactoryAsync().ConfigureAwait(false);
            else if(_channelFactory != null)
                channel = _channelFactory();

            if(channel == null)
                throw new ArgumentNullException(nameof(_channel));

            // A dispatcher created here belongs to this connection and must be
            // disposed with it; a user-supplied one is shared and outlives us.
            var ownsDispatcher = _receiveDispatcher == null;
            var dispatcher = _receiveDispatcher ?? new ReceiveDispatcher();

            await channel.StartAsync().ConfigureAwait(false);

            TContract contract;
            IInterlocutor interlocutor;

            if (OriginContractFactory == null)
            {
                (contract, interlocutor) = CreateProxyContract(channel, dispatcher);

                var (AvailableForWork, UnavailabilityReason) = await interlocutor.SendHelloMessageAsync().ConfigureAwait(false);

                if (!AvailableForWork)
                {
                    await interlocutor.DisposeAsync().ConfigureAwait(false);

                    if (ownsDispatcher)
                        await dispatcher.DisposeAsync().ConfigureAwait(false);

                    throw new Exception($"Interlocutor is not available for work. Unavailability reason: {UnavailabilityReason}");
                }
            }
            else
                (contract, interlocutor) = CreateOriginContract(channel, dispatcher, fullmode);

            return new Connection<TContract>(contract, channel, interlocutor);
        }
        public IConnection<TContract> Build(bool fullmode = false)
        {
            IChannel channel = null;

            if (_channel != null)
                channel = _channel;
            else if (_channelFactoryAsync != null)
                channel = _channelFactoryAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            else if (_channelFactory != null)
                channel = _channelFactory();

            if (channel == null)
                throw new ArgumentNullException(nameof(_channel));

            var ownsDispatcher = _receiveDispatcher == null;
            var dispatcher = _receiveDispatcher ?? new ReceiveDispatcher();

            channel.Start();

            TContract contract;
            IInterlocutor interlocutor;

            if (OriginContractFactory == null)
            {
                (contract, interlocutor) = CreateProxyContract(channel, dispatcher);

                var (AvailableForWork, UnavailabilityReason) = interlocutor.SendHelloMessageAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                if (!AvailableForWork)
                {
                    interlocutor.Dispose();

                    if (ownsDispatcher)
                        dispatcher.Dispose();

                    throw new Exception($"Interlocutor is not available for work. Unavailability reason: {UnavailabilityReason}");
                }
            }
            else
                (contract, interlocutor) = CreateOriginContract(channel, dispatcher, fullmode);

            return new Connection<TContract>(contract, channel, interlocutor);
        }

        private (TContract contract, IInterlocutor interlocutor) CreateOriginContract(IChannel channel, IDispatcher dispatcher, bool fullmode)
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

            var interlocutorProperties = CreateProperties(fullmode);
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
            var interlocutorProperties = CreateProperties(false);

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

        private InterlocutorProperties CreateProperties(bool fullmode)
        {
            var type = typeof(TContract);

            var interlocutorProperties = new InterlocutorProperties()
            {
                ClientVersion = type.GetCustomAttribute<TntClientVersion>()?.Version ?? new Version(1, 0, 0),
                ServerVersion = type.GetCustomAttribute<TntServerVersion>()?.Version ?? new Version(1, 0, 0),
                MinimalClientVersion = type.GetCustomAttribute<TntMinimalClientVersion>()?.Version ?? new Version(1, 0, 0),
                MinimalServerVersion = type.GetCustomAttribute<TntMinimalServerVersion>()?.Version ?? new Version(1, 0, 0),
                Fullmode = fullmode,
                DefaultMaxAnsDelay = _maxAnsDelay,
                DefaultPingInterval = 5000,
                MaxFrameLength = _maxFrameLength,
            };

            return interlocutorProperties;
        }

        public void Dispose()
        {
            _receiveDispatcher?.Dispose();
        }
    }
}