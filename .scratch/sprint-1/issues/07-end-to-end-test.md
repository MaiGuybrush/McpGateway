# 07 — Sprint 1 端到端驗證

**What to build:** 完整 Sprint 1 出口條件驗證：一支測試工具可經 JWT 認證後被呼叫，啟動驗證有完整測試覆蓋，Core package 可發布至 NuGet feed。

**Blocked by:** 04-startup-validation, 06-token-cache-redis

**Status:** ready-for-agent

## Sprint 1 出口條件驗證

- [ ] **條件 1**: 一支測試工具可經 JWT 認證後被呼叫
  - [ ] `src/McpGateway/Tools/Manual/` 某工具完整實作（繼承 ToolBase）
  - [ ] 帶合法 JWT token → `tools/call` 成功執行
  - [ ] Tool 內可存取 `Context.UserId` 等身分資訊
  - [ ] 回傳結果正確

- [ ] **條件 2**: 啟動驗證 6 項各有單元測試，錯誤訊息一次列出全部問題
  - [ ] 6 項驗證各有獨立單元測試
  - [ ] 多項驗證同時失敗 → exception 含全部問題
  - [ ] 錯誤訊息含工具名稱、檔案、具體問題

- [ ] **條件 3**: Core 可發布至內部 NuGet feed 並被測試專案還原
  - [ ] `dotnet pack src/McpGateway.Core/` 產出 `0.1.0-preview` package
  - [ ] Push 至內部 NuGet feed (http://10.53.216.186:5000/)
  - [ ] `src/McpGateway/` 可還原並建置成功

## 整合測試

- [ ] 完整流程測試：啟動 → JWT 認證 → 呼叫工具 → 回傳結果
- [ ] Redis cache 測試：第一次呼叫寫入，第二次命中 cache
- [ ] 驗證失敗測試：錯誤 JWT → 401，缺 Department config → 啟動失敗

## 文件更新

- [ ] 更新 `docs/specs/development-plan.md` Sprint 1 狀態為已完成
- [ ] 產出 `docs/sprint-1-report.md`（類似 sprint-0-report.md 格式）
