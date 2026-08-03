# 03 — 量測真實延遲基準

**What to build:** 對 SDK spike server 進行首次真實延遲量測（單體、無 ingress、本機環境），取得 p50/p95/p99 數據，對比 PoC-REPORT.md 原預估值（p95 42-52ms），記錄結果供後續參考。

**Blocked by:** 01-sdk-spike（需有可運行 server）

**Status:** ready-for-agent

- [ ] 準備負載測試工具（k6 或 wrk）
- [ ] 撰寫測試腳本：100 concurrent 持續 30s 呼叫單一 tool
- [ ] 執行測試取得 p50/p95/p99 延遲數據
- [ ] 對比 PoC-REPORT.md:375 原預估值（p95 42-52ms）
- [ ] 記錄結果至 `docs/sprint-0-latency-baseline.md`（含測試環境說明）
- [ ] 若 p95 >> 52ms → 標註風險（需重審 ADR-001）
