# 04 — 稽核日誌 + PII 遮蔽

**What to build:** 每次工具呼叫自動寫入稽核日誌（含 Department, CoreVersion, CorrelationId 等欄位），參數中的 PII 欄位自動遮蔽（依 `Audit:PiiFields` 設定），原始值不進 sink。

**Blocked by:** 03-downstream-client（需 CorrelationId 貫穿）

**Status:** ready-for-agent

- [ ] 定義 `ToolInvocationAuditLog` record（Core spec §8）
- [ ] 欄位：Timestamp, AgentId, ToolName, ToolVersion, Department, CoreVersion, CorrelationId, Parameters, Success, HttpStatusCode, DurationMs, ErrorMessage
- [ ] 實作 `IAuditLogger` 介面
- [ ] 實作 PII 遮蔽邏輯：
  - 遞迴遍歷 Parameters object
  - 命中 `Audit:PiiFields` 設定的欄位名即遮蔽
  - 遮蔽規則：email → `c***@example.com`, 其他 → 保留前 2 字元 + `***`
- [ ] 遮蔽發生在**寫入前**（原始值不進 sink）
- [ ] Tool 執行前後自動記錄（middleware 或 decorator）
- [ ] 讀取 `Audit:Sink` 設定（ApplicationInsights / File）
- [ ] `Department` = gateway 部門（從 config）
- [ ] `CoreVersion` = `McpGateway.Core` assembly version
- [ ] 撰寫單元測試：PII 遮蔽正確、欄位完整、原始值未洩漏
- [ ] 撰寫整合測試：完整 tool 呼叫 → 稽核日誌寫入 → 驗證內容
