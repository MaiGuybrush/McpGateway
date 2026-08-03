# 01 — Host bootstrap 最小可運行版本

**What to build:** 部門專案引用 Core package 後，3 行 Program.cs 即可啟動 MCP server（無認證、無工具，僅驗證 host 流程）。Server 可啟動並回應 health check。

**Blocked by:** None — 可立即開始

**Status:** ready-for-agent

- [ ] 實作 `AddMcpGateway(builder)` extension method
- [ ] 讀取 `appsettings.json` 的 `McpGateway` section
- [ ] 註冊 MCP server services（使用 ModelContextProtocol SDK）
- [ ] 實作 `RunMcpGatewayAsync(app)` extension method
- [ ] 掛載 `/health/live` 與 `/health/ready` endpoints
- [ ] 掛載 MCP endpoint 於 `RoutePrefix`（從 config 讀取）
- [ ] `src/McpGateway/Program.cs` 3 行版本可啟動無錯誤
- [ ] curl health check 回傳 200 OK
- [ ] curl MCP endpoint 回傳 JSON-RPC 回應（tools/list 空清單可接受）
