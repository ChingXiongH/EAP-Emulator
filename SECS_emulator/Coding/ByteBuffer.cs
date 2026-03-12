using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SECS_emulator.Coding
{
    /// <summary>
    /// 提供 BigEndian 優先的二進位讀寫工具，用於組裝與解析 HSMS 封包。
    /// HSMS 協定使用 Big-Endian（網路位元組序）。
    /// </summary>
    public class ByteBuffer
    {
        private readonly MemoryStream _stream;
        private readonly BinaryWriter _writer;
        private readonly BinaryReader _reader;

        // ── 建構子 ────────────────────────────────────────────────────────────

        /// <summary>建立空白緩衝區（用於寫入 / 編碼）。</summary>
        public ByteBuffer()
        {
            _stream = new MemoryStream();
            _writer = new BinaryWriter(_stream);
            _reader = new BinaryReader(_stream);
        }

        /// <summary>從現有位元組陣列建立緩衝區（用於讀取 / 解碼）。</summary>
        public ByteBuffer(byte[] buffer)
        {
            _stream = new MemoryStream(buffer);
            _writer = new BinaryWriter(_stream);
            _reader = new BinaryReader(_stream);
        }

        // ── 位置控制 ──────────────────────────────────────────────────────────

        /// <summary>目前讀寫指標位置（byte offset）。</summary>
        public int Offset => (int)_stream.Position;

        /// <summary>緩衝區總長度。</summary>
        public int Length => (int)_stream.Length;

        /// <summary>剩餘可讀位元組數。</summary>
        public int Remaining => Length - Offset;

        public void SetOffset(int offset) => _stream.Position = offset;

        /// <summary>取出全部內容為位元組陣列。</summary>
        public byte[] ToArray() => _stream.ToArray();

        // ── 寫入方法（BigEndian）──────────────────────────────────────────────

        public void WriteUInt8(byte value)
            => _writer.Write(value);

        public void WriteUInt16(ushort value)
        {
            _writer.Write((byte)(value >> 8));
            _writer.Write((byte)(value & 0xFF));
        }

        public void WriteUInt32(uint value)
        {
            _writer.Write((byte)((value >> 24) & 0xFF));
            _writer.Write((byte)((value >> 16) & 0xFF));
            _writer.Write((byte)((value >> 8) & 0xFF));
            _writer.Write((byte)(value & 0xFF));
        }

        public void WriteInt16(short value) => WriteUInt16((ushort)value);
        public void WriteInt32(int value) => WriteUInt32((uint)value);

        public void WriteBytes(byte[] data) => _writer.Write(data);

        public void WriteASCII(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
            _writer.Write(bytes);
        }

        // ── 讀取方法（BigEndian）──────────────────────────────────────────────

        public byte ReadUInt8() => _reader.ReadByte();

        public ushort ReadUInt16()
        {
            byte hi = _reader.ReadByte();
            byte lo = _reader.ReadByte();
            return (ushort)((hi << 8) | lo);
        }

        public uint ReadUInt32()
        {
            byte b0 = _reader.ReadByte();
            byte b1 = _reader.ReadByte();
            byte b2 = _reader.ReadByte();
            byte b3 = _reader.ReadByte();
            return (uint)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
        }

        public short ReadInt16() => (short)ReadUInt16();
        public int ReadInt32() => (int)ReadUInt32();

        public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

        public string ReadASCII(int count)
        {
            byte[] bytes = _reader.ReadBytes(count);
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
