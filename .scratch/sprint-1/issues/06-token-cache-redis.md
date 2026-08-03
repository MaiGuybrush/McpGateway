# 06 — Token Cache (Redis) + TTL 策略

**What to build:** JWT/API-KEY 驗證結果快取至 Redis，避免重複驗證同一 token。Cache key 為 `auth:{tokenType}:{tokenHash}`，跨部門共用。

**Blocked by:** 05-jwt-auth, 02-config-binding

**Status:** ready-for-agent

- [ ] 讀取 `TokenCache:Redis` 設定（connection string）
- [ ] 實作 `ITokenCacheService` 介面
- [ ] 使用 `StackExchange.Redis` 連線至 Redis
- [ ] Cache key 格式：`auth:{tokenType}:{SHA256(token)}`（不含部門）
- [ ] TTL 策略：
  - JWT: `exp - now - JwtExpirySkewMinutes`（預設 -1 min）
  - API-KEY: `ApiKeyTtlMinutes`（預設 5 min）
  - NTLM: 不快取
- [ ] JWT middleware 先查 cache，命中則跳過 JWKS 驗證
- [ ] 驗證成功後寫入 cache（value = JSON serialized ToolContext）
- [ ] 撰寫單元測試：cache miss → 驗證 → 寫入，cache hit → 直接用
- [ ] 撰寫整合測試：連 Redis 驗證 TTL 正確設定
- [ ] 降級：Redis 不可用 → 降級為不快取 + warning log（直接呼叫認證服務）
