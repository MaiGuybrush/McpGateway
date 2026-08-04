# 手動測試 MCP Gateway 指南

## 前置條件

確保 Gateway 正在運行：

```bash
cd src/McpGateway.Report
dotnet run --urls "http://localhost:5100"
```

您應該看到：
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5100
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

## 測試步驟

### 1. 測試健康檢查端點

**測試就緒檢查**:

```bash
curl -s http://localhost:5100/health/ready
```

預期回應：
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0012039"
}
```

**測試存活檢查**:

```bash
curl -s http://localhost:5100/health/live
```

---

### 2. 測試 Metrics 端點

```bash
curl -s http://localhost:5100/metrics
```

應該看到 Prometheus 格式的 metrics，例如：
```
# HELP mcpgw_core_version MCP Gateway Core version information
mcpgw_core_version{dept="report",version="0.1.0.0"} 1
```

---

### 3. 測試 MCP SSE 端點

**連線到 SSE 端點**:

```bash
curl -N http://localhost:5100/mcp/sse
```

您應該看到 Server-Sent Events 流：
```
event: handshake
data: {"protocol": "2024-11-05"}

event: handshake
data: {"protocol": "2024-11-05", "capabilities": {"experimental": [], "roots": {"listChanged": false}}}

event: handshake
data: {}
```

按下 `Ctrl+C` 停止。

---

### 4. 取得工具列表

使用 JSON-RPC 取得可用工具：

```bash
curl -X POST http://localhost:5100/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/list",
    "id": 1
  }'
```

預期回應 (經過格式化)：
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "tools": [
      {
        "name": "query_wip",
        "description": "查詢在製品（WIP）報告，提供製造現場的生產進度、產量與良率資訊",
        "inputSchema": {
          "type": "object",
          "properties": {
            "workCenter": {
              "type": "string",
              "description": "工作中心編號（選填），例如: WC-1, WC-2 等",
              "default": null
            },
            "productLine": {
              "type": "string",
              "description": "產品線編號（選填），例如: LINE-1, LINE-2 等",
              "default": null
            },
            "startDate": {
              "type": "string",
              "description": "開始日期（選填），格式: YYYY-MM-DD",
              "default": null
            },
            "endDate": {
              "type": "string",
              "description": "結束日期（選填），格式: YYYY-MM-DD",
              "default": null
            }
          }
        }
      }
    ]
  }
}
```

---

### 5. 測試 query_wip 工具 (基本查詢)

**不帶參數的查詢**:

```bash
curl -X POST http://localhost:5100/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "query_wip",
      "arguments": {}
    },
    "id": 2
  }'
```

**預期回應**:
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "content": [
      {
        "type": "resource",
        "resource": {
          "json": {
            "totalItems": 5,
            "items": [
              {
                "workOrderId": "WO-1234",
                "productCode": "PROD-001",
                "productName": "Widget A",
                "workCenter": "WC-1",
                "productLine": "LINE-1",
                "quantityInProcess": 150,
                "quantityCompleted": 75,
                "status": "In Progress",
                "scheduledCompletion": "2024-01-05T10:00:00Z",
                "priority": "Normal",
                "yieldRate": 0.95
              }
              // ... more items
            ],
            "summary": {
              "totalQuantityInProcess": 1500,
              "totalQuantityCompleted": 750,
              "averageYieldRate": 0.85,
              "workCenters": 3,
              "productLines": 2
            },
            "generatedAt": "2024-01-01T09:00:00Z"
          }
        },
        "mimeType": "application/json",
        "text": "## WIP 在製品報告\n報告產生時間: 2024-01-01 09:00:00\n總項目數: 5\n\n### 摘要統計\n- 在製總數: 1,500\n- 完成總數: 750\n- 平均良率: 85.00%\n..."
      }
    ]
  }
}
```

---

### 6. 測試 query_wip 工具 (帶篩選條件)

**按工作中心篩選**:

```bash
curl -X POST http://localhost:5100/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "query_wip",
      "arguments": {
        "workCenter": "WC-1"
      }
    },
    "id": 3
  }'
```

**按產品線和日期範圍篩選**:

```bash
curl -X POST http://localhost:5100/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "query_wip",
      "arguments": {
        "productLine": "LINE-1",
        "startDate": "2024-01-01",
        "endDate": "2024-01-31"
      }
    },
    "id": 4
  }'
```

---

### 7. 測試錯誤處理

**嘗試調用不存在的工具**:

```bash
curl -X POST http://localhost:5100/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "nonexistent_tool",
      "arguments": {}
    },
    "id": 5
  }'
```

預期錯誤回應：
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "error": {
    "code": -32601,
    "message": "Method not found"
  }
}
```

---

### 8. 使用 Claude Desktop 測試 (進階)

編輯 Claude Desktop 設定檔 (`~/Library/Application Support/Claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "report-gateway": {
      "command": "dotnet",
      "args": ["run", "--project", "D:/Projects/.NET/McpGateway/src/McpGateway.Report/McpGateway.Report.csproj"],
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

重啟 Claude Desktop，然後在對話中詢問：

> "Get WIP report for work center WC-1"

Claude 應該會自動調用 `query_wip` 工具。

---

## 除錯技巧

### 檢查 Gateway 日誌

觀察執行中的 Gateway 輸出，查看：
- 工具調用請求
- 回應時間
- 錯誤訊息

### 測試下游 API

直接測試 Mock API：

```bash
# 啟動 Mock API
cd src/MockOcelotApi
dotnet run --urls "http://localhost:5000"

# 測試 WIP 端點
curl "http://localhost:5000/api/report/wip?workCenter=WC-1"
```

---

## 預期行為

1. **首次連線**: SSE 流會顯示握手訊息
2. **工具列表**: 應包含 `query_wip` 工具
3. **工具調用**: 
   - 無參數回傳所有模擬資料
   - 帶參數篩選回傳符合條件的資料
   - 錯誤參數回傳錯誤訊息
4. **效能**: 回應時間應 < 50ms

---

## 常見問題

**問題**: "Unable to connect to localhost:5100"

**解決**: 
- 確認 Gateway 正在運行
- 檢查防火牆設定
- 使用 `dotnet run --urls "http://localhost:5100" 明確指定 URL

**問題**: "Method not found" 錯誤

**解決**: 
- 確認工具名稱拼寫正確 (`query_wip`)
- 檢查工具是否已註冊 (呼叫 `tools/list`)

**問題**: 回應格式錯誤

**解決**: 
- 確認 Content-Type 為 `application/json`
- 確認 JSON-RPC 格式正確
- 檢查 JSON 語法有效性
