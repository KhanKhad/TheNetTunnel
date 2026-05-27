namespace TheNetTunnel.Exceptions.Remote
{
    /// <summary>
    /// Thrown when the connection was lost on the remote side
    /// (e.g. ping heartbeat failure or an explicit DisconnectMessage was received).
    /// </summary>
    public class RemoteConnectionLostException : RemoteException
    {
        public RemoteConnectionLostException(short? messageId, int? askId, string message = null)
            : base(ErrorType.ConnectionAlreadyLost, isFatal: true, messageId, askId, message)
        {
        }
    }
}
