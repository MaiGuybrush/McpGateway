# 05 — Sprint 0 Gate 驗證與文檔

**What to build:** 驗證 Sprint 0 全部 6 項出口條件（development-plan.md §2），產出 sprint-0-report.md 記錄驗證結果、延遲數據、MapMcp 路徑前綴結論。若 MapMcp 不支援 path prefix → 更新 ADR-009 D4 為 hostname routing fallback。

**Blocked by:** 01-sdk-spike, 02-core-skeleton, 03-latency-baseline, 04-cleanup-poc-code

**Status:** ready-for-agent

- [ ] 驗證：有可運行 MCP server（來自 01）
- [ ] 驗證：Attribute + 組件掃描可行（來自 01）
- [ ] 驗證：MapMcp 路徑前綴支援結果（來自 01）
- [ ] 驗證：延遲數據已取得（來自 03）
- [ ] 驗證：NuGet feed 可發布/還原（來自 02）
- [ ] 驗證：程式碼已清理可建置（來自 04）
- [ ] 產出 `docs/sprint-0-report.md`：記錄全部驗證結果、決策、數據
- [ ] **若 MapMcp 不支援 path prefix**：更新 ADR-009 D4 section，改為 hostname routing，並記錄此 fallback 對 ingress 配置的影響
- [ ] 更新 development-plan.md Sprint 0 狀態為已完成
