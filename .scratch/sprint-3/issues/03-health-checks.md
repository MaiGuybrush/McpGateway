# 03 — Health Checks

**What to build:** `/health/live`（行程存活，永遠 200）+ `/health/ready`（相依檢查：Redis 可連 + JWKS 可取得，未就緒回 503）。供 K8s liveness/readiness probe 使用（未來部署）。

**Blocked by:** None — Redis/JWKS 已於 Sprint 1 實作

**Status:** ready-for-agent

- [ ] `GET /health/live` 端點永遠回 200 OK `{"status":"healthy"}`（除非 process 已死）
- [ ] `GET /health/ready` 檢查 Redis 連線（`IConnectionMultiplexer.IsConnected` 或 ping）
- [ ] `GET /health/ready` 檢查 JWKS 端點可達（HEAD 請求或 cached key 存在）
- [ ] 任一檢查失敗 → 回 503 Service Unavailable `{"status":"unhealthy","checks":[...]}`
- [ ] Ready check 總超時 2 秒（避免拖慢探測）
- [ ] 使用 `Microsoft.Extensions.Diagnostics.HealthChecks`
- [ ] `Observability/HealthChecks.cs` 定義 `RedisHealthCheck` + `JwksHealthCheck`
- [ ] `AddMcpGateway` 註冊 health checks
- [ ] 單元測試：Redis 不可連 → ready 回 503
- [ ] 單元測試：JWKS 端點 404 → ready 回 503
- [ ] 整合測試：正常情況兩端點皆 200
