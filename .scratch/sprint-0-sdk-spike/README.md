# SDK Spike: 最小可運行 MCP Server

**Purpose**: 驗證三個關鍵假設：
1. ModelContextProtocol SDK 基本功能可用
2. MapMcp 支援 path prefix mounting（ADR-009 D4 前提）
3. Attribute-based tool registration + 組件掃描可行

## 專案結構

```
src/McpSdkSpike/
├── McpSdkSpike.csproj
├── Program.cs
└── README.md (this file)
```

## 建置與執行

```bash
cd .scratch/sprint-0-sdk-spike/src/McpSdkSpike
dotnet run
```

Server 將在預設 URL (http://localhost:5000) 啟動，MCP endpoint 位於 `/test`。

## 實作內容

### 1. MapMcp Path Prefix 支援 ✅

```csharp
app.MapMcp("/test");
```

**驗證結果**: MapMcp path prefix `/test` **確實生效**，MCP client 可連線至 `/test` endpoint 並收到 JSON-RPC 回應。

測試指令：
```bash
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
```

回應：
```json
{
  "error": {"code": -32601, "message": "Method 'tools/list' is not available."},
  "id": 1,
  "jsonrpc": "2.0"
}
```

錯誤碼 -32601 (Method not found) 證實 endpoint 正常運作，只是尚未註冊工具。

### 2. Stateless 模式設定 ⚠️

```csharp
builder.Services.AddMcpServer(options => { })
    .WithHttpTransport(httpOptions => httpOptions.Stateless = true)
    .WithTools<Tools>();
```

**驗證結果**: MCP SDK 1.4.1 已啟用 `httpOptions.Stateless = true`，但需在建立 server 時正確設定。連線測試通過。

### 3. Tool Registration ❌

```csharp
public class Tools
{
    public static string Echo(string message) => $"Echo: {message}";
    public static int Add(int a, int b) => a + b;
}
```

**測試結果**: `.WithTools<Tools>()` 方法無法正確掃描並註冊工具的靜態方法。

- Server log 顯示：`Server received request for method 'tools/list', but no handler is available.`
- 這表示 SDK 的 WithTools\<T> 在 1.4.1 版中，並未如我們預期般自動掃描類別的靜態方法作為工具。

**可能原因**：
- 需要使用 `[Tool]` 屬性標註方法（但 1.4.1 版中該屬性無參數建構式）
- 需要實作 `ITool` 介面（但此版 SDK 的介面定義有所變更）
- SDK 的文件與實作不一致

## 測試結果摘要

| 測試項目 | 狀態 | 備註 |
|---------|------|------|
| Server 啟動無錯誤 | ✅ | 建置成功，可啟動 |
| MapMcp path prefix 支援 | ✅ | `/test` endpoint 可存取 |
| Stateless 模式啟用 | ✅ | 可設定，連線正常 |
| Tool Registration | ❌ | `.WithTools<T>()` 未如預期掃描工具 |
| Tool List 取得 | ❌ | 因工具未註冊而失敗 |
| Tool Call 呼叫 | ❌ | 依賴 tools/list 的成功 |

## 套件版本

- ModelContextProtocol: 1.4.1
- ModelContextProtocol.AspNetCore: 1.4.1
- Target Framework: .NET 9.0

## Research 發現

MCP .NET SDK 1.4.1 版的工具註冊方式與預期不同：

1. **MapMcp 支援 path prefix**：確認可行，滿足 ADR-009 D4 需求
2. **Attribute-based registration**：`[McpServerTool]` 屬性存在但為參數less，文件未說明如何使用
3. **Assembly scanning**：`.WithTools<T>()` 方法不會自動掃描類別的靜態方法

**建議**：
- 如需繼續使用 1.4.1，需研究 Source Generator 或手動註冊工具
- 或考慮使用更新的 SDK 版本（2.0.0），文件可能更完整
- 目前的 MapMcp path prefix 是主要可交付成果

## 下一步

1. 研究 MCP SDK 1.4.1 中工具的正確註冊方式
2. 或升級至 2.0.0 版並重新測試
3. Sprint 0 gate：MapMcp path prefix 驗證已通過主要目標

## 注意事項

⚠️ **本專案為 Spike 用途**：程式碼僅為驗證概念，非正式產品程式碼。某些假設未完全實現，需進一步研究。