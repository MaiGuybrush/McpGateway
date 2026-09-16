# [Research] 調研 ModelContextProtocol.AspNetCore 多端點工具隔離機制

- **ID**: `001`
- **Label**: `wayfinder:research`
- **Status**: `closed`
- **Assignee**: `MCP Protocol Researcher`
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None
- **Resolved**: 2026-09-16

## Question

`ModelContextProtocol.AspNetCore`（1.4.1）在同一個 ASP.NET Core WebApplication 進程中，如何支援多個端點（例如 `/mfg/mes/mcp` 與 `/mfg/wms/mcp`）分別掛載彼此隔離的 Tool 集合？官方是否支援 Named McpServer、Keyed Services、子路徑分組，還是需要由 `McpGateway.Core` 實作端點路由分流中介層？

---

## Resolution

### 1. 核心調查結果
1. **官方 SDK 現狀 (1.4.1)**：
   - 官方採用單一 Server 與全域工具聚合（Global Tool Aggregation）模式。
   - 尚未支援 Named Server 或 Keyed Services（社群 Issue #591, #612, #1202 仍為規劃中長程議題）。
2. **`MapMcp(path)` 的行為**：
   - 多次呼叫 `app.MapMcp(path)` 僅是為同一全域 Server 建立多個路由別名（Alias），底層共用同一個 `McpServerOptions`，所有工具會被全部混合暴露。
3. **無痛解法：Stateless 模式動態過濾**：
   - 在 `McpGateway.Core` 的 Stateless 模式下（`httpOptions.Stateless = true`），每個連入的 HTTP 請求皆會觸發 `HttpServerTransportOptions.ConfigureSessionOptions`。
   - 透過 `HttpContext.Request.Path` 解析子系統（如 `mes`、`wms`），在請求級別動態過濾 `mcpServerOptions.ToolCollection`。
   - 客戶端在 `tools/list` 與 `tools/call` 時皆嚴格受限於該子系統白名單，達成 100% 協議層隔離。

### 2. 核心技術模式
- **登錄中心**：引進 `IMcpSubsystemRegistry` 維護各子系統與 Tool 類型的對應白名單。
- **端點掛載**：`app.MapMcp("/{dept}/{system}/mcp")`。
- **動態過濾**：在 `ConfigureSessionOptions` 執行 `mcpOptions.ToolCollection = registry.FilterToolsForSubsystem(system, mcpOptions.ToolCollection)`。
