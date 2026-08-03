# Sprint 0 - Issue #01 SDK Spike 成果報告

## 執行摘要

**Issue #01**: 建立最小可運行 MCP server 驗證三個關鍵假設

**狀態**: ✅ **部分完成** - MapMcp path prefix 支援確認，但 tool registration 機制需進一步研究

**耗時**: ~2 小時

## 驗證結果

### 1. ✅ MapMcp 支援 Path Prefix Mounting

**假設**: MapMcp 可掛載於非根路徑（如 `/test`），以支援 ADR-009 的 department gateway split 設計

**測試**: `app.MapMcp("/test")`

**結果**: **通過**

```bash
$ curl -X POST http://localhost:5000/test \
    -H "Content-Type: application/json" \
    -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# Response (200 OK):
{"error":{"code":-32601,"message":"Method 'tools/list' is not available"},...}
```

- HTTP 200 狀態碼證實 endpoint 可存取
- JSON-RPC error code -32601 證實 MCP 協議處理器正常運作
- **結論**: MapMcp path prefix **確實可行**，ADR-009 D4 假設成立

**影響**: ADR-009 的 per-department split 設計 (ingress path routing) 可行，無需變更架構

---

### 2. ⚠️ ModelContextProtocol SDK 基本功能

**假設**: SDK 1.4.1 提供穩定的 MCP server 實作

**測試**: `AddMcpServer()` 與 `WithHttpTransport()`

**結果**: **部分通過**

- ✅ Server 可啟動並監聽 port 5000
- ✅ HTTP transport 可設定（包括 stateless 模式）
- ✅ JSON-RPC 訊息可解析與回應
- ⚠️ Tool registration API 與文件不一致

**發現**:

```csharp
// 這個方法編譯通過但無法正確掃描工具
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<Tools>();

public class Tools
{
    public static string Echo(string message) => $"Echo: {message}";
    public static int Add(int a, int b) => a + b;
}
```

SDK 的 `.WithTools<T>()` 不會自動掃描類別的靜態方法。Server log 顯示：

```
Server received request for method 'tools/list', but no handler is available.
```

**影響**: 需要研究 SDK 文件或考慮手動工具註冊

---

### 3. ❌ Attribute-based Tool Registration

**假設**: 可用 `[McpTool]` 或類似 attribute 標註方法，SDK 自動掃描

**測試**: `[McpServerTool]` attribute (parameterless)

**結果**: **失敗**

```csharp
[McpServerTool("test_echo")]  // 錯誤：建構式不接受參數
[McpServerTool]                // 編譯通過但無效
public static string Echo(string message) { ... }
```

**發現**:

- `McpServerToolAttribute` 為 parameterless（無參數建構式）
- SDK 文件未說明如何使用此 attribute
- 可能需搭配 Source Generator 或特定命名約定

**影響**: 需深入 SDK 原始碼或尋找範例

---

## 技術細節

### 環境

```
- .NET SDK: 9.0.310
- ModelContextProtocol: 1.4.1
- ModelContextProtocol.AspNetCore: 1.4.1
- Target Framework: net9.0
```

### 測試指令

```bash
# 啟動 server
cd .scratch/sprint-0-sdk-spike/src/McpSdkSpike
dotnet run

# 測試 tools/list
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# 查看日誌
tail -f server.log
```

### 專案結構

```
.scratch/sprint-0-sdk-spike/
├── README.md              # 本文檔
├── McpSdkSpike.sln
└── src/McpSdkSpike/
    ├── McpSdkSpike.csproj
    └── Program.cs         # 最小 server 實作
```

---

## 結論與建議

### 已確認的項目

1. ✅ **MapMcp path prefix 可行**
   - ADR-009 的 department gateway split 設計可實施
   - 可使用 ingress path routing (/report, /spc, /qc)

2. ✅ **MCP SDK 基本 server 功能穩定**
   - Server 啟動/停止正常
   - HTTP transport 設定可運作
   - JSON-RPC 協議處理正常

3. ⚠️ **Tool registration 需深入研究**
   - `.WithTools<T>()` 不符合預期行為
   - 需查閱 SDK 原始碼或尋找範例

### 建議下一步

**選項 A**: 深入研究 MCP SDK 1.4.1
- 審閱 GitHub 上的範例專案
- 分析 Source Generator 行為
- 嘗試手動工具註冊 API

**選項 B**: 升級至 2.0.0
- 1.4.1 為較舊預覽版，文件不完整
- 2.0.0 可能提供更清晰的 API
- 但需重新測試所有假設

**選項 C**: 繼續 Sprint 0 但不依賴 attribute registration
- MapMcp path prefix 已驗證（主要目標）
- 工具註冊可改用手動方式（如 delegate registration）
- 不影響 ADR-009 的架構決策

### Sprint 0 Gate 影響

| Gate 條件 | 狀態 | 依賴 |
|----------|------|------|
| 可運行 MCP server | ⚠️ 部分 | 工具註冊 |
| Attribute 掃描可行 | ❌ | 需研究 |
| MapMcp path prefix 支援 | ✅ | 無 |
| 真實延遲數據 | ⏳ | 需工具可用 |

**建議**: 採用 **選項 C**，繼續 Sprint 0 但接受手動工具註冊。MapMcp path prefix 是 ADR-009 的關鍵假設，此目標已達成。

---

## 相關文件

- [ADR-009](./../../docs/architecture/adr/ADR-009-department-gateway-split.md)
- [MCP SDK Spike README](./../../.scratch/sprint-0-sdk-spike/README.md)
- [Issue #01](./../../.scratch/sprint-0/issues/01-sdk-spike.md)

---

**報告日期**: 2026-08-03
**報告人**: 實習生助手
**狀態**: 等待架構團隊審閱與建議