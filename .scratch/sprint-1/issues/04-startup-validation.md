# 04 — 啟動驗證 6 項檢查

**What to build:** 啟動時對所有註冊工具執行 6 項驗證（Core spec §5.1），驗證失敗則一次列出全部問題並拋例外，不啟動 server。

**Blocked by:** 03-tool-base-contract

**Status:** ready-for-agent

- [ ] 實作 `IStartupValidator` 介面
- [ ] 驗證 #1: 類別有 `[McpTool]` attribute
- [ ] 驗證 #2: `TInput` 所有屬性有 `[Description]` attribute
- [ ] 驗證 #3: `[McpTool].Name` 以 `{Department}_` 開頭（ADR-009 D6）
- [ ] 驗證 #4: 工具名在本服務內唯一（無重複）
- [ ] 驗證 #5: `Version` 若有值，須符合 SemVer 格式
- [ ] 驗證 #6: 孤兒描述覆寫路徑（記 warning，非 fail-fast）
- [ ] 收集全部錯誤後統一拋 `StartupValidationException`（含全部問題清單）
- [ ] 錯誤訊息含工具名稱、檔案位置、具體問題描述
- [ ] `RunMcpGatewayAsync` 執行驗證，失敗時不啟動
- [ ] 撰寫單元測試覆蓋 6 項驗證（正常、失敗、多項同時失敗）
