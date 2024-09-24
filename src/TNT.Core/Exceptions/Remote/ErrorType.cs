namespace TNT.Core.Exceptions.Remote
{
    public enum ErrorType
    {
        UnhandledUserExceptionError    = 3,
        SerializationError             = 5,
        ContractSignatureError         = 6,
        MaxNumberOfConnectionsExceeded = 7,
        ConnectionAlreadyLost          = 8,
    }
}
