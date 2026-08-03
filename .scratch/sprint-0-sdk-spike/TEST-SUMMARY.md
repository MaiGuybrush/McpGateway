# SDK Spike 測試總覽

## 快速測試指令

```bash
# 啟動 server（背景）
cd .scratch/sprint-0-sdk-spike/src/McpSdkSpike
nohup dotnet run > server.log 2>&1 &

# 測試 tools/list
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# 預期回應（工具尚未註冊）:
# HTTP 200 OK
# {"error":{"code":-32601,"message":"Method 'tools/list' is not available."},...}

# 查看 server 日誌
tail -f server.log
```

## 已驗證的功能

### ✅ 1. Server 啟動與執行

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**證據**: Server 日誌顯示成功啟動

### ✅ 2. MapMcp Path Prefix

```
Request: POST http://localhost:5000/test
Response: 200 OK (JSON-RPC error -32601)
```

**證據**: HTTP 200 與 JSON-RPC 解析證實 `/test` endpoint 可存取

### ✅ 3. JSON-RPC Protocol 處理

```
Request: {"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}
Response: {"error":{"code":-32601,...}}
```

**證據**: 收到結構化 JSON-RPC 回應，證實協議處理器運作中

### ❌ 4. Tool Registration

```
Server log: "Server received request for method 'tools/list', but no handler is available."
```

**發現**: `.WithTools<Tools>()` 未自動註冊工具的靜態方法

## 測試覆蓋率

| 測試項目 | 覆蓋 | 結果 |
|---------|------|------|
| Server 啟動 | ✅ | 通過 |
| HTTP Transport | ✅ | 通過 |
| JSON-RPC 解析 | ✅ | 通過 |
| MapMcp prefix | ✅ | 通過 |
| Tool 註冊 | ❌ | 失敗 |
| tools/list | ⏸️ | 依賴工具註冊 |
| tools/call | ⏸️ | 依賴工具註冊 |

## 檔案清單

```
.
├── README.md              # spike 詳細文件
├── RESULTS.md             # 完整測試報告
├── McpSdkSpike.sln        # solution 檔案
└── src/
    └── McpSdkSpike/
        ├── McpSdkSpike.csproj   # 專案設定
        ├── Program.cs           # 主要程式碼 (57 行)
        └── server.log           # 執行日誌
```

**總行數**: ~120 行程式碼（含文件）

## 關鍵程式碼片段

```csharp
// MapMcp path prefix 設定
app.MapMcp("/test");

// Stateless 模式啟用
builder.Services.AddMcpServer()
    .WithHttpTransport(httpOptions => httpOptions.Stateless = true)
    .WithTools<Tools>();

// Tool 類別（未被正確掃描）
public class Tools
{
    public static string Echo(string message) => $"Echo: {message}";
    public static int Add(int a, int b) => a + b;
}
```

## 下一步建議

1. **研究 SDK**: 查閱 MCP C# SDK GitHub 原始碼
2. **參考範例**: 尋找使用範例專案
3. **手動註冊**: 如需要，改用手動工具註冊 API
4. **考慮升級**: 評估是否升級至 2.0.0 版

## 命令速查

```bash
# 建置
dotnet build

# 執行
dotnet run

# 測試
curl -X POST http://localhost:5000/test \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# 檢查日誌
tail -f server.log | grep -E "tools/list|MCP|Error"
```

---

**最後更新**: 2026-08-03
**測試環境**: .NET 9.0, ModelContextProtocol 1.4.1