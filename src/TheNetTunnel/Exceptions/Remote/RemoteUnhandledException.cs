using System;

namespace TheNetTunnel.Exceptions.Remote
{
    public class RemoteUnhandledException : RemoteException
    {
        public RemoteUnhandledException(short? messageId, int? askId, Exception innerException, string message = null ) 
            :base(ErrorType.UnhandledUserExceptionError, false, messageId, askId,  message, innerException)
        {
        }
    }
}