# McpGateway.__Department__ 端點與工具文件

## 基本資訊
- **部門代碼**：`__department__`
- **服務連接埠**：`__Port__`
- **MCP 路由前綴**：`/mcp`
- **健康檢查端點**：
  - `/health/live` (Liveness)
  - `/health/ready` (Readiness)

## 工具清單 (Tools)

### 1. `__tool_name__`
- **說明**：查詢 __Department__ 現場之 __ToolClass__ 處理狀態。
- **輸入參數**：
  - `shop` (string): 廠別代碼 (例如 TFT1)
  - `queryId` (string): 查詢代碼
- **輸出型態**：`__ToolClass__ResultDto` (Structured JSON)

### 2. `hello`
- **說明**：連線測試與問候工具
- **輸入參數**：
  - `name` (string): 問候對象名稱
- **輸出型態**：`string`
