# 02 — 實作 query_wip_data 工具

**What to build:** `report_query_wip` 工具，呼叫下游 MFG Report API，支援 NTLM 認證，分組彙總 WIP 數據，回傳 JSON summary。

**Blocked by:** #01

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `src/McpGateway.Report/Tools/QueryWipTool.cs` 建立，繼承 `ToolBase<QueryWipInput, QueryWipOutput>`
- [ ] `[McpTool("report_query_wip")]` 標註
- [ ] Input schema 含欄位：`shop_id`, `group_by`, `product_filter`, `proc_filter`（參考 `tools.py:126-131`）
- [ ] 呼叫下游 API 使用 `IDownstreamClient`（或 `HttpClient` + NTLM auth）
- [ ] 分組邏輯實作（`PROC_ID`, `PRODUCT_ID`, `PRODUCT_ID+VER`, `OWNER_ID`）
- [ ] 回傳 summary 含 `total_lots`, `total_sheets`, `top_10_groups`
- [ ] 本地手動測試：MCP client 可呼叫並收到正確回應
- [ ] Description 清楚說明參數用途（語意封裝，非機械式）
