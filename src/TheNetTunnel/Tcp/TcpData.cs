namespace TheNetTunnel.Tcp
{
    public class TcpData
    {
        public TcpData() { }

        /// <summary>
        /// Backing buffer holding the received bytes. May be larger than
        /// <see cref="Length"/> and rented from the shared <c>ArrayPool</c>
        /// (see <see cref="Pooled"/>) — only the first <see cref="Length"/>
        /// bytes are valid.
        /// </summary>
        public byte[] Bytes { get; set; }

        /// <summary>Number of valid bytes in <see cref="Bytes"/>.</summary>
        public int Length { get; set; }

        /// <summary>
        /// True when <see cref="Bytes"/> was rented from <c>ArrayPool&lt;byte&gt;.Shared</c>
        /// and must be returned by the consumer once the payload has been copied out.
        /// </summary>
        public bool Pooled { get; set; }

        public object Sender { get; set; }
    }
}
