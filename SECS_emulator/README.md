# SECS_Test — HSMS 通訊測試工具

## 專案概述

以 **C# / .NET 8** 開發的 SECS/GEM HSMS（SEMI E37）點對點通訊測試工具。
透過 TCP/IP Socket 與設備進行二進位 HSMS 封包通訊，支援收發 SECS-II 格式的資料訊息。

- **模式**：預設 Active（Host 主動連線）；可切換 Passive（等待設備連入）
- **握手**：自動完成 Select.req / Select.rsp，並自動回應 Linktest.req
- **解碼**：支援 SECS-II 巢狀 LIST 結構與所有基本資料型別

---

## 專案結構

```
SECS_Test/
├── Program.cs                   ← 執行進入點（連線設定 + 互動 CLI）
├── SECS_Client.cs               ← 核心通訊客戶端
├── Connection/
│   ├── SECS_Port.cs             ← 連線設定資料類別
│   └── Connect.cs               ← 空殼（職責已整合至 SECS_Client）
├── Coding/
│   ├── ByteBuffer.cs            ← BigEndian 二進位讀寫工具
│   ├── Encoder.cs               ← SECSMessage → HSMS byte[] 編碼器
│   └── Decoder.cs               ← HSMS byte[] → SECSMessage 解碼器
└── Data/
    └── S_Data.cs                ← 資料模型（SECSMessage、SECSItem、DataType、MessageType）
```

---

## 各檔案說明

### `Program.cs` — 執行進入點
- 建立 `SECS_Port` 設定物件（IP / Port / DeviceID / IsActive）
- 建立 `SECSClient`，訂閱 `MessageReceived` 事件
- 呼叫 `client.Connect()` 啟動通訊
- 互動 CLI 指令：
  | 指令 | 動作 |
  |------|------|
  | `s1f1` | 發送 S1F1 Are You There（W-bit=true） |
  | `lt`   | 發送 Linktest.req |
  | `q`    | Separate.req 後結束程式 |

### `SECS_Client.cs` — 核心通訊客戶端
| 方法 / 成員 | 說明 |
|---|---|
| `Connect()` | 依 IsActive 選擇主動/被動連線，連線後啟動接收執行緒並自動送 Select.req |
| `Send(SECSMessage)` | 透過 Encoder 序列化後發送，自動填入 SystemBytes |
| `SendSelectReq()` | 公開 API，手動觸發 HSMS 握手 |
| `SendLinktestReq()` | 公開 API，手動發送 Linktest |
| `Disconnect()` | 送 Separate.req 後關閉 Socket |
| `MessageReceived` event | 收到 DataMessage 時觸發，供外部訂閱業務邏輯 |
| `ReceiveLoop()` | 背景執行緒，`ReceiveExact()` 保證讀滿 TCP 分段 |
| `DispatchMessage()` | 依 SType 自動回應控制訊息（Select/Linktest/Separate） |

**注意**：`SECS_Client` 同時只運作一種模式（Active 或 Passive）。
雙向互通是透過同一條 TCP 連線的全雙工特性實現，不需要同時開兩個 Socket。

### `Connection/SECS_Port.cs` — 連線設定
```csharp
new SECS_Port {
    IP       = "127.0.0.1",
    Port     = 5000,
    DeviceID = 0,
    IsActive = true   // false = Passive（等待設備連入）
}
```

### `Coding/ByteBuffer.cs` — 二進位工具
- **BigEndian** 讀寫（HSMS 協定規定網路位元組序）
- 寫入：`WriteUInt8 / WriteUInt16 / WriteUInt32 / WriteBytes / WriteASCII`
- 讀取：`ReadUInt8 / ReadUInt16 / ReadUInt32 / ReadBytes / ReadASCII`
- 屬性：`Offset`、`Length`、`Remaining`

### `Coding/Encoder.cs` — 編碼器
HSMS 封包格式：`[4-byte Length][10-byte Header][SECS-II Body...]`

- `Encode(SECSMessage)` → `byte[]`
- Header：SessionId、Stream、Function、WBit、PType、SType、SystemBytes
- Body：`EncodeItem()` 遞迴編碼 LIST / 純值（所有 DataType 均支援）

### `Coding/Decoder.cs` — 解碼器
- `Decode(byte[])` → `SECSMessage`
- `DecodeItem()` 遞迴解析 SECS-II 巢狀 LIST 與所有基本型別
- `Print(SECSMessage)` 除錯用，印出完整訊息結構到 Console

### `Data/S_Data.cs` — 資料模型
| 型別 | 說明 |
|---|---|
| `SECSMessage` | SessionId / Stream / Function / WBit / PType / SType / SystemBytes / Body |
| `SECSItem` | Format (DataType) + Value (object) + Items (List\<SECSItem\>) |
| `MessageType` | DataMessage=0, SelectReq=1, SelectRsp=2, LinktestReq=5, LinktestRsp=6, SeparateReq=9 |
| `DataType` | LIST, BINARY, BOOLEAN, ASCII, I1~I8, U1~U8, F4, F8 |

---

## 快速啟動

```powershell
cd c:\TEST\SECS_Test\SECS_Test
dotnet build
dotnet run
```

**Active 模式（預設）**：目標設備需先在 `127.0.0.1:5000` 上等待連線。

**自測（本機雙程序）**：
1. 視窗 A → 改 `IsActive = false` → `dotnet run`（被動等待）
2. 視窗 B → 保持 `IsActive = true` → `dotnet run`（主動連入）

---

## 下次會話快速指引

> **背景**：通訊骨架已完整，目前可以：做 HSMS 握手、發送任意 SxFy、解碼 SECS-II Body。

**待擴充的方向（依優先序）**：
1. **在 `OnMessageReceived` 加入業務回覆邏輯**（例如收到 S1F1 → 回 S1F2）
2. **Linktest 定時器**：在 `Connect()` 後啟動 `Timer`，每 T8 秒自動呼叫 `SendLinktestReq()`
3. **斷線重連**：`ReceiveLoop` 結束後，若 `IsActive` 可自動重試 `Connect()`
4. **訊息佇列**：將 `MessageReceived` 改為生產者/消費者佇列（`Channel<SECSMessage>`），避免接收執行緒被業務邏輯阻塞

---

## HSMS 封包格式備忘

```
Byte 0-3  : Length (BigEndian) — Header + Body 的總長度（不含自身 4 bytes）
Byte 4-5  : Session ID (Device ID)
Byte 6    : Header Byte 1 → bit7=W-bit, bit6-0=Stream  (DataMessage) / 0 (Control)
Byte 7    : Header Byte 2 → Function                   (DataMessage) / 0 (Control)
Byte 8    : PType = 0
Byte 9    : SType (MessageType enum)
Byte 10-13: System Bytes (交易唯一識別，BigEndian uint)
Byte 14+  : SECS-II Body（只有 DataMessage 才有）
```

**SECS-II Item 格式**：
```
[Format Byte: 高6bit=DataType, 低2bit=長度欄位佔幾bytes]
[Length Byte(s): 值的長度]
[Value Bytes...]
```
