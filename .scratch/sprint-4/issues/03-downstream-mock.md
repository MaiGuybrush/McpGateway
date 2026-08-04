# 03 — 下游 API Mock (WireMock)

**What to build:** WireMock stub 模擬 MFG Report WIP API，回傳測試數據，支援不同 `shop_id` 場景。

**Blocked by:** None — 可與 #02 平行

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `tests/McpGateway.IntegrationTests/Mocks/WipApiMock.cs` 建立 WireMock mapping
- [ ] 支援路徑：`/api/wip/{shop_id}`
- [ ] 模擬 3 種 shop_id 回應：`CF3`, `ARY7`, `TFT7`（各 20-50 筆測試數據）
- [ ] 測試數據含必要欄位：`PROC_ID`, `PRODUCT_ID`, `PRODUCT_VER`, `OWNER_ID`, `LOT_CNT`, `SHEET_CNT`, `PANEL_CNT`, `STAY_HRS`
- [ ] 模擬錯誤場景：404 (invalid shop), 500 (server error), timeout
- [ ] WireMock server 可獨立啟動，port 8888
