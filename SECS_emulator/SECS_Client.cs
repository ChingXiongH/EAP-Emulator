using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SECS_emulator.Coding;
using SECS_emulator.Connection;
using SECS_emulator.Data;

namespace SECS_emulator
{
    /// <summary>
    /// HSMS 通訊客戶端。
    /// 支援 Active（主動連線）與 Passive（被動監聽）兩種模式。
    /// 連線建立後自動完成 HSMS Select 握手，並啟動背景接收迴圈。
    /// </summary>
    public class SECSClient
    {
        // ── 設定 ───────────────────────────────────────────────────────────────
        private readonly SECS_Port _port;

        // ── Socket ────────────────────────────────────────────────────────────
        private Socket _socket;          // 已連線的通訊 Socket
        private Socket _listenSocket;    // Passive 模式專用的監聽 Socket

        // ── 狀態 ───────────────────────────────────────────────────────────────
        private bool _connected = false;
        private uint _systemByteCounter = 1;  // 每次發送遞增，作為唯一交易號碼

        // ── 事件 ───────────────────────────────────────────────────────────────
        /// <summary>成功解碼一則 SECS 訊息時觸發。</summary>
        public event Action<SECSMessage> MessageReceived;

        // ── 建構子 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 以 <see cref="SECS_Port"/> 設定物件建立 SECSClient。
        /// </summary>
        public SECSClient(SECS_Port port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        /// <summary>方便的快速建構子。</summary>
        public SECSClient(string ip, int port, ushort deviceId, bool isActive = true)
            : this(new SECS_Port { IP = ip, Port = port, DeviceID = deviceId, IsActive = isActive })
        { }

        // ── 連線 ───────────────────────────────────────────────────────────────

        /// <summary>
        /// 依 <see cref="SECS_Port.IsActive"/> 選擇主動或被動連線。
        /// 連線成功後自動送出 Select.req 並啟動背景接收迴圈。
        /// </summary>
        public void Connect()
        {
            if (_connected) return;

            try
            {
                if (_port.IsActive)
                    ConnectActive();
                else
                    ConnectPassive();

                _connected = true;

                // 啟動背景接收執行緒
                var recvThread = new Thread(() => ReceiveLoop(_socket))
                {
                    IsBackground = true,
                    Name = "SECS-Recv"
                };
                recvThread.Start();

                // Active 模式：主動發出 Select.req 握手
                if (_port.IsActive)
                    SendSelectReq();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Connect 失敗: {ex.Message}");
            }
        }

        private void ConnectActive()
        {
            Console.WriteLine($"[Active] 連線至 {_port.IP}:{_port.Port} ...");
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(_port.IP, _port.Port);
            Console.WriteLine($"[Active] 已連線到 {_port.IP}:{_port.Port}");
        }

        private void ConnectPassive()
        {
            Console.WriteLine($"[Passive] 監聽 {_port.IP}:{_port.Port} ...");
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(new IPEndPoint(IPAddress.Parse(_port.IP), _port.Port));
            _listenSocket.Listen(1);

            Console.WriteLine("[Passive] 等待對方連入...");
            _socket = _listenSocket.Accept();   // 阻塞直到對方連入
            _listenSocket.Close();
            Console.WriteLine("[Passive] Connect Success");
        }

        // ── 斷線 ───────────────────────────────────────────────────────────────

        public void Disconnect()
        {
            try
            {
                SendSeparateReq();
                _socket?.Close();
                _connected = false;
                Console.WriteLine("[INFO] 已斷開連線");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Disconnect: {ex.Message}");
            }
        }

        // ── 發送 API ───────────────────────────────────────────────────────────

        /// <summary>發送一則 SECS 訊息（自動填入 SystemBytes）。</summary>
        public void Send(SECSMessage msg)
        {
            if (!_connected)
            {
                Console.WriteLine("[WARN] 尚未連線，無法發送");
                return;
            }

            msg.SystemBytes = _systemByteCounter++;
            byte[] packet = Encoder.Encode(msg);

            try
            {
                _socket.Send(packet);
                Console.WriteLine($"[SEND] {DescribeMsg(msg)} ({packet.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Send: {ex.Message}");
            }
        }

        // ── HSMS 控制訊息 ──────────────────────────────────────────────────────

        /// <summary>發送 HSMS Select.req（連線握手請求）。</summary>
        public void SendSelectReq()
        {
            Console.WriteLine("[HSMS] → Select.req");
            Send(new SECSMessage
            {
                SessionId = _port.DeviceID,
                SType = MessageType.SelectReq
            });
        }

        /// <summary>發送 HSMS Select.rsp（收到 Select.req 後回覆）。</summary>
        private void SendSelectRsp(uint systemBytes)
        {
            Console.WriteLine("[HSMS] → Select.rsp");
            var rsp = new SECSMessage
            {
                SessionId = _port.DeviceID,
                SType = MessageType.SelectRsp,
                SystemBytes = systemBytes   // 與 req 相同的 System Bytes
            };
            byte[] packet = Encoder.Encode(rsp);
            _socket.Send(packet);
        }

        /// <summary>發送 HSMS Linktest.req。</summary>
        public void SendLinktestReq()
        {
            Console.WriteLine("[HSMS] → Linktest.req");
            Send(new SECSMessage
            {
                SessionId = _port.DeviceID,
                SType = MessageType.LinktestReq
            });
        }

        /// <summary>發送 HSMS Linktest.rsp。</summary>
        private void SendLinktestRsp(uint systemBytes)
        {
            Console.WriteLine("[HSMS] → Linktest.rsp");
            var rsp = new SECSMessage
            {
                SessionId = _port.DeviceID,
                SType = MessageType.LinktestRsp,
                SystemBytes = systemBytes
            };
            byte[] packet = Encoder.Encode(rsp);
            _socket.Send(packet);
        }

        /// <summary>發送 HSMS Separate.req（正常結束連線）。</summary>
        private void SendSeparateReq()
        {
            try
            {
                Console.WriteLine("[HSMS] → Separate.req");
                byte[] packet = Encoder.Encode(new SECSMessage
                {
                    SessionId = _port.DeviceID,
                    SType = MessageType.SeparateReq
                });
                _socket.Send(packet);
            }
            catch { /* 斷線時忽略錯誤 */ }
        }

        // ── 接收迴圈 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 背景執行緒：持續從 socket 讀取完整的 HSMS 封包並分派處理。
        /// HSMS 封包格式：前 4 bytes 為 BigEndian 的封包長度（不含自身），
        /// 後面接 Length bytes 的內容（10-byte Header + Body）。
        /// </summary>
        private void ReceiveLoop(Socket socket)
        {
            Console.WriteLine("[RECV] 接收迴圈已啟動");

            byte[] lengthBuf = new byte[4];

            while (_connected)
            {
                try
                {
                    // 1. 讀取 4-byte 長度前綴
                    if (!ReceiveExact(socket, lengthBuf, 4))
                        break;

                    uint bodyLen = (uint)((lengthBuf[0] << 24) | (lengthBuf[1] << 16)
                                       | (lengthBuf[2] << 8) | lengthBuf[3]);

                    if (bodyLen == 0 || bodyLen > 64 * 1024)
                    {
                        Console.WriteLine($"[WARN] 異常封包長度 {bodyLen}，略過");
                        continue;
                    }

                    // 2. 讀取封包主體
                    byte[] bodyBuf = new byte[bodyLen];
                    if (!ReceiveExact(socket, bodyBuf, (int)bodyLen))
                        break;

                    // 3. 組合完整封包（Length + Body）供 Decoder 解析
                    byte[] fullPacket = new byte[4 + bodyLen];
                    Buffer.BlockCopy(lengthBuf, 0, fullPacket, 0, 4);
                    Buffer.BlockCopy(bodyBuf, 0, fullPacket, 4, (int)bodyLen);

                    // 4. 解碼並分派
                    SECSMessage msg = Decoder.Decode(fullPacket);
                    StreamFunction streamFunction = (StreamFunction)(msg.Stream * 10000 + msg.Function * 10 + (msg.WBit ? 1 : 0));
                    DispatchMessage(msg);
                    SECSMsgHandler(streamFunction, msg);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset
                                               || ex.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    Console.WriteLine("[INFO] 對方已斷線");
                    break;
                }
                catch (Exception ex)
                {
                    if (_connected)
                        Console.WriteLine($"[ERROR] ReceiveLoop: {ex.Message} {ex.StackTrace}");
                    break;
                }
            }

            _connected = false;
            Console.WriteLine("[RECV] 接收迴圈已結束");
        }

        /// <summary>確保從 socket 讀滿 count bytes（處理 TCP 分段接收）。</summary>
        private static bool ReceiveExact(Socket socket, byte[] buf, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int received = socket.Receive(buf, offset, count - offset, SocketFlags.None);
                if (received == 0) return false;   // 對方關閉連線
                offset += received;
            }
            return true;
        }

