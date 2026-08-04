# MCP 端點文件

## 標準 MCP 端點格式

根據 `ModelContextProtocol.AspNetCore` 套件實現，標準端點如下：

### 1. SSE 端點（Server-Sent Events）

```
GET http://localhost:5100/sse
```

**注意**：在某些配置下，可能需要使用根路徑：
```
GET http://localhost:5100/
```

SSE 端點用於：
- 建立長連線以接收伺服器推送的事件
- 接收工具、資源和提示的變更通知
- 保持與 MCP 客戶端的持續連線

### 2. JSON-RPC 端點

```
POST http://localhost:5100/
Content-Type: application/json
```

**重要**：
- 使用根路徑 `/`
- **不是** `/mcp` 或 `/message`
- 請求和回應都使用相同的端點
- 回應以 SSE (text/event-stream) 格式返回

## 支援的 JSON-RPC 方法

### `tools/list` - 取得可用工具列表

**請求**：
```json
{
  "jsonrpc": "2.0",
  "method": "tools/list",
  "id": 1
}
```

**回應**：
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "tools": [
      {
        "name": "query_wip",
        "description": "查詢在製品（WIP）報告...",
        "inputSchema": {
          "type": "object",
          "properties": {
            "workCenter": { "type": "string", "description": "工作中心編號", "default": null },
            "productLine": { "type": "string", "description": "產品線編號", "default": null },
            "startDate": { "type": "string", "description": "開始日期", "default": null },
            "endDate": { "type": "string", "description": "結束日期", "default": null }
          }
        }
      }
    ]
  }
}
```

### `tools/call` - 執行工具

**請求**：
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "query_wip",
    "arguments": {
      "workCenter": "WC-1",
      "productLine": "LINE-1"
    }
  },
  "id": 2
}
```

**回應**：
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "content": [
      {
        "type": "resource",
        "resource": {
          "json": { ... }
        },
        "mimeType": "application/json",
        "text": "## WIP 在製品報告\n..."
      }
    ]
  }
}
```

## Client 配置範例

### Claude Desktop

編輯 `~/Library/Application Support/Claude/claude_desktop_config.json`:

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

### Cursor

編輯 `.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "report-gateway": {
      "url": "http://localhost:5100/"
    }
  }
}
```

### Python MCP Client

```python
from mcp import Client

client = Client("http://localhost:5100")

# 取得工具列表
tools = client.list_tools()

# 執行工具
result = client.call_tool("query_wip", {
    "workCenter": "WC-1"
})
```

### Postman / curl

**取得工具列表**：
```bash
curl -X POST http://localhost:5100/ \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

**執行工具**：
```bash
curl -X POST http://localhost:5100/ \
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
    "id": 2
  }'
```

## 回應格式

### 成功回應
- HTTP 狀態碼：`200 OK`
- Content-Type：`text/event-stream`
- 回應格式：SSE (Server-Sent Event)

### 錯誤回應
- HTTP 狀態碼：`4xx` 或 `5xx`
- 可能的錯誤：
  - `404 Not Found` - Endpoint 不存在或拼寫錯誤
  - `405 Method Not Allowed` - 使用錯誤的 HTTP 方法
  - `400 Bad Request` - JSON 格式錯誤
  - `-32601` (JSON-RPC 錯誤碼) - 方法不存在

## 除錯技巧

### 檢查 Gateway 正在運行
```bash
curl http://localhost:5100/health/ready
```

### 檢查 Metrics
```bash
curl http://localhost:5100/metrics
```

### 檢視日誌
啟動 Gateway 時不使用背景執行：
```bash
dotnet run --urls "http://localhost:5100"
```

## 參考文件

- [MCP 協議規範](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [JSON-RPC 2.0 規範](https://www.jsonrpc.org/specification)
