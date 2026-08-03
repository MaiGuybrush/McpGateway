# Sprint 1 Tickets — Core 骨架 + JWT 認證

**目標**: 建立 Core package 完整骨架，部門專案 3 行即可啟動，JWT 認證可用。

**估時**: 8 人天  
**相依**: Sprint 0 Gate 通過、Redis 環境（DevOps）  
**依據**: `docs/specs/development-plan.md` §3、`docs/specs/mcp-gateway-core-spec.md`

---

## Tickets（依賴順序）

### 可立即開始
- [01-host-bootstrap-minimal.md](issues/01-host-bootstrap-minimal.md) — Host bootstrap 最小可運行版本（3 行 Program.cs）

### 第二波（01 完成後）
- [02-config-binding.md](issues/02-config-binding.md) — 設定結構繫結與驗證
- [03-tool-base-contract.md](issues/03-tool-base-contract.md) — Tool 基底契約與掃描

### 第三波（02+03 完成後）
- [04-startup-validation.md](issues/04-startup-validation.md) — 啟動驗證 6 項檢查（blocked by 03）
- [05-jwt-auth.md](issues/05-jwt-auth.md) — JWT 驗證與 JWKS（blocked by 02+03）

### 第四波（05 完成後）
- [06-token-cache-redis.md](issues/06-token-cache-redis.md) — Token Cache (Redis) + TTL 策略

### 最終（全部完成後）
- [07-end-to-end-test.md](issues/07-end-to-end-test.md) — Sprint 1 端到端驗證 + 出口條件

---

## 依賴圖

```
01 Host bootstrap
    ├──> 02 Config binding ──┐
    │                        ├──> 05 JWT auth ──> 06 Token cache ──┐
    └──> 03 Tool contract ───┤                                     │
                             └──> 04 Startup validation ───────────┤
                                                                    │
                                                                    v
                                                            07 E2E test
```

---

## Sprint 1 出口條件

- [ ] 一支測試工具可經 JWT 認證後被呼叫
- [ ] 啟動驗證 6 項各有單元測試，錯誤訊息一次列出全部問題
- [ ] Core 可發布至內部 NuGet feed 並被測試專案還原

---

## Vertical Slicing 原則

每個 ticket 都是**可獨立驗證的完整路徑**：

- **01** → server 可啟動，health check 可用
- **02** → config 可繫結，缺必填欄位會失敗
- **03** → tool 可註冊，tools/list 可回傳
- **04** → 啟動驗證執行，失敗時拋例外
- **05** → JWT 認證可用，工具可執行
- **06** → cache 生效，重複請求命中 Redis
- **07** → 全部整合驗證通過

---

## 實作順序建議

### Phase 1: 骨架（01-03）
先建立基本 host + config + tool contract，確保「部門專案 3 行可啟動」。

### Phase 2: 驗證（04）
加入啟動驗證，確保工具定義正確。

### Phase 3: 認證（05-06）
JWT 驗證 + cache，完成認證管線。

### Phase 4: 整合（07）
端到端測試，驗證出口條件。

---

## 工作 frontier

當前可執行：**01**（無 blocker）

---

## 注意事項

### Redis 環境
- **需求**: Sprint 0 → Sprint 1 需 DevOps 配置 Redis
- **Fallback**: Redis 不可用時降級為不快取（記 warning）

### SDK 版本
- 繼續用 `ModelContextProtocol 1.4.1`（內部 feed 可用）
- 2.0.0 升級非阻斷，可延後至 Sprint 2+

### Tool 範例
- 使用 `src/McpGateway/Tools/Manual/` 某工具作為測試範例
- 改寫為繼承 `ToolBase<TInput, TOutput>`

---

**建立日期**: 2026-08-03  
**預估完成**: 8 人天  
**前置條件**: Sprint 0 完成 ✅