        // ── 訊息分派 ───────────────────────────────────────────────────────────

        public void SECSMsgHandler(StreamFunction streamFunction, SECSMessage msg)
        {
            switch (streamFunction)
            {
                case StreamFunction.S1F13W:
                    ReciveS1F13W(msg);
                    break;

            }
        }
        private void DispatchMessage(SECSMessage msg)
        {
            Decoder.Print(msg);   // 印出可讀格式

            switch (msg.SType)
            {
                case MessageType.SelectReq:
                    // Passive 端或對方重新握手
                    Console.WriteLine("[HSMS] ← Select.req → 回覆 Select.rsp");
                    SendSelectRsp(msg.SystemBytes);
                    break;

                case MessageType.SelectRsp:
                    Console.WriteLine("[HSMS] ← Select.rsp（握手完成，通訊建立）");
                    break;

                case MessageType.LinktestReq:
                    Console.WriteLine("[HSMS] ← Linktest.req → 回覆 Linktest.rsp");
                    SendLinktestRsp(msg.SystemBytes);
                    break;

                case MessageType.LinktestRsp:
                    Console.WriteLine("[HSMS] ← Linktest.rsp（Linktest OK）");
                    break;

                case MessageType.SeparateReq:
                    Console.WriteLine("[HSMS] ← Separate.req（對方要求斷線）");
                    _connected = false;
                    _socket?.Close();
                    break;

                case MessageType.DataMessage:
                    Console.WriteLine($"[DATA] S{msg.Stream}F{msg.Function} W={msg.WBit}");
                    MessageReceived?.Invoke(msg);
                    break;

                default:
                    Console.WriteLine($"[WARN] 未知 SType: {msg.SType}");
                    break;
            }
        }

        // ── 輔助 ───────────────────────────────────────────────────────────────

        private static string DescribeMsg(SECSMessage msg)
            => msg.SType == MessageType.DataMessage
                ? $"S{msg.Stream}F{msg.Function} W={msg.WBit}"
                : msg.SType.ToString();

        public void ReciveS1F13W(SECSMessage msg)
        {
            Console.WriteLine("[HSMS] → Reply S1F14");
            // var sendS1F14 = new SECSMessage
            // {
            //     Stream = msg.Stream,
            //     Function = 14,
            //     SessionId = _port.DeviceID,
            //     SType = MessageType.DataMessage,
            //     SystemBytes = msg.SystemBytes,
            //     Body = null   // S1F14: Establish Communications Acknowledge (空回覆)
            // };
            // var HACK = new SECSItem(DataType.BINARY, new byte[] { 0X00 });
            // var timeList = new SECSItem(DataType.LIST, new List<SECSItem>());

            // sendS1F14.Body = new SECSItem(DataType.LIST, new List<SECSItem>
            // {
            //     HACK,
            //     timeList
            // });

            StreamFunctionBase streamFunctionBase = new StreamFunctionBase();
            SECSMessage sendS1F14 = streamFunctionBase.Create_S1F14(msg, msg.SystemBytes);

            byte[] packet = Encoder.Encode(sendS1F14);
            _socket.Send(packet);
        }
    }
}
