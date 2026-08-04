# Sprint 2 Tickets — API-KEY + 下游呼叫 + 稽核

**目標**: 完成第二條認證路徑（API-KEY），實作下游呼叫與稽核日誌，驗證降級策略。

**估時**: 7.5 人天  
**相依**: Sprint 1 完成、API-KEY 服務 SLA 確認（Auth 團隊）  
**依據**: `docs/specs/development-plan.md` §4、`docs/specs/mcp-gateway-core-spec.md` §6-8

---

## Tickets（依賴順序）

### 第一波（可立即開始）
- [01-apikey-client.md](issues/01-apikey-client.md) — API-KEY 服務客戶端 + 快取
- [03-downstream-client.md](issues/03-downstream-client.md) — IDownstreamClient + 具名 HttpClient + 重試（可與 01 平行）

### 第二波（01 完成後）
- [02-auth-degradation.md](issues/02-auth-degradation.md) — 認證降級策略（5 種情境）

### 第三波（03 完成後）
- [04-audit-log-pii.md](issues/04-audit-log-pii.md) — 稽核日誌 + PII 遮蔽

### 最終（全部完成後）
- [05-end-to-end-sprint2.md](issues/05-end-to-end-sprint2.md) — Sprint 2 端到端驗證 + 出口條件

---

## 依賴圖

```
01 API-KEY client ──> 02 Auth degradation ──┐
                                             │
03 Downstream client ──> 04 Audit log + PII ┤
                                             │
                                             v
                                     05 E2E test
```

---

## Sprint 2 出口條件

- [ ] API-KEY 與 JWT 兩條認證路徑皆可用
- [ ] 認證服務停機時降級行為符合 §6.4，有整合測試覆蓋
- [ ] 稽核日誌含全部必要欄位，PII 遮蔽經測試驗證（原始值不進 sink）

---

## Vertical Slicing 原則

每個 ticket 都是**可獨立驗證的完整路徑**：

- **01** → API-KEY 認證可用，token 驗證成功，tool 可執行
- **02** → 5 種降級情境測試通過，warning log 正確
- **03** → Tool 可呼叫下游 API，headers 自動注入，retry 運作
- **04** → 稽核日誌寫入，PII 遮蔽正確，原始值未洩漏
- **05** → 全部整合驗證通過，出口條件滿足

---

## 實作順序建議

### Phase 1: 認證路徑（01）
實作 API-KEY 驗證，與 JWT 形成雙路徑。

### Phase 2: 降級（02）
驗證 5 種異常情境降級行為。

### Phase 3: 下游呼叫（03）
實作 IDownstreamClient，tool 可呼叫下游 API。

### Phase 4: 稽核（04）
完整稽核日誌 + PII 遮蔽。

### Phase 5: 整合（05）
端到端測試，驗證出口條件。

---

## 工作 frontier

當前可執行：**01, 03**（平行開發）

---

## 注意事項

### API-KEY 服務
- **需求**: Auth 團隊提供 API-KEY 驗證服務 endpoint + SLA
- **Timeout**: 預設 3 秒（可設定）
- **Fallback**: 逾時 + cache 有值 → 使用 cache

### 下游呼叫
- **Retry**: 僅冪等方法（GET/PUT/DELETE）
- **POST**: 預設不重試（避免重複建單）
- **Headers**: 自動注入身分標頭（X-User-*, X-Auth-Type, X-Correlation-Id）

### PII 遮蔽
- **時機**: 寫入日誌**前**（原始值不進 sink）
- **規則**: email → `c***@example.com`, 其他 → 前 2 字元 + `***`
- **測試**: 必須有負面測試確認原始值未洩漏

### 降級策略
- **5 種情境**: API-KEY 逾時（有/無 cache）、JWKS 失敗（有/無 cache）、Redis 不可用
- **行為**: 有 cache 用 cache + warning，無 cache 回 503
- **測試**: WireMock 模擬異常 + Redis 停機測試

---

**建立日期**: 2026-08-03  
**預估完成**: 7.5 人天  
**前置條件**: Sprint 1 完成 ✅
