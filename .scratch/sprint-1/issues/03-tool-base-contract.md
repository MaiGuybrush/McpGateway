# 03 — Tool 基底契約與掃描

**What to build:** 部門專案可定義繼承 `ToolBase<TInput, TOutput>` 的工具類別，用 `[McpTool]` attribute 標註，Core 可掃描並註冊至 MCP server，`tools/list` 可回傳工具清單。

**Blocked by:** 01-host-bootstrap-minimal

**Status:** ready-for-agent

- [ ] 定義 `ToolBase<TInput, TOutput>` abstract class（Core spec §3.3）
- [ ] 定義 `McpToolAttribute` 含 `Name`, `Version`, `Deprecated`, `DeprecationReason`
- [ ] 定義 `ToolContext` record（UserId, Department, Role, TokenType, AgentId, CorrelationId）
- [ ] 實作 `AddToolsFromAssembly<TMarker>()` extension
- [ ] 掃描 assembly 找出所有 `[McpTool]` 標註類別
- [ ] 註冊至 MCP server（使用 SDK 的 `.WithTools<T>()` 或類似機制）
- [ ] `src/McpGateway/Tools/Manual/` 某一支工具改為繼承 `ToolBase<,>` 並標註 `[McpTool]`
- [ ] `tools/list` 回傳該工具（name, description, inputSchema）
- [ ] `tools/call` 可執行該工具並回傳結果（暫時無認證，ToolContext 填 dummy 值）
