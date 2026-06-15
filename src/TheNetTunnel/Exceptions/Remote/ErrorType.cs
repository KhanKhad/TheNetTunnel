namespace TheNetTunnel.Exceptions.Remote
{
    public enum ErrorType
    {
        UnhandledUserExceptionError    = 3,
        ContractIdIsNotSupported       = 4,
        SerializationError             = 5,
        ContractSignatureError         = 6,
        MaxNumberOfConnectionsExceeded = 7,
        ConnectionAlreadyLost          = 8,
        HandshakeRejected              = 9,
    }
}
