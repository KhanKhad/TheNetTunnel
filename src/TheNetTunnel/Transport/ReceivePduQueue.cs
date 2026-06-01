using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using TheNetTunnel.Exceptions.Local;
using TheNetTunnel.Presentation;

namespace TheNetTunnel.Transport
{
    public class ReceivePduQueue
    {
        private const int LengthHeaderSize = sizeof(int);

        /// <summary>
        /// Default upper bound for a single frame payload (64 MB). Frames declaring
        /// a larger (or negative) length are rejected before any allocation happens.
        /// </summary>
        public const int DefaultMaxFrameLength = 64 * 1024 * 1024;

        private readonly int _maxFrameLength;

        private readonly Queue<PooledMemoryStream> _completedPackets = new();

        private PooledMemoryStream _currentPacket;
        private int _remainingPayloadBytes;

        private readonly byte[] _partialHeader = new byte[LengthHeaderSize];
        private int _partialHeaderBytes;

        public bool IsEmpty => _completedPackets.Count == 0;

        public ReceivePduQueue(int maxFrameLength = DefaultMaxFrameLength)
        {
            if (maxFrameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength));

            _maxFrameLength = maxFrameLength;
        }

        public void Enqueue(byte[] data) => Enqueue(data.AsSpan());

        public void Enqueue(ReadOnlySpan<byte> data)
        {
            while (!data.IsEmpty)
            {
                if (_currentPacket == null && !TryStartNewPacket(ref data))
                    return;

                AppendToCurrentPacket(ref data);
            }
        }

        public Stream DequeueOrNull() => _completedPackets.Count > 0 ? _completedPackets.Dequeue() : null;

        private bool TryStartNewPacket(ref ReadOnlySpan<byte> data)
        {
            if (_partialHeaderBytes == 0 && data.Length >= LengthHeaderSize)
            {
                StartPacket(BinaryPrimitives.ReadInt32LittleEndian(data));
                data = data.Slice(LengthHeaderSize);
                return true;
            }

            var needed = LengthHeaderSize - _partialHeaderBytes;
            var available = data.Length < needed ? data.Length : needed;
            data.Slice(0, available).CopyTo(_partialHeader.AsSpan(_partialHeaderBytes));
            _partialHeaderBytes += available;
            data = data.Slice(available);

            if (_partialHeaderBytes < LengthHeaderSize)
                return false;

            StartPacket(BinaryPrimitives.ReadInt32LittleEndian(_partialHeader));
            _partialHeaderBytes = 0;
            return true;
        }

        private void StartPacket(int payloadLength)
        {
            if (payloadLength < 0 || payloadLength > _maxFrameLength)
                throw new InvalidFrameLengthException(payloadLength, _maxFrameLength);

            _remainingPayloadBytes = payloadLength;
            _currentPacket = new PooledMemoryStream(payloadLength > 0 ? payloadLength : 1);
        }

        private void AppendToCurrentPacket(ref ReadOnlySpan<byte> data)
        {
            var toCopy = data.Length < _remainingPayloadBytes ? data.Length : _remainingPayloadBytes;
            _currentPacket.Write(data.Slice(0, toCopy));
            _remainingPayloadBytes -= toCopy;
            data = data.Slice(toCopy);

            if (_remainingPayloadBytes == 0)
            {
                _currentPacket.Position = 0;
                _completedPackets.Enqueue(_currentPacket);
                _currentPacket = null;
            }
        }
    }
}
