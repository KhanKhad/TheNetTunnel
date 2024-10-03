namespace TNT.Core.Exceptions.Remote
{
    public class RemoteSerializationException : RemoteException
    {
        public RemoteSerializationException(
            short? messageId, 
            int? askId = null, 
            bool isFatal = true, 
            string message = null) 
            : base(ErrorType.SerializationError, isFatal, messageId, askId, message)
        {
        }
    }
}