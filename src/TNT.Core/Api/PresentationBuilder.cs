using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TNT.Core.Presentation;
using TNT.Core.Presentation.Deserializers;
using TNT.Core.Presentation.ReceiveDispatching;
using TNT.Core.Presentation.Serializers;
using TNT.Core.Transport;

namespace TNT.Core.Api
{
    public class PresentationBuilder<TContract> where TContract:class
    {
        public Func<IDispatcher> ReceiveDispatcherFactory { get; private set; } = ()=> new TaskDispatcher();

        public Action<TContract, IChannel> ContractInitializer { get; private set; } = (contract, channel) => { };
        public Action<TContract, IChannel, ErrorMessage> ContractFinalizer   { get; private set; } = (contract, channel, cause) => { };

        private int _maxAnsDelay = 30000;
        public int MaxAnswerTimeoutDelay => _maxAnsDelay;
        public List<DeserializationRule> UserDeserializationRules { get; } = new List<DeserializationRule>();

        public List<SerializationRule>   UserSerializationRules   { get; } = new List<SerializationRule>();

        public Type ContractInterfaceType { get; } = typeof(TContract);

        public Func<IChannel, TContract> OriginContractFactory { get; }

        internal PresentationBuilder()
        {
            OriginContractFactory = null;
        }
        internal PresentationBuilder(Func<IChannel, TContract> contractFactory)
        {
            OriginContractFactory = contractFactory ?? throw new ArgumentNullException(nameof(contractFactory));
        }
        
        public PresentationBuilder<TContract> UseReceiveDispatcher<TDispatcher>()
            where TDispatcher : IDispatcher, new()
        {
            return this.UseReceiveDispatcher(() => new TDispatcher());
        }

        public PresentationBuilder<TContract> SetMaxAnsTimeout(int delay)
        {
            _maxAnsDelay = delay;
            return this;
        }
        public PresentationBuilder<TContract> UseReceiveDispatcher(Func<IDispatcher> dispatcherFactory)
        {
            ReceiveDispatcherFactory = dispatcherFactory ?? throw new ArgumentNullException(nameof(dispatcherFactory));
            return this;
        }


        public PresentationBuilder<TContract> UseSerializer(SerializationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            UserSerializationRules.Add(rule);
            return this;
        }

        public PresentationBuilder<TContract> UseSerializer<TType, TSerializer>() where TSerializer: ISerializer, new ()
        {
            return UseSerializer(new SerializationRule((t)=> t== typeof(TType), (t,f)=>new TSerializer()));
        }

        public PresentationBuilder<TContract> UseDeserializer(DeserializationRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            UserDeserializationRules.Add(rule);
            return this;
        }

        public PresentationBuilder<TContract> UseDeserializer<TType, TDeserializer>() where TDeserializer: IDeserializer, new()
        {

            return UseDeserializer(new DeserializationRule((t) => t == typeof(TType), (t) => new TDeserializer()));
        }

        public PresentationBuilder<TContract> UseContractInitalization(Action<TContract, IChannel> initializer)
        {
            ContractInitializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
            return this;
        }
        public PresentationBuilder<TContract> UseContractFinalization(Action<TContract, IChannel, ErrorMessage> finalizer)
        {
            ContractFinalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
            return this;
        }
        public PresentationBuilder<TContract> UseContractFinalization(Action<TContract, IChannel> finalizer)
        {
            return UseContractFinalization((ch, co, ca) => finalizer(ch, co));
        }
        
        public ConnectionBuilder<TContract> UseChannel(IChannel theChannel) 
        {
            if (theChannel == null)
                throw new ArgumentNullException(nameof(theChannel));
            return UseChannel<IChannel>(() => theChannel);
        }

        public ConnectionBuilder<TContract> UseChannelAsync(Func<Task<IChannel>> asyncChannelFactory)
        {
            if (asyncChannelFactory == null)
                throw new ArgumentNullException(nameof(asyncChannelFactory));
            return new ConnectionBuilder<TContract>(this, asyncChannelFactory);
        }
        public ConnectionBuilder<TContract> UseChannel<TChannel>(Func<IChannel> channelFactory) 
        {
            if (channelFactory == null)
                throw new ArgumentNullException(nameof(channelFactory));
            return new ConnectionBuilder<TContract>(this, channelFactory);
        }

    }
}