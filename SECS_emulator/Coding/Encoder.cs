using System;
using System.Collections.Generic;
using System.Text;
using SECS_emulator.Data;

namespace SECS_emulator.Coding
{
    /// <summary>
    /// 將 <see cref="SECSMessage"/> 物件編碼為 HSMS 二進位封包。
    /// 格式：[4-byte Length][10-byte Header][Body...]
    /// </summary>
    public static class Encoder
    {
        /// <summary>
        /// 把一則 SECS 訊息編碼成可透過 Socket 發送的位元組陣列。
        /// </summary>
        public static byte[] Encode(SECSMessage msg)
        {
            // 1. 先編碼 Body（只有 DataMessage 才有 Body）
            byte[] bodyBytes = Array.Empty<byte>();
            if (msg.SType == MessageType.DataMessage && msg.Body != null)
                bodyBytes = EncodeItem(msg.Body);

            // 2. 組裝 10-byte Header
            var headerBuf = new ByteBuffer();

            // Byte 0-1: Session ID (Device ID)
            headerBuf.WriteUInt16(msg.SessionId);

            // Byte 2: Header Byte 1
            //   bit7 = R-bit (reserved, 0)
            //   bit6-0 = Stream (for DataMessage) / 0 for control
            if (msg.SType == MessageType.DataMessage)
            {
                byte hb1 = (byte)(msg.Stream & 0x7F);
                if (msg.WBit) hb1 |= 0x80;          // W-bit 放在最高位
                headerBuf.WriteUInt8(hb1);
                // Byte 3: Function
                headerBuf.WriteUInt8(msg.Function);
            }
            else
            {
                // 控制訊息 Stream=0 / Function=0
                headerBuf.WriteUInt8(0x00);
                headerBuf.WriteUInt8(0x00);
            }

            // Byte 4: PType (Presentation Type, always 0)
            headerBuf.WriteUInt8(msg.PType);

            // Byte 5: SType (Session Type)
            headerBuf.WriteUInt8((byte)msg.SType);

            // Byte 6-9: System Bytes (唯一交易識別)
            headerBuf.WriteUInt32(msg.SystemBytes);

            byte[] header = headerBuf.ToArray(); // 10 bytes

            // 3. 計算並寫入長度前綴（Header + Body，不含自身的 4 bytes）
            uint totalLength = (uint)(header.Length + bodyBytes.Length); // 10 + N

            var packet = new ByteBuffer();
            packet.WriteUInt32(totalLength);
            packet.WriteBytes(header);
            packet.WriteBytes(bodyBytes);

            return packet.ToArray();
        }

        // ── SECS-II Item 編碼 ──────────────────────────────────────────────────

        private static byte[] EncodeItem(SECSItem item)
        {
            var buf = new ByteBuffer();

            if (item.Format == DataType.LIST)
            {
                var children = item.Items;
                // 先遞迴編碼所有子項目
                var childBytes = new List<byte[]>();
                int totalChildLen = 0;
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        byte[] cb = EncodeItem(child);
                        childBytes.Add(cb);
                        totalChildLen += cb.Length;
                    }
                }
                WriteFormatByte(buf, DataType.LIST, children?.Count ?? 0);
                foreach (var cb in childBytes)
                    buf.WriteBytes(cb);
            }
            else
            {
                byte[] valueBytes = EncodeValue(item);
                WriteFormatByte(buf, item.Format, valueBytes.Length);
                buf.WriteBytes(valueBytes);
            }

            return buf.ToArray();
        }

        /// <summary>
        /// 寫入 Format/Length Byte(s)：
        /// 高 6 bits = DataType code，低 2 bits = Length 欄位所佔的 byte 數。
        /// </summary>
        private static void WriteFormatByte(ByteBuffer buf, DataType fmt, int length)
        {
            byte formatCode = (byte)((byte)fmt << 2);          // 高 6 bits

            if (length <= 0xFF)
            {
                buf.WriteUInt8((byte)(formatCode | 0x01));      // 1 byte 長度
                buf.WriteUInt8((byte)length);
            }
            else if (length <= 0xFFFF)
            {
                buf.WriteUInt8((byte)(formatCode | 0x02));      // 2 byte 長度
                buf.WriteUInt8((byte)((length >> 8) & 0xFF));
                buf.WriteUInt8((byte)(length & 0xFF));
            }
            else
            {
                buf.WriteUInt8((byte)(formatCode | 0x03));      // 3 byte 長度
                buf.WriteUInt8((byte)((length >> 16) & 0xFF));
                buf.WriteUInt8((byte)((length >> 8) & 0xFF));
                buf.WriteUInt8((byte)(length & 0xFF));
            }
        }

        private static byte[] EncodeValue(SECSItem item)
        {
            var buf = new ByteBuffer();
            object val = item.Value;

            switch (item.Format)
            {
                case DataType.ASCII:
                    buf.WriteASCII(val?.ToString() ?? "");
                    break;

                case DataType.BINARY:
                    if (val is byte[] raw) buf.WriteBytes(raw);
                    else if (val is byte b) buf.WriteUInt8(b);
                    break;

                case DataType.BOOLEAN:
                    buf.WriteUInt8((bool)val ? (byte)0xFF : (byte)0x00);
                    break;

                // ── 整數（無號）
                case DataType.U1:
                    buf.WriteUInt8(Convert.ToByte(val));
                    break;
                case DataType.U2:
                    buf.WriteUInt16(Convert.ToUInt16(val));
                    break;
                case DataType.U4:
                    buf.WriteUInt32(Convert.ToUInt32(val));
                    break;
                case DataType.U8:
                    ulong u8 = Convert.ToUInt64(val);
                    buf.WriteBytes(BitConverter.GetBytes(u8));
                    break;

                // ── 整數（有號）
                case DataType.I1:
                    buf.WriteUInt8((byte)(sbyte)Convert.ToSByte(val));
                    break;
                case DataType.I2:
                    buf.WriteInt16(Convert.ToInt16(val));
                    break;
                case DataType.I4:
                    buf.WriteInt32(Convert.ToInt32(val));
                    break;
                case DataType.I8:
                    long i8 = Convert.ToInt64(val);
                    buf.WriteBytes(BitConverter.GetBytes(i8));
                    break;

                // ── 浮點數
                case DataType.F4:
                    byte[] f4 = BitConverter.GetBytes(Convert.ToSingle(val));
                    if (BitConverter.IsLittleEndian) Array.Reverse(f4);
                    buf.WriteBytes(f4);
                    break;
                case DataType.F8:
                    byte[] f8 = BitConverter.GetBytes(Convert.ToDouble(val));
                    if (BitConverter.IsLittleEndian) Array.Reverse(f8);
                    buf.WriteBytes(f8);
                    break;

                default:
                    Console.WriteLine($"[Encoder] 未支援的 DataType: {item.Format}");
                    break;
            }

            return buf.ToArray();
        }
    }
}
