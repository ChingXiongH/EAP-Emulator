using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SECS_emulator.Data
{
    public enum MessageType : byte
    {
        DataMessage = 0,
        SelectReq = 1,
        SelectRsp = 2,
        DeselectReq = 3,
        DeselectRsp = 4,
        LinktestReq = 5,
        LinktestRsp = 6,
        RejectReq = 7,
        SeparateReq = 9
    }

    public class SECSMessage
    {
        public ushort SessionId { get; set; }
        public byte Stream { get; set; }
        public byte Function { get; set; }
        public bool WBit { get; set; }
        public byte PType { get; set; } = 0;
        public MessageType SType { get; set; } = MessageType.DataMessage;
        public uint SystemBytes { get; set; }
        public SECSItem Body { get; set; }
    }

    public class SECSItem
    {
        public DataType Format { get; set; }
        public object Value { get; set; }

        public SECSItem(DataType format, object value)
        {
            Format = format;
            Value = value;
        }

        public List<SECSItem> Items => Value as List<SECSItem>;
    }
    public enum DataType
    {
        LIST = 0,
        BINARY = 8,
        BOOLEAN = 9,
        ASCII = 16,
        JIS8 = 17,
        I8 = 24,
        I1 = 25,
        I2 = 26,
        I4 = 28,
        F8 = 32,
        F4 = 36,
        U8 = 40,
        U1 = 41,
        U2 = 42,
        U4 = 44,
        UNKNOWN = 255
    }
    public enum StreamFunction
    {
        S1F1W = 10011,
        S1F2 = 10020,
        S1F3W = 10031,
        S1F4 = 10040,
        S1F11W = 10111,
        S1F12 = 10120,
        S1F13W = 10131,
        S1F14 = 10140,
        S1F15W = 10151,
        S1F16 = 10160,
        S1F17W = 10171,
        S1F18 = 10180,
        S1F21W = 10211,
        S1F22 = 10220,
        S1F23W = 10231,
        S1F24 = 10240,
        S2F13W = 20131,
        S2F14 = 20140,
        S2F15W = 20151,
        S2F16 = 20160,
        S2F17W = 20171,
        S2F18 = 20180,
        S2F23W = 20231,
        S2F24 = 20240,
        S2F25W = 20251,
        S2F26 = 20260,
        S2F29W = 20291,
        S2F30 = 20300,
        S2F31W = 20311,
        S2F32 = 20320,
        S2F33W = 20331,
        S2F34 = 20340,
        S2F35W = 20351,
        S2F36 = 20360,
        S2F37W = 20371,
        S2F38 = 20380,
        S2F41W = 20411,
        S2F42 = 20420,
        S3F17W = 30171,
        S3F18 = 30180,
        S3F27W = 30271,
        S3F28 = 30280,
        S5F1W = 50011,
        S5F2 = 50020,
        S5F3W = 50031,
        S5F4 = 50040,
        S5F5W = 50051,
        S5F6 = 50060,
        S5F7W = 50071,
        S5F8 = 50080,
        S6F1 = 60010,
        S6F2W = 60002,
        S6F11W = 60111,
        S6F12 = 60120,
        S6F15W = 60151,
        S6F16 = 60160,
        S6F19W = 60191,
        S6F20 = 60200,
        S7F1W = 70011,
        S7F2 = 70020,
        S7F3W = 70031,
        S7F4 = 70040,
        S7F5W = 70051,
        S7F6 = 70060,
        S7F17W = 70171,
        S7F18 = 70180,
        S7F19W = 70191,
        S7F20 = 70200,
        S7F23W = 70231,
        S7F24 = 70240,
        S7F25W = 70251,
        S7F26 = 70260,
        S9F1 = 90010,
        S9F3 = 90030,
        S9F5 = 90050,
        S9F7 = 90070,
        S9F9 = 90090,
        S9F11 = 90110,
        S10F1W = 100011,
        S10F2 = 100020,
        S10F3W = 100031,
        S10F4 = 100040,
        S10F5W = 100051,
        S10F6 = 100060,
        S14F9W = 140091,
        S14F10 = 140100,
        S16F5W = 160051,
        S16F6 = 160060,
        S16F11W = 160111,
        S16F12 = 160120,
        S16F15W = 160151,
        S16F16 = 160160,
        S16F17W = 160171,
        S16F18 = 160180,
        S16F19W = 160191,
        S16F20 = 160200,
        S16F21W = 160211,
        S16F22 = 160220,
        S16F27W = 160271,
        S16F28 = 160280
    }
}
