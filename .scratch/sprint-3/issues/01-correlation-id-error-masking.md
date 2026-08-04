# 01 — CorrelationId 貫穿 + 錯誤遮蔽

**What to build:** 每次工具呼叫產生唯一 CorrelationId（GUID），貫穿稽核日誌、下游 HTTP 標頭、錯誤訊息。錯誤回傳遮蔽內部細節（堆疊追蹤、內部位址、下游原始錯誤），只給語意化訊息 + CorrelationId。參數驗證錯誤可完整回傳（助 LLM 自我修正）。

**Blocked by:** None — can start immediately

**Status:** ready-for-agent

- [ ] CorrelationId 產生於請求開始，存入 `HttpContext.Items` 或 scoped service
- [ ] 稽核日誌 `ToolInvocationAuditLog.CorrelationId` 欄位填入
- [ ] `IDownstreamClient` 所有請求自動注入 `X-Correlation-Id` 標頭
- [ ] 未預期例外（工具執行中拋出）回傳「工具執行失敗，請聯繫支援並提供 ID: {correlationId}」
- [ ] 下游 5xx 或逾時回傳「下游服務暫時無法使用，請稍後再試」，不含原始錯誤訊息
- [ ] 下游 4xx 回傳語意化訊息（如「查無此訂單編號」），不含下游 API 原始回應
- [ ] 參數驗證失敗（`[Required]`, `[Description]` 缺失等）可完整回傳驗證錯誤
- [ ] 認證失敗回傳「認證失敗」，不含原因細節（防資訊洩漏）
- [ ] 單元測試：各錯誤類型的遮蔽邏輯（`ErrorResponseBuilder` 或類似）
- [ ] 整合測試：呼叫現有工具（如 `EchoUserTool`），驗證錯誤訊息不含堆疊
