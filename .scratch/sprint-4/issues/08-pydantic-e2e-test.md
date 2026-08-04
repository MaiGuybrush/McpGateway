# 08 — Pydantic AI E2E 測試

**What to build:** Pydantic AI 連接 Report Gateway，驗證 `report_query_wip` 工具選擇與參數填充正確率。

**Blocked by:** #04, #06

**Status:** ready-for-agent

## Acceptance criteria

- [ ] `tests/e2e/test_pydantic_ai.py` 建立
- [ ] Pydantic AI agent 設定 MCP server URL（`http://localhost:5000/report/mcp`）
- [ ] 測試 prompt 5 題（與 #07 不同場景，補充多步驟與模糊描述）：
  - "What's the WIP status in CF3?"
  - "Give me ARY7 panel count by owner"
  - "Check if TFT7 has any bottleneck at ASSY process"
  - "Compare CF3 vs ARY7 total sheets"
  - "Export CF3 WIP data grouped by product"
- [ ] 記錄正確率：工具選擇正確 + 參數填充正確 = 成功
- [ ] 輸出報告：`tests/e2e/pydantic-results.md`（5/5 or X/5, 失敗案例附錯誤）
- [ ] 正確率 >= 80% 通過
