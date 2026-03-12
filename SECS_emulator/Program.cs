using System;
using System.Collections.Generic;
using SECS_emulator;
using SECS_emulator.Connection;
using SECS_emulator.Data;

class Program
{
    static void Main()
    {
        // ── 連線設定 ─────────────────────────────────────────────────────────
        var portConfig = new SECS_Port
        {
            IP = "127.0.0.1",
            Port = 5000,
            DeviceID = 0,
            IsActive = true     // true = 主動連線（Host）；false = 被動監聽
        };

        // ── 建立客戶端並訂閱訊息事件 ─────────────────────────────────────────
        var client = new SECSClient(portConfig);

        client.MessageReceived += OnMessageReceived;

        // ── 連線（HSMS Active/Passive 握手會自動進行） ───────────────────────
        client.Connect();

        // ── 互動式命令列，保持程式運行並允許手動發送 S1F1 ────────────────────
        Console.WriteLine("\n指令：[s1f1] 發送 S1F1  |  [lt] Linktest  |  [q] 結束\n");
        while (true)
        {
            string input = Console.ReadLine()?.Trim().ToLower();
            switch (input)
            {
                case "s1f1":
                    // S1F1 Are You There (W-bit=true，設備應回 S1F2)
                    client.Send(new SECSMessage
                    {
                        SessionId = portConfig.DeviceID,
                        SType = MessageType.DataMessage,
                        Stream = 1,
                        Function = 1,
                        WBit = true
                    });
                    break;

                case "lt":
                    client.SendLinktestReq();
                    break;

                case "q":
                    client.Disconnect();
                    return;

                default:
                    if (!string.IsNullOrEmpty(input))
                        Console.WriteLine("未知指令。可用：s1f1 | lt | q");
                    break;
            }
        }
    }

    /// <summary>處理來自設備的 SECS 資料訊息。</summary>
    private static void OnMessageReceived(SECSMessage msg)
    {
        Console.WriteLine($"[APP] 收到資料訊息: S{msg.Stream}F{msg.Function}");
        // TODO: 在這裡加入業務邏輯，例如回覆 S1F2、S2F18 等
    }
}
