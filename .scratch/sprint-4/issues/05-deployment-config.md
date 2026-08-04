# 05 — 部署配置（IIS / standalone）

**What to build:** 部署腳本 + 環境變數範本，支援兩種部署方式：IIS hosting 或 standalone exe。

**Blocked by:** #01

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `deploy/report-gateway/` 目錄建立
- [ ] IIS 部署：`web.config` 範本（含 `aspNetCore` handler 設定）
- [ ] Standalone 部署：PowerShell 啟動腳本（`start-report-gateway.ps1`）
- [ ] 環境變數範本：`.env.template`（含 `NTLM_USERNAME`, `NTLM_PASSWORD`, `JwksUrl`, `RedisConnectionString`）
- [ ] 部署文件：`deploy/report-gateway/README.md`（兩種方式的步驟）
- [ ] 驗證：在本地 IIS 或 PowerShell 啟動成功
