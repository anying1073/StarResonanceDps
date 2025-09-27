namespace StarResonanceDpsAnalysis.WinForm
{
    public sealed class ByteReader
    {
        private readonly byte[] _buffer;
        private int _offset;

        public ByteReader(byte[] buffer, int offset = 0)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _offset = offset;
        }
        public bool TryPeekUInt32BE(out uint value)
        {
            if (Remaining < 4) { value = 0; return false; }
            value = (uint)(_buffer[_offset] << 24 |
                           _buffer[_offset + 1] << 16 |
                           _buffer[_offset + 2] << 8 |
                           _buffer[_offset + 3]);
            return true;
        }

        public int Remaining => _buffer.Length - _offset;

        // ===== UInt64 (大端) =====
        public ulong ReadUInt64BE()
        {
            if (Remaining < 8) throw new EndOfStreamException();
            ulong value =
                ((ulong)_buffer[_offset] << 56) |
                ((ulong)_buffer[_offset + 1] << 48) |
                ((ulong)_buffer[_offset + 2] << 40) |
                ((ulong)_buffer[_offset + 3] << 32) |
                ((ulong)_buffer[_offset + 4] << 24) |
                ((ulong)_buffer[_offset + 5] << 16) |
                ((ulong)_buffer[_offset + 6] << 8) |
                _buffer[_offset + 7];
            _offset += 8;
            return value;
        }

        public ulong PeekUInt64BE()
        {
            if (Remaining < 8) throw new EndOfStreamException();
            return
                ((ulong)_buffer[_offset] << 56) |
                ((ulong)_buffer[_offset + 1] << 48) |
                ((ulong)_buffer[_offset + 2] << 40) |
                ((ulong)_buffer[_offset + 3] << 32) |
                ((ulong)_buffer[_offset + 4] << 24) |
                ((ulong)_buffer[_offset + 5] << 16) |
                ((ulong)_buffer[_offset + 6] << 8) |
                _buffer[_offset + 7];
        }

        // ===== UInt32 (大端) =====
        public uint ReadUInt32BE()
        {
            if (Remaining < 4) throw new EndOfStreamException();
            uint value =
                (uint)(_buffer[_offset] << 24 |
                       _buffer[_offset + 1] << 16 |
                       _buffer[_offset + 2] << 8 |
                       _buffer[_offset + 3]);
            _offset += 4;
            return value;
        }

        public uint PeekUInt32BE()
        {
            if (Remaining < 4) throw new EndOfStreamException();
            return (uint)(_buffer[_offset] << 24 |
                          _buffer[_offset + 1] << 16 |
                          _buffer[_offset + 2] << 8 |
                          _buffer[_offset + 3]);
        }

        // ===== UInt16 (大端) =====
        public ushort ReadUInt16BE()
        {
            if (Remaining < 2) throw new EndOfStreamException();
            ushort value = (ushort)(_buffer[_offset] << 8 | _buffer[_offset + 1]);
            _offset += 2;
            return value;
        }

        public ushort PeekUInt16BE()
        {
            if (Remaining < 2) throw new EndOfStreamException();
            return (ushort)(_buffer[_offset] << 8 | _buffer[_offset + 1]);
        }

        // ===== Bytes =====
        public byte[] ReadBytes(int length)
        {
            if (length < 0 || Remaining < length) throw new EndOfStreamException();
            var result = new byte[length];
            Buffer.BlockCopy(_buffer, _offset, result, 0, length);
            _offset += length;
            return result;
        }

        public byte[] PeekBytes(int length)
        {
            if (length < 0 || Remaining < length) throw new EndOfStreamException();
            var result = new byte[length];
            Buffer.BlockCopy(_buffer, _offset, result, 0, length);
            return result;
        }

        public byte[] ReadRemaining()
        {
            return ReadBytes(Remaining);
        }

    }

}
