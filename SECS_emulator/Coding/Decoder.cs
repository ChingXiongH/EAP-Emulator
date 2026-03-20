using System;
using System.Collections.Generic;
using System.Text;
using SECS_emulator.Data;

namespace SECS_emulator.Coding
{
    /// <summary>
    /// 將 HSMS 二進位封包解碼為 <see cref="SECSMessage"/> 物件。
    /// 格式：[4-byte Length][10-byte Header][Body...]
    /// </summary>
    public static class Decoder
    {
        /// <summary>HSMS Header 固定 10 bytes。</summary>
        public const int Header_Len = 10;

        /// <summary>
        /// 解碼一個完整的 HSMS 封包（含前 4-byte Length 欄位）。
        /// </summary>
        /// <param name="raw">完整封包位元組（Length + Header + Body）</param>
        public static SECSMessage Decode(byte[] raw)
        {
            var buf = new ByteBuffer(raw);

            // ── 讀取 Length（4 bytes, BigEndian） ────────────────────────────
            uint length = buf.ReadUInt32();   // Header + Body 的總長度

            if (raw.Length < 4 + length)
                throw new ArgumentException($"[Decoder] 封包長度不足: 預期 {4 + length} bytes，實際 {raw.Length} bytes");

            // ── 讀取 10-byte Header ────────────────────────────────────────────
            ushort sessionId = buf.ReadUInt16();  // Byte 0-1

            byte headerByte1 = buf.ReadUInt8();   // Byte 2
            byte headerByte2 = buf.ReadUInt8();   // Byte 3
            byte pType = buf.ReadUInt8();   // Byte 4
            byte sTypeByte = buf.ReadUInt8();   // Byte 5
            uint systemBytes = buf.ReadUInt32();  // Byte 6-9

            // 解析 Stream / Function / WBit（僅 DataMessage 有意義）
            byte stream = (byte)(headerByte1 & 0x7F);
            bool wbit = (headerByte1 & 0x80) != 0;
            byte function = headerByte2;
            StreamFunction streamFunction = (StreamFunction)(stream * 10000 + function * 10 + (wbit ? 1 : 0));

            MessageType sType = Enum.IsDefined(typeof(MessageType), sTypeByte)
                ? (MessageType)sTypeByte
                : MessageType.DataMessage;

            // ── 解碼 Body（只有 DataMessage 才有） ────────────────────────────
            SECSItem body = null;
            int bodyLen = (int)length - Header_Len;

            if (sType == MessageType.DataMessage && bodyLen > 0)
            {
                body = DecodeItem(buf);
            }

            return new SECSMessage
            {
                SessionId = sessionId,
                Stream = stream,
                Function = function,
                WBit = wbit,
                PType = pType,
                SType = sType,
                SystemBytes = systemBytes,
                Body = body
            };
        }

        // ── SECS-II Item 遞迴解碼 ──────────────────────────────────────────────

        private static SECSItem DecodeItem(ByteBuffer buf)
        {
            // Format Byte：高 6 bits = DataType，低 2 bits = 長度欄位佔幾 bytes
            byte formatByte = buf.ReadUInt8();
            int lenBytesCnt = formatByte & 0x03;
            byte dataTypeCode = (byte)(formatByte >> 2);

            DataType fmt = Enum.IsDefined(typeof(DataType), (int)dataTypeCode)
                ? (DataType)dataTypeCode
                : DataType.UNKNOWN;

            // 讀取長度
            int itemLength = 0;
            for (int i = 0; i < lenBytesCnt; i++)
                itemLength = (itemLength << 8) | buf.ReadUInt8();

            // LIST → 遞迴
            if (fmt == DataType.LIST)
            {
                var children = new List<SECSItem>(itemLength);
                for (int i = 0; i < itemLength; i++)
                    children.Add(DecodeItem(buf));
                return new SECSItem(DataType.LIST, children);
            }

            // 非 LIST → 讀值
            byte[] valueBytes = buf.ReadBytes(itemLength);
            object value = DecodeValue(fmt, valueBytes);
            return new SECSItem(fmt, value);
        }

        private static object DecodeValue(DataType fmt, byte[] bytes)
        {
            switch (fmt)
            {
                case DataType.ASCII:
                    return Encoding.ASCII.GetString(bytes);

                case DataType.BINARY:
                    return bytes;

                case DataType.BOOLEAN:
                    return bytes.Length > 0 && bytes[0] != 0x00;

                // ── 無號整數
                case DataType.U1:
                    return bytes[0];
                case DataType.U2:
                    return (ushort)((bytes[0] << 8) | bytes[1]);
                case DataType.U4:
                    return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
                case DataType.U8:
                    return BitConverter.ToUInt64(SwapIfLE(bytes, 8), 0);

                // ── 有號整數
                case DataType.I1:
                    return (sbyte)bytes[0];
                case DataType.I2:
                    return (short)((bytes[0] << 8) | bytes[1]);
                case DataType.I4:
                    return (int)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
                case DataType.I8:
                    return BitConverter.ToInt64(SwapIfLE(bytes, 8), 0);

                // ── 浮點數
                case DataType.F4:
                    return BitConverter.ToSingle(SwapIfLE(bytes, 4), 0);
                case DataType.F8:
                    return BitConverter.ToDouble(SwapIfLE(bytes, 8), 0);

                default:
                    Console.WriteLine($"[Decoder] 未支援的 DataType: {fmt}，以原始 byte[] 回傳");
                    return bytes;
            }
        }

        /// <summary>若本機為 Little-Endian，翻轉位元組陣列以符合 BigEndian 格式。</summary>
        private static byte[] SwapIfLE(byte[] src, int count)
        {
            byte[] buf = new byte[count];
            Array.Copy(src, buf, Math.Min(count, src.Length));
            if (BitConverter.IsLittleEndian) Array.Reverse(buf);
            return buf;
        }

        // ── 除錯輔助 ──────────────────────────────────────────────────────────

        /// <summary>將 SECSMessage 以人類可讀格式印到 Console。</summary>
        public static void Print(SECSMessage msg)
        {
            if (msg.SType == MessageType.DataMessage)
                Console.WriteLine($"[MSG] S{msg.Stream}F{msg.Function} W={msg.WBit} SysBytes=0x{msg.SystemBytes:X8}");
            else
                Console.WriteLine($"[MSG] Control: {msg.SType} SysBytes=0x{msg.SystemBytes:X8}");

            if (msg.Body != null)
                PrintItem(msg.Body, 0);
        }

        private static void PrintItem(SECSItem item, int indent)
        {
            string pad = new string(' ', indent * 2);
            if (item.Format == DataType.LIST)
            {
                Console.WriteLine($"{pad}<L [{item.Items?.Count ?? 0}]");
                if (item.Items != null)
                    foreach (var child in item.Items)
                        PrintItem(child, indent + 1);
                Console.WriteLine(">.");
            }
            else
            {
                Console.WriteLine($"{pad}<{item.Format} {item.Value}>");
            }
        }
    }
}
