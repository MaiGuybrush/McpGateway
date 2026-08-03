# 02 — 設定結構繫結與驗證

**What to build:** Core 可讀取並驗證 `appsettings.json` 的 `McpGateway` section，缺少必填欄位時啟動失敗並給出清楚錯誤訊息。

**Blocked by:** 01-host-bootstrap-minimal

**Status:** ready-for-agent

- [ ] 定義 `McpGatewayOptions` class（對應 Core spec §4）
- [ ] 必填欄位：`Department`, `RoutePrefix`
- [ ] 選填欄位：`Ocelot`, `Auth`, `TokenCache`, `Audit`
- [ ] `AddMcpGateway` 使用 `IOptions<McpGatewayOptions>` 繫結
- [ ] 缺少 `Department` → 拋 `ConfigurationException` 並明確指出欄位名稱
- [ ] `RoutePrefix` 不等於 `/{Department}` → 記 warning log（允許但不建議）
- [ ] 撰寫單元測試：缺必填欄位、正確繫結、warning 觸發
