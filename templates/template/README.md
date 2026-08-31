# McpGateway.__Department__

McpGateway.__Department__ 是基於 `McpGateway.Core` 建置的標準 MCP Gateway 服務，負責提供 __Department__ 部門之 Model Context Protocol (MCP) 介面。

## 快速開始

### 1. 建置專案
```bash
dotnet restore
dotnet build
```

### 2. 本地啟動
```bash
dotnet run
```
服務預設監聽於 `http://localhost:__Port__`。

### 3. 健康檢查與 MCP 測試
- Liveness Check: `GET http://localhost:__Port__/health/live`
- Readiness Check: `GET http://localhost:__Port__/health/ready`
- MCP Endpoint: `POST http://localhost:__Port__/mcp`
