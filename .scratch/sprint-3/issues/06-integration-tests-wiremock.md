# 06 — 整合測試（WireMock）

**What to build:** 使用 WireMock.Net 模擬 Ocelot Gateway + 認證服務（JWKS endpoint / API-KEY validation service），覆蓋三條認證路徑（JWT / API-KEY / NTLM）+ 降級策略（認證服務逾時但 Redis 快取有值）。驗證 CorrelationId / Gateway-Department 標頭傳遞至下游。

**Blocked by:** #01 (CorrelationId), #04 (NTLM)

**Status:** ready-for-agent

- [ ] `tests/McpGateway.Core.IntegrationTests/` 專案含 `WireMock.Net` package
- [ ] `WireMockFixture.cs` 啟動 WireMock server（隨機 port），提供 stub builder helper
- [ ] Mock JWKS endpoint：回傳測試用 public key（對應測試 JWT token）
- [ ] Mock API-KEY validation service：`POST /api-key/validate` → 200 `{"valid":true,"userId":"test"}`
- [ ] Mock Ocelot downstream：`GET /api/orders/{id}` → 200，驗證收到 `X-Correlation-Id` + `X-Gateway-Department` + `X-User-Id` 標頭
- [ ] 測試案例：JWT 路徑 → 成功呼叫下游，稽核日誌含 `AuthType="JWT"`
- [ ] 測試案例：API-KEY 路徑 → 成功呼叫下游，稽核日誌含 `AuthType="API-KEY"`
- [ ] 測試案例：NTLM 路徑 → 下游回 401 Unauthorized → retry with credentials → 200
- [ ] 測試案例：API-KEY 服務逾時（延遲 5 秒）+ Redis 快取有值 → 使用快取成功，稽核含 warning
- [ ] 測試案例：JWKS 取得失敗（404）+ Redis 快取有 public key → 使用快取成功
- [ ] 驗證下游收到的 `X-Correlation-Id` 值與稽核日誌中一致
- [ ] 驗證下游收到 `X-Gateway-Department: report`
