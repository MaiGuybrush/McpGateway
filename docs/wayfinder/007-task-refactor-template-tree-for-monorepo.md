# [Task] 重構 templates/ 範本樹為 Modular Monorepo 結構

- **ID**: `007`
- **Label**: `wayfinder:task`
- **Status**: `open`
- **Assignee**: Unassigned
- **Parent**: [Map: 部門內多系統模組化與端點分流架構](file:///D:/Projects/.NET/McpGateway.Core/docs/wayfinder/map.md)
- **Blocked by**: None (Ticket 004 resolved)

## Question

依據 ADR-014 規範，將 `McpGateway.Core/templates/` 模板樹重構為分層結構：
1. `templates/template-host/`：包含薄宿主 Web 專案、包含 `// __SUBSYSTEM_REGISTRATION__` 錨點之 `Program.cs`、分層 `appsettings.json` 與 `McpGateway.__Department__.Host.csproj`。
2. `templates/template-module/`：包含子系統 Class Library 模板、`__System__ModuleExtensions.cs`、三段式 MCP Tool 類別與 `McpGateway.__Department__.__System__.csproj`。
3. 升級現有 `scaffold.ps1`，使其預設產出 `src/McpGateway.<Dept>.Host/` 與 Solution 檔案。
