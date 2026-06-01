using System;

namespace TheNetTunnel.Exceptions.Local
{
    /// <summary>
    /// Thrown when an incoming frame declares a payload length that is negative
    /// or exceeds the configured maximum. Protects against malformed/malicious
    /// length prefixes that would otherwise trigger huge allocations.
    /// </summary>
    public class InvalidFrameLengthException : LocalException
    {
        public InvalidFrameLengthException(int declaredLength, int maxLength)
            : base(true, null, null,
                $"Incoming frame declared an invalid payload length {declaredLength} " +
                $"(allowed range is 0..{maxLength}).")
        {
            DeclaredLength = declaredLength;
            MaxLength = maxLength;
        }

        public int DeclaredLength { get; }
        public int MaxLength { get; }
    }
}
