# CacheStressTester – Test Results Summary

## Test Overview

| Field | Value |
|-------|--------|
| **Test ID / Run Tag** | `Run_2025_10_17_AWS_RedisBaseline` |
| **Date** | `2025-10-17` |
| **Executed By** | `Alex` |
| **Environment** | `AWS ElastiCache` |
| **Redis Endpoint** | `my-elasticache-cluster.amazonaws.com:6379` |
| **Tool Version** | `v1.0.0` |
| **Duration** | `60s` |
| **Threads** | `300` |
| **Requests per Thread** | `2000` |
| **Payload Size (B)** | `1024` |
| **Read/Write Ratio** | `0.5` |
| **Aggressive Mode** | `true` |
| **Publish Metrics** | `false` |
| **Show Progress** | `true` | 

---

## Performance Summary

| Metric | Value | Notes |
|---------|--------|-------|
| **Total Requests** | `120000` |  |
| **Success Count** | `115234` |  |
| **Timeout Count** | `3412` | Mostly during ramp-up |
| **Error Count** | `1354` | Write failures due to latency |
| **Avg Latency (ms)** | `11.43` |  |
| **P95 Latency (ms)** | `47.85` | Slight spikes observed |
| **Requests per Second (RPS)** | `1842.6` | Overall throughput |
| **Cache Hit Ratio** | `0.87` | Redis INFO stats |
| **Evicted Keys Δ** | `26` | Low eviction rate |
| **Memory Δ (MB)** | `+45.7` | Acceptable growth |

---

## Redis Metrics (Native)

| Metric | Before | After | Δ | Observation |
|---------|---------|-------|--|--------------|
| **Used Memory (MB)** | 250.3 | 296.0 | +45.7 | Expected growth |
| **Connected Clients** | 120 | 420 | +300 | Matches thread count |
| **Evicted Keys** | 3210 | 3236 | +26 | Stable |
| **Instantaneous Ops/sec** | 1840 | 1980 | +140 | Peak throughput |

---

## Observations

- Response times remained stable up to ~80% of load duration.  
- A small rise in **timeouts** toward the end may indicate the need to adjust **min EPCU** in ElastiCache.  
- No critical errors or cluster-level rejections were detected.  
- Aggressive mode created sufficient load for scaling test validation.

---

## Recommendations

1. **Scaling Policy Validation**
   - Test with different `min EPCU` settings (e.g., +25%) to verify automatic scaling responsiveness.
2. **Eviction Tracking**
   - Consider enabling Redis `maxmemory-policy` logging to trace evicted keys origin.
3. **Alert Thresholds**
   - CloudWatch alarms:  
     - `MemoryUsage > 75%`  
     - `OpsPerSec < 1000`  
     - `TimeoutCount > 5% of total requests`
4. **Next Run**
   - Plan an additional 120s test with 500 threads to observe sustained load behavior.

---

## Related Artifacts

| File | Description |
|------|--------------|
| `Reports/result_20251015_224701.json` | Raw test output |
| `Logs/run_20251015.log` | Console logs |
| `CloudWatchDashboard.png` | Screenshot of metric trends |
| `ElastiCacheConfigSnapshot.json` | Configuration snapshot |

---

## Summary

| Status | Outcome |
|---------|----------|
| **Test Execution** | Successful |
| **System Stability** | Stable under current EPCU |
| **Scaling Behavior** | Under review |
| **Next Steps** | Validate CloudWatch alarm thresholds |
