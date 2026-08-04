# 05 — 契約測試框架

**What to build:** 以最小部門專案（`McpGateway.Report` 含 1 支 stub tool）作為契約基準。Core 變更後，該專案必須能啟動並通過啟動驗證（6 項檢查）。測試執行於 CI，攔下破壞性變更（如新增強制驗證規則、移除公開 API）。

**Blocked by:** #01 — CorrelationId 等基礎功能需可驗證

**Status:** ready-for-agent

- [ ] `tests/McpGateway.Core.ContractTests/` 專案建立（xUnit + `Microsoft.AspNetCore.Mvc.Testing`）
- [ ] `Fixtures/MinimalDepartmentProject/` 子目錄含最小 `McpGateway.Report` 專案：
  - `Program.cs`（3 行標準寫法）
  - `Tools/ReportStubTool.cs`（1 支 `report_stub` tool，僅回傳固定字串）
  - `appsettings.json`（`Department="report"`, `RoutePrefix="/report"`）
- [ ] 契約測試 `CoreCompatibilityTests.cs`：
  - `WebApplicationFactory<Program>` 啟動 minimal project
  - 驗證啟動不拋例外（通過全部 6 項啟動驗證）
  - 呼叫 `report_stub` tool → 成功回傳預期結果
- [ ] CI pipeline（GitHub Actions / Azure DevOps）執行此測試，失敗阻斷 PR merge
- [ ] README 說明：此測試目的是防止 Core 破壞性變更，不測試功能正確性（功能由整合測試負責）
