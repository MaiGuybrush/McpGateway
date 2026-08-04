# 09 — 效能 Baseline 量測

**What to build:** K6 腳本跑完整 E2E 流程，量測 p95 延遲，驗證 <50ms 門檻，輸出效能報告。

**Blocked by:** #07, #08

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `tests/k6/sprint-4-baseline.js` 建立
- [ ] 模擬 100 並發 users，持續 5 分鐘
- [ ] 場景：隨機呼叫 `report_query_wip`（3 種 shop_id, 2 種 group_by）
- [ ] 記錄指標：
  - p50, p95, p99 延遲
  - 成功率（2xx vs 5xx）
  - RPS (requests per second)
- [ ] 輸出報告：`tests/k6/sprint-4-report.md`
- [ ] 驗證：p95 < 50ms（含 ingress/proxy overhead）
- [ ] 若超標：分析瓶頸（DB query, NTLM auth, 下游 API），記錄優化建議
- [ ] 整合測試正確率彙總：SK + Pydantic AI 總正確率 >= 85%
