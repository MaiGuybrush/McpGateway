# 07 — Semantic Kernel E2E 測試

**What to build:** Semantic Kernel 連接 Report Gateway，驗證 `report_query_wip` 工具選擇與參數填充正確率。

**Blocked by:** #04, #06

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `tests/E2E/SemanticKernelTests.cs` 建立（或獨立專案）
- [ ] SK 註冊 MCP plugin，指向 Report Gateway（`http://localhost:5000/report/mcp`）
- [ ] 測試 prompt 5 題（參考 `TestPrompts.md` 改編為 WIP 場景）：
  - "Query CF3 WIP data"
  - "Get ARY7 WIP grouped by process"
  - "Show TFT7 WIP for product TJ6F"
  - "CF3 WIP at proc LAMI, group by product"
  - "ARY7 total sheets"
- [ ] 記錄正確率：工具選擇正確 + 參數填充正確 = 成功
- [ ] 輸出報告：`tests/E2E/sk-results.md`（5/5 or X/5, 失敗案例附錯誤）
- [ ] 正確率 >= 80% 通過
