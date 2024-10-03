namespace TNT.Core.Exceptions.Remote
{
    public class RemoteContractImplementationException : RemoteException
    {
        public RemoteContractImplementationException(short messageId, int? askId, bool isFatal,   string message = null) 
            : base(ErrorType.ContractSignatureError, isFatal, messageId, askId, message)
        {
        }

    }
}