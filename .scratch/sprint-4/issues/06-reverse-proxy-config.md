# 06 — Reverse Proxy 路徑分流設定

**What to build:** Nginx 或 IIS URL Rewrite 設定，將 `/report` 路徑轉發到 Report Gateway。

**Blocked by:** #05

**Status:** ready-for-agent

## Acceptance criteria

- [ ] Nginx 設定範例：`deploy/nginx/report.conf`（`location /report { proxy_pass ... }`）
- [ ] IIS URL Rewrite 設定範例：`deploy/iis/web.config`（rewrite rule for `/report`）
- [ ] 驗證：`curl http://proxy/report/mcp` → 轉發到 Report Gateway
- [ ] 驗證：路徑前綴正確移除（gateway 收到的路徑為 `/mcp`，非 `/report/mcp`）
- [ ] 文件更新：`deploy/report-gateway/README.md` 加入 proxy 設定步驟
