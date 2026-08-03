# Sprint 0 Tickets — 環境與 SDK Spike

**目標**：讓「能不能做」這件事有答案。決策關卡，非暖身。

**估時**：5 人天  
**阻斷**：全部後續工作  
**依據**：`docs/specs/development-plan.md` §2

---

## Tickets（依賴順序）

### 可立即開始
- [01-sdk-spike.md](issues/01-sdk-spike.md) — SDK spike: 最小可運行 MCP server（驗證 MapMcp path prefix + attribute registration）
- [02-core-skeleton.md](issues/02-core-skeleton.md) — Core 專案骨架 + NuGet 發布流程

### 第二波（01 完成後）
- [03-latency-baseline.md](issues/03-latency-baseline.md) — 量測真實延遲基準（需 spike server）

### 第三波（02 完成後）
- [04-cleanup-poc-code.md](issues/04-cleanup-poc-code.md) — 清理無法編譯程式碼（需 Core package）

### 最終（全部完成後）
- [05-gate-verification.md](issues/05-gate-verification.md) — Sprint 0 Gate 驗證與文檔

---

## 依賴圖

```
01 SDK spike ────┐
                 ├──> 03 延遲量測
                 │
                 └──────────────┐
                                ├──> 05 Gate 驗證
02 Core 骨架 ────> 04 清理程式碼 ┘
```

---

## Sprint 0 出口條件（Gate）

必須全數為「是」才進 Sprint 1：

- [ ] 有可運行 MCP server，能被 MCP client 連上並列出工具
- [ ] Attribute 標註 + 組件掃描註冊確認可行
- [ ] MapMcp 路徑前綴確認可行（否則 ADR-009 D4 需改設計）
- [ ] Preview SDK 無阻斷性 bug，或已知 GA 時程可接受
- [ ] 取得第一組真實延遲數據
- [ ] 內部 NuGet feed 可發布與還原

**Gate 失敗 fallback** 見 `development-plan.md` §2 表格。

---

## 工作 frontier

當前可執行：**01, 02**（無 blocker）
