# 05 — JWT 驗證與 JWKS Public Key

**What to build:** MCP 請求帶 JWT token 時，Core 用 JWKS endpoint 驗證 token 簽章，提取 sub/department/role 建立 ToolContext，JWT 驗證成功的請求可執行工具。

**Blocked by:** 03-tool-base-contract, 02-config-binding

**Status:** ready-for-agent

- [ ] 讀取 `Auth:JwksEndpoint` 設定
- [ ] 實作 `JwksPublicKeyProvider`：從 JWKS endpoint 取得 public keys
- [ ] 快取 JWKS keys（TTL = `Auth:JwksCacheHours`，預設 24 小時）
- [ ] 實作 JWT middleware：解析 `Authorization: Bearer <token>`
- [ ] 驗證 JWT 簽章（使用 JWKS public key）
- [ ] 提取 claims：`sub` → UserId, `department` → Department, `role` → Role
- [ ] 建立 `ToolContext`（TokenType = "JWT", AgentId 從 header 取得）
- [ ] 注入至 `ToolBase<,>` 的 `Context` property
- [ ] Tool 執行時可存取 `Context.UserId`, `Context.Department`
- [ ] 撰寫整合測試：帶合法 JWT → 工具執行成功，帶非法 JWT → 401
- [ ] 降級：JWKS 取得失敗但有快取 → 使用快取 + warning log
