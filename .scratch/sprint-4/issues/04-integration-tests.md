# 04 — 整合測試（WireMock + Gateway）

**What to build:** 端到端整合測試，啟動 WireMock + Report Gateway，驗證工具呼叫流程。

**Blocked by:** #02, #03

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `tests/McpGateway.IntegrationTests/ReportGatewayTests.cs` 建立
- [ ] `IntegrationTestFixture` 啟動 WireMock + Report Gateway（in-memory host）
- [ ] 測試案例 1：基本 query（`shop_id=CF3`, 無 filter）→ 驗證 summary 結構
- [ ] 測試案例 2：product filter（`product_filter=TJ6F`）→ 驗證過濾邏輯
- [ ] 測試案例 3：group_by 切換（`PROC_ID` vs `PRODUCT_ID`）→ 驗證分組正確
- [ ] 測試案例 4：錯誤處理（invalid shop → 404, timeout → error message）
- [ ] 測試案例 5：NTLM auth header 注入驗證（WireMock 檢查 `Authorization` header）
- [ ] 全部測試通過，覆蓋率 > 80%
