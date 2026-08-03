# 01 — SDK spike: 最小可運行 MCP server

**What to build:** 建立最小可運行 MCP server 驗證三個關鍵假設：(1) ModelContextProtocol SDK 基本功能可用，(2) MapMcp 支援 path prefix mounting（ADR-009 D4 前提），(3) Attribute-based tool registration + 組件掃描可行（design-doc 4.2.2 v1.1 簡化依據）。Server 可被 MCP client 連線，列出工具，並成功呼叫。

**Blocked by:** None — 可立即開始

**Status:** ready-for-agent

- [ ] 建立 ASP.NET Core minimal API project (net9.0)
- [ ] 安裝 ModelContextProtocol 1.4.1 + ModelContextProtocol.AspNetCore 1.4.1
- [ ] 實作單一測試 tool 用 `[McpTool("test_echo")]` attribute
- [ ] 掛載 MapMcp 於 path prefix `/test`（驗證 ADR-009 D4 假設）
- [ ] 啟動 server，確認無錯誤
- [ ] 用 MCP client 連線取得 tool list
- [ ] 呼叫 tool 並驗證回傳正確
- [ ] 記錄 MapMcp path prefix 支援結果至 spike 專案 README.md
