# 03 — IDownstreamClient + 具名 HttpClient + 重試

**What to build:** Tool 可透過 `IDownstreamClient` 呼叫下游 API（Ocelot），自動帶入身分標頭（X-User-Id, X-User-Department, X-User-Role 等），GET/PUT/DELETE 自動重試（冪等），POST 不重試。

**Blocked by:** None — 可與 01 平行

**Status:** ready-for-agent

- [ ] 定義 `IDownstreamClient` 介面（Core spec §7）
- [ ] 實作 `DownstreamClient` 使用具名 HttpClient
- [ ] 讀取 `Ocelot:BaseUrl`, `Ocelot:TimeoutSeconds`, `Ocelot:Retry` 設定
- [ ] 自動注入 headers（Core spec §6.3）：
  - `X-User-Id`, `X-User-Department`, `X-User-Role`
  - `X-Auth-Type`, `X-Correlation-Id`, `X-Gateway-Department`
- [ ] 實作 retry policy（僅 GET/PUT/DELETE）：
  - 可重試狀態碼：408, 429, 5xx
  - 重試次數 `Retry:Count`，backoff `Retry:BackoffMs`
- [ ] POST 預設不重試（避免重複建單）
- [ ] `ToolBase<TInput, TOutput>` 注入 `IDownstreamClient` 至 `Downstream` property
- [ ] 撰寫單元測試：headers 正確注入、retry 行為正確
- [ ] 撰寫整合測試：WireMock 模擬下游 API（200, 500 + retry, timeout）
