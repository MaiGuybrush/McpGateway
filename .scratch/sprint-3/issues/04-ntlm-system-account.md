# 04 — NTLM 系統帳號認證

**What to build:** 第三種認證路徑（NTLM），從環境變數（`NTLM_USERNAME`, `NTLM_PASSWORD`）讀取系統帳號，用於下游需 Windows 認證的服務。NTLM token 不進 Token Cache（規格 §6.2）。認證失敗時記稽核 + CorrelationId。

**Blocked by:** #01 — CorrelationId 需貫穿認證流程

**Status:** ready-for-agent

- [ ] `appsettings.json` `Auth:SystemAccount:Type="NTLM"` + `CredentialSource="Environment"` 時啟用
- [ ] `Auth/NtlmCredentialProvider.cs` 從環境變數 `NTLM_USERNAME` / `NTLM_PASSWORD` 讀取
- [ ] 帳密缺失 → 啟動驗證失敗（fail-fast），錯誤訊息明確指出缺哪個環境變數
- [ ] `DownstreamClientFactory` 為 NTLM 類型建立帶 `CredentialCache.DefaultNetworkCredentials` 的 HttpClient
- [ ] NTLM token 不進 `ITokenCacheService`（跳過快取邏輯）
- [ ] 認證失敗時稽核日誌含 `AuthType="NTLM"` + CorrelationId
- [ ] 下游請求標頭含 `X-Auth-Type: NTLM`
- [ ] 單元測試：環境變數缺失 → `NtlmCredentialProvider` 拋例外
- [ ] 單元測試：NTLM HttpClient 包含正確 credentials
- [ ] 整合測試（WireMock）：模擬需 Windows auth 的下游（401 → retry with credentials → 200）
