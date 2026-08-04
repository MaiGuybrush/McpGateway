# 01 — API-KEY 服務客戶端 + 快取

**What to build:** MCP 請求帶 API-KEY token 時，Core 呼叫 API-KEY 驗證服務驗證 token，提取身分資訊建立 ToolContext，驗證結果快取至 Redis（TTL 5 分鐘），API-KEY 認證成功的請求可執行工具。

**Blocked by:** None — Sprint 1 已完成

**Status:** ready-for-agent

- [ ] 讀取 `Auth:ApiKeyServiceUrl` 與 `Auth:ApiKeyTimeoutSeconds` 設定
- [ ] 實作 `IApiKeyValidator` 介面
- [ ] HTTP POST 至 API-KEY 服務 `/validate` endpoint（body: `{"apiKey": "..."}`）
- [ ] 解析回應取得 userId, department, role
- [ ] 建立 `ToolContext`（TokenType = "API-KEY"）
- [ ] 寫入 Token Cache（key: `auth:apikey:{SHA256(token)}`, TTL: `ApiKeyTtlMinutes`）
- [ ] 實作 API-KEY middleware：解析 `Authorization: Bearer <api-key>`
- [ ] 先查 cache，命中則跳過驗證服務呼叫
- [ ] 驗證失敗 → 401，逾時 → 503
- [ ] 撰寫單元測試：驗證成功、cache 命中、逾時處理
- [ ] 撰寫整合測試：WireMock 模擬 API-KEY 服務
