namespace TheNetTunnel.Exceptions.Remote
{
    /// <summary>
    /// Thrown when the HelloMessage handshake was rejected by the remote endpoint
    /// (e.g. incompatible version or connections limit reached).
    /// </summary>
    public class RemoteHandshakeRejectedException : RemoteException
    {
        public RemoteHandshakeRejectedException(short? messageId, int? askId, string message = null)
            : base(ErrorType.HandshakeRejected, isFatal: true, messageId, askId, message)
        {
        }
    }
}
