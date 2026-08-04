# 05 — Sprint 2 端到端驗證

**What to build:** 完整 Sprint 2 出口條件驗證：API-KEY 與 JWT 兩條認證路徑皆可用，降級策略有測試覆蓋，稽核日誌含全部必要欄位且 PII 已遮蔽。

**Blocked by:** 01-apikey-client, 02-auth-degradation, 04-audit-log-pii

**Status:** ready-for-agent

## Sprint 2 出口條件驗證

- [ ] **條件 1**: API-KEY 與 JWT 兩條認證路徑皆可用
  - [ ] 帶 JWT token → tool 執行成功
  - [ ] 帶 API-KEY token → tool 執行成功
  - [ ] 兩條路徑的 ToolContext 正確建立
  - [ ] cache 機制於兩條路徑皆運作

- [ ] **條件 2**: 認證服務停機時降級行為符合 §6.4，有整合測試覆蓋
  - [ ] 5 種降級情境各有測試案例
  - [ ] WireMock 模擬服務異常（逾時、5xx）
  - [ ] Redis 停機測試（降級為不快取）
  - [ ] Warning log 正確記錄

- [ ] **條件 3**: 稽核日誌含全部必要欄位，PII 遮蔽經測試驗證
  - [ ] 所有必要欄位存在：Department, CoreVersion, CorrelationId 等
  - [ ] PII 欄位已遮蔽（email, phone, customerName 等）
  - [ ] 原始 PII 值未出現於 sink（負面測試）
  - [ ] 日誌格式正確（JSON / structured log）

## 整合測試

- [ ] 完整流程：API-KEY 認證 → 呼叫 tool → 下游 API → 稽核日誌
- [ ] 完整流程：JWT 認證 → 呼叫 tool → 下游 API → 稽核日誌
- [ ] 降級流程：認證服務逾時 → fallback to cache → 成功執行
- [ ] 異常流程：無 cache + 服務停機 → 503 回應

## 文件更新

- [ ] 更新 `docs/specs/development-plan.md` Sprint 2 狀態為已完成
- [ ] 產出 `docs/sprint-2-report.md`（包含降級測試結果、PII 遮蔽驗證）
- [ ] 更新 `README.md`（若有 API-KEY 使用範例）

## NuGet Package

- [ ] `dotnet pack src/McpGateway.Core/` 產出 `0.2.0-preview`
- [ ] 驗證版本號正確（CoreVersion 欄位應為 0.2.0）
