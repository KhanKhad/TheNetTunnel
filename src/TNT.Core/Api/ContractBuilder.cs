using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TNT.Core.Contract.Origin;
using TNT.Core.Contract.Proxy;
using TNT.Core.Presentation;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.Serializers;
using TNT.Core.Transport;
using System.Linq;
using TNT.Core.Contract;
using TNT.Core.ReceiveDispatching;

namespace TNT.Core.Api
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

            dispatcher.Start();

            TContract contract = OriginContractFactory == null
                ? CreateProxyContract(channel, dispatcher)
                : CreateOriginContract(channel, dispatcher);

            await channel.StartAsync();

            return new Connection<TContract>(contract, channel);
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

            TContract contract = OriginContractFactory == null
                ? CreateProxyContract(channel, dispatcher)
                : CreateOriginContract(channel, dispatcher);

            channel.Start();

            return new Connection<TContract>(contract, channel);
        }

        private TContract CreateOriginContract(IChannel channel, IDispatcher dispatcher)
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

            var newInterlocutor = new Interlocutor(dispatcher, channel, _maxAnsDelay);
            newInterlocutor.Initialize(_methodsDescriptor);

            dispatcher.SetContract(contract);
            dispatcher.Start();

            OriginCallbackDelegatesHandlerFactory.CreateFor(contractMemebers, contract, newInterlocutor);

            newInterlocutor.Start();

            return contract;
        }
        private TContract CreateProxyContract(IChannel channel, IDispatcher dispatcher)
        {
            var newInterlocutor = new Interlocutor(dispatcher, channel, _maxAnsDelay);
            var contract = ProxyContractFactory.CreateProxyContract<TContract>(newInterlocutor, out var finalType, out var actionHandlers);

            if(_methodsDescriptor == null)
            {
                _methodsDescriptor = new MethodsDescriptor();
                _methodsDescriptor.CreateDescription(ProxyContractFactory.ParseContractInterface(typeof(TContract)));

                foreach (var actionHandler in actionHandlers)
                {
                    var mm = finalType.GetMethod(actionHandler.Value);

                    //var params1 = new object[] { 4 };
                    //var res = mm.Invoke(instance, params1);

                    _methodsDescriptor.SetHandler(actionHandler.Key, mm);
                }
                _methodsDescriptor.SetContract(contract);
            }


            dispatcher.SetContract(contract);
            dispatcher.Start();

            newInterlocutor.Initialize(_methodsDescriptor);
            newInterlocutor.Start();

            return contract;
        }

    }
}