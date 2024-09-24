using System;

namespace TNT.Core.Exceptions.Remote
{
    public class RemoteUnhandledException : RemoteException
    {
        public RemoteUnhandledException(short? messageId, short? askId, Exception innerException, string message = null ) 
            :base(ErrorType.UnhandledUserExceptionError, false, messageId, askId,  message, innerException)
        {
        }
    }
}