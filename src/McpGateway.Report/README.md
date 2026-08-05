# McpGateway.Report

部門專用的 MCP Gateway，提供製造現場 WIP (Work In Progress) 報告查詢功能。

## 功能

- **query_wip**: 查詢在製品報告，提供生產進度、產量與良率資訊

## 快速開始

### 前置條件

- .NET 9.0 SDK
- MCP Gateway Core 函式庫

### 安裝

```bash
cd src/McpGateway.Report
dotnet restore
dotnet build
```

### 設定

編輯 `appsettings.json`:

```json
{
  "McpGateway": {
    "Department": "report",
    "RoutePrefix": "/mcp",
    "EnableHealthChecks": true,
    "Auth": {
      "Provider": "JWT",
      "Enabled": true,
      "JwksEndpoint": "https://your-auth-server/.well-known/jwks.json",
      "JwksCacheHours": 24,
      "ApiKeyServiceUrl": "https://your-auth-server/api-key/validate",
      "ApiKeyTimeoutSeconds": 3
    },
    "TokenCache": {
      "Type": "Redis",
      "ConnectionString": "localhost:6379"
    }
  }
}
```

#### 認證提供者選項 (Auth Providers)

`McpGateway.Core` 支援以下幾種認證 Provider（設定於 `McpGateway.Auth.Provider`）：

| Provider | 說明 | 關鍵設定項目 / 環境變數 |
|---|---|---|
| **`JWT`** (預設) | Bearer JWT 認證，透過 JWKS 端點驗證公鑰並提取 User/Role Claims。 | `JwksEndpoint` (必要的 JWKS URL)<br>`JwksCacheHours` (公鑰快取小時數，預設 `24`) |
| **`API-KEY`** | 服務對服務認證，透過集中式 API-KEY 驗證服務檢查效力。 | `ApiKeyServiceUrl` (驗證服務 URL)<br>`ApiKeyTimeoutSeconds` (超時秒數，預設 `3`) |
| **`NTLM`** | Windows 整合認證，適用於存取不支援 JWT/API-KEY 的下游舊型系統。 | 系統帳號密碼由環境變數注入：<br>`NTLM_SERVICE_ACCOUNT`<br>`NTLM_SERVICE_PASSWORD` |
| **`None`** / **`Disabled`** | 停用認證 (設定 `"Enabled": false`)，適用於單機開發或測試。 | `"Enabled": false` |
### 運行

```bash
dotnet run
```

Gateway 將在 `http://localhost:5100` 啟動。

## API

### SSE 端點

`GET /mcp/sse` - 建立 SSE 連線以串流工具列表

### 工具調用

`POST /mcp` - 調用 MCP 工具

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
  "id": 1
}
```

## 工具清單

### query_wip

查詢在製品（WIP）報告，提供製造現場的生產進度、產量與良率資訊。

**參數**:

- `workCenter` (string, optional): 工作中心編號，例如: "WC-1"
- `productLine` (string, optional): 產品線編號，例如: "LINE-1"
- `startDate` (string, optional): 開始日期，格式: "YYYY-MM-DD"
- `endDate` (string, optional): 結束日期，格式: "YYYY-MM-DD"

**回傳**:

```csharp
public class QueryWipOutput
{
    public int TotalItems { get; set; }
    public List<WipItem> Items { get; set; }
    public WipSummary Summary { get; set; }
    public DateTime GeneratedAt { get; set; }
}
```

**範例請求**:

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
    "id": 1
  }'
```

## 測試

### 單元測試

```bash
cd tests/McpGateway.Report.IntegrationTests
dotnet test
```

### E2E 測試

```bash
cd tests/McpGateway.Report.E2ETests
dotnet test
```

### 效能測試

```bash
cd tests/k6
k6 run wip-perf-test.js
```

## 架構

```
┌─────────────────────────────────┐
│   Semantic Kernel / Pydantic AI │
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│  McpGateway.Report (Port 5100) │
│  - query_wip tool              │
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│  Downstream API (Mock)        │
│  - GET /api/report/wip         │
└─────────────────────────────────┘
```

## 效能目標

- **P95 延遲**: < 50ms
- **工具正確率**: 85%+
- **部署時間**: ≤ 1 天

## 授權

內部使用
