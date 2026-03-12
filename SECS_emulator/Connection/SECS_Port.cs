using System;

namespace SECS_emulator.Connection
{
    /// <summary>
    /// 儲存 HSMS 連線設定資料。
    /// </summary>
    public class SECS_Port
    {
        /// <summary>設備 IP 位址。</summary>
        public string IP { get; set; }

        /// <summary>設備通訊埠。</summary>
        public int Port { get; set; }

        /// <summary>設備 Session ID（Device ID）。</summary>
        public ushort DeviceID { get; set; }

        /// <summary>
        /// true  = Active  模式（Host 主動連線 Equipment）
        /// false = Passive 模式（Host 被動等待 Equipment 連入）
        /// </summary>
        public bool IsActive { get; set; } = true;

        public override string ToString()
            => $"{(IsActive ? "Active" : "Passive")} {IP}:{Port} DeviceID={DeviceID}";
    }
}
