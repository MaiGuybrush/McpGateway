# 02 — 認證降級策略（5 種情境）

**What to build:** 認證服務異常時（逾時、5xx、Redis 不可用），系統依 Core spec §6.4 降級策略處理：有 cache 則用 cache + warning，無 cache 則回 503。所有 5 種情境有整合測試覆蓋。

**Blocked by:** 01-apikey-client

**Status:** ready-for-agent

- [ ] **情境 1**: API-KEY 服務逾時，cache 有值 → 使用 cache + warning log
- [ ] **情境 2**: API-KEY 服務逾時，cache 無值 → 回 503
- [ ] **情境 3**: JWKS 取得失敗，有快取金鑰 → 使用快取金鑰 + warning log
- [ ] **情境 4**: JWKS 取得失敗，無快取金鑰 → 回 503
- [ ] **情境 5**: Redis 不可用 → 降級為直接呼叫認證服務（不快取）+ warning log
- [ ] 實作 fallback 邏輯於 `JwksPublicKeyProvider` 與 `IApiKeyValidator`
- [ ] 實作 Redis 不可用時的 `NullTokenCacheService` fallback
- [ ] 所有降級行為記錄 warning log 含 CorrelationId
- [ ] 撰寫整合測試：WireMock 模擬逾時、5xx、連線失敗
- [ ] 撰寫整合測試：停止 Redis → 驗證降級行為
- [ ] 每種情境有明確測試案例（總計 5+ 個測試）
