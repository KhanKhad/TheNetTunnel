using System;
using TheNetTunnel.Exceptions.Local;

namespace TheNetTunnel.Exceptions.Remote
{
    /// <summary>
    /// Remote error occurred during call processing
    /// like serialization, 
    /// wrong contractImplementation, 
    /// user code exceptions etc...
    /// </summary>
    public abstract class RemoteException: TntCallException
    {
        protected RemoteException(
            ErrorType id,
            bool isFatal,
            short? messageId,
            int? askId,
            string message = null, 
            Exception innerException = null)
            :base(isFatal, messageId, askId,  $"[{id}]"+ (message??(" tnt call exception")), innerException)
        {
            Id = id;
        }

        public ErrorType Id { get; }

        public static RemoteException Create(ErrorType type, string additionalInfo, short? messageId,
            int? askId, bool isFatal = false)
        {
            switch (type)
            {
                case ErrorType.UnhandledUserExceptionError:
                    return new RemoteUnhandledException(messageId, askId, null, additionalInfo);
                case ErrorType.SerializationError:
                    return new RemoteSerializationException(messageId, askId, isFatal, additionalInfo);
                case ErrorType.ContractSignatureError:
                    return new RemoteContractImplementationException(messageId.Value, askId, isFatal, additionalInfo);
                case ErrorType.ConnectionAlreadyLost:
                    return new RemoteConnectionLostException(messageId, askId, additionalInfo);
                case ErrorType.HandshakeRejected:
                    return new RemoteHandshakeRejectedException(messageId, askId, additionalInfo);
                case ErrorType.ContractIdIsNotSupported:
                    return new RemoteContractIdIsNotSupported(messageId, askId, additionalInfo);
                default:
                    throw new InvalidOperationException(
                        $"Exception type {type} is unknown. Exception message: {additionalInfo}");
            }
        }
    }
}