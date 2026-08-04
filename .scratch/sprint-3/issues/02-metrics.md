# 02 — Metrics 暴露

**What to build:** 暴露 6 項 Prometheus metrics 於 `/metrics` 端點：`mcpgw_core_version`（版本漂移可見度）、`mcpgw_tool_calls_total`（使用量 + 成功率）、`mcpgw_tool_duration_seconds`（端到端延遲 histogram）、`mcpgw_downstream_duration_seconds`（下游耗時，隔離 gateway 開銷）、`mcpgw_auth_failures_total`（認證異常偵測）、`mcpgw_token_cache_total`（快取效益）。

**Blocked by:** None — can start immediately

**Status:** ready-for-agent

- [ ] `Observability/MetricsService.cs` 註冊於 DI（singleton）
- [ ] `/metrics` 端點回傳 Prometheus text format（使用 `prometheus-net` 或 `System.Diagnostics.Metrics` + exporter）
- [ ] `mcpgw_core_version{dept="report",version="0.1.0"}` gauge，於 `RunMcpGatewayAsync` 啟動時設定
- [ ] `mcpgw_tool_calls_total{dept,tool,status}` counter，每次工具執行完畢 +1（status = success/failure）
- [ ] `mcpgw_tool_duration_seconds{dept,tool}` histogram，記錄端到端耗時（含認證 + 下游）
- [ ] `mcpgw_downstream_duration_seconds{dept,tool}` histogram，僅下游 HTTP 耗時
- [ ] `mcpgw_auth_failures_total{dept,reason}` counter，認證失敗時 +1（reason = invalid_token/timeout/etc）
- [ ] `mcpgw_token_cache_total{dept,result}` counter，result = hit/miss
- [ ] 整合測試：呼叫工具後 scrape `/metrics`，驗證 `mcpgw_tool_calls_total` 增加
- [ ] 單元測試：`MetricsService` 各 metric 的標籤與值正確性
