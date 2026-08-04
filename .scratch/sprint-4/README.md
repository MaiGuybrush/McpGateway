# Sprint 4：首個部門專案 + 部署驗證

**目標**：建立 `McpGateway.Report` 部門 gateway，實作 `report_query_wip` 工具，完成 E2E 測試與效能量測。

**總票數**：9 tickets
**預估**：Core 4 人天 + 部門 5 人天

---

## Tickets 依賴圖

```
#01 Report Gateway 骨架 (獨立)
 ├─ #02 query_wip 工具
 ├─ #05 部署配置
 │   └─ #06 Reverse Proxy
 │
#03 下游 Mock (獨立)
 │
#02 + #03
 └─ #04 整合測試
     ├─ #07 SK E2E
     └─ #08 Pydantic AI E2E
         └─ #09 效能 Baseline
```

---

## 工作前線 (Frontier)

可立即開始（無依賴）：
- **#01** — Report Gateway 骨架
- **#03** — 下游 Mock

---

## 關鍵量測（Sprint 4 Gate）

完成 #09 後必須回答：

1. **p95 延遲** — 含 ingress/proxy，與 <50ms 門檻的關係？
2. **工具選擇正確率** — SK + Pydantic AI 總正確率 >= 85%？
3. **部署耗時** — 從零到部署花多久？（ADR-002 目標：≤ 1 人天）

若任一指標未達標，需回頭檢討 ADR-001（協定選擇）與整體 ROI。

---

## 實作參考

- **工具範例**：`D:\Projects\agent\cim-agent\agents\mfg_report\tools.py:query_wip_data`
- **Core 工具基底**：`src/McpGateway.Core/Tools/ToolBase.cs`
- **測試 prompts**：`tests/McpGateway.Tests/TestPrompts.md`（改編為 WIP 場景）

---

**建立日期**：2026-08-04
**狀態**：ready-for-implementation
