# [Task] 在 McpGateway.Core 實作模組分流中介層 (IMcpSubsystemRegistry)

- **ID**: `006`
- **Label**: `wayfinder:task`
- **Status**: `open`
- **Assignee**: Unassigned
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None (Ticket 004 resolved)

## Question

依據 ADR-014 規範，在 `McpGateway.Core` 專案中新增 `IMcpSubsystemRegistry`、實作 `AddMcpSubsystem` Fluent API，並於 `McpGatewayHostExtensions.AddMcpGateway` 的 `ConfigureSessionOptions` 回呼中掛接路徑正則解析與 `mcpOptions.ToolCollection` 動態白名單過濾機制，附帶單元與整合測試驗證。
