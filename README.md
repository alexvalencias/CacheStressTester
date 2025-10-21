# CacheStressTester

CacheStressTester is a **Redis cache load testing tool** designed to evaluate performance, stability, and behavior under high concurrency.  
It simulates realistic workloads by generating configurable read/write operations and collecting performance metrics before and after the test.

---

## Overview

The tool connects to a **Redis-compatible cache** (e.g., AWS ElastiCache, Azure Cache for Redis, or a local Redis instance)  
and executes a configurable number of operations across multiple threads for a specified duration.

It captures key performance indicators such as:
- Latency (average and P95)
- Success, timeout, and error rates
- Memory usage before/after
- Cache hit ratio
- Evicted keys and instantaneous operations per second

---

## Configuration

All runtime parameters are loaded from **`appsettings.json`**, environment variables, or CLI arguments (if extended later).

### Example `appsettings.json`
```json
{
  "CacheTestConfig": {
    "Environment": "AWS",
    "RedisConnectionString": "localhost:6379",
    "DurationSeconds": 60,
    "Threads": 200,
    "RequestsPerThread": 1000,
    "PayloadSizeBytes": 1024,
    "ReadWriteRatio": 0.5,
    "AggressiveMode": true,
    "PublishMetrics": false,
    "ShowProgress": true
  }
}
```

### Parameter Description

| Property | Type | Default | Description |
|-----------|------|----------|-------------|
| **Environment** | `string` | `"AWS"` | Logical environment (used for metric publisher selection). |
| **RedisConnectionString** | `string` | `"localhost:6379"` | Redis endpoint. For ElastiCache, include host and port. |
| **DurationSeconds** | `int` | `20` | Test duration in seconds. |
| **Threads** | `int` | `50` | Number of parallel worker threads. |
| **RequestsPerThread** | `int` | `200` | Number of requests executed per thread. |
| **PayloadSizeBytes** | `int` | `1024` | Size of the value written in write operations. |
| **ReadWriteRatio** | `double` | `0.5` | Ratio between read and write operations. |
| **AggressiveMode** | `bool` | `false` | When true, enables delete operations and variable payloads. |
| **PublishMetrics** | `bool` | `false` | If true, publishes metrics to CloudWatch or App Insights. |
| **ShowProgress** | `bool` | `true` | Controls whether a live progress bar is displayed in console. |

---

## How It Works

1. **Initialization** - Reads configuration, connects to Redis, and captures initial metrics.  
2. **Execution** - Runs multiple threads performing read/write/delete operations using `ICacheClient`.  
3. **Progress & Feedback** - Optionally shows a live progress bar and message updates (`ShowProgress = true`).  
4. **Metrics Collection** - After the test ends, it captures post-test metrics.  
5. **Result Reporting** - Writes results as JSON and prints a human-readable summary.  
6. **(Optional)** - Publishes metrics to CloudWatch or Application Insights.

---

## Clients and Extensibility

| Client | Description |
|---------|--------------|
| **RedisCacheClient** | Default client using StackExchange.Redis. |
| **AzureRedisClient** | Compatible client for Azure Redis Cache. |

You can extend by implementing the `ICacheClient` interface:
```csharp
public interface ICacheClient
{
    Task<byte[]?> GetAsync(string key);
    Task SetAsync(string key, byte[] value);
}
```

---

## Example Run

### Local Redis Example

```bash
dotnet run --project CacheStressTester
```

Output example:

```
Starting CacheStressTester...

--- Effective Test Configuration ---
Environment        : AWS
Redis Endpoint     : localhost:6379
Duration (seconds) : 15
Threads            : 300
Requests per thread: 2000
Payload size (B)   : 1024
Read/Write Ratio   : 0.5
Publish Metrics    : False
Show Progress      : True
------------------------------------

Running in AGGRESSIVE MODE - high pressure load enabled.
[############################------------]  69% Test duration reached, stopping...

--- Stress Test Summary ---
Total Requests : 6966
Success Count  : 2499
Timeout Count  : 489
Error Count    : 495
Avg Latency    : 265.28 ms
P95 Latency    : 492.55 ms

Report saved: bin/Debug/net9.0/Reports/result_20251015_224701.json

Stress test completed.
```

---

## Output Report

Each run generates a detailed JSON file in the `Reports/` directory.

### Example JSON Output
```json
{
  "Environment": "AWS",
  "TotalRequests": 6966,
  "SuccessCount": 2499,
  "TimeoutCount": 489,
  "ErrorCount": 495,
  "AvgLatencyMs": 265.28,
  "P95LatencyMs": 492.55,
  "RequestsPerSecond": 203.21,
  "UsedMemoryMB_Before": 35.12,
  "UsedMemoryMB_After": 42.38,
  "MemoryDeltaMB": 7.26,
  "EvictedKeysDelta": 12,
  "HitRatio": 0.81
}
```

---

## Metrics Definitions

| Metric | Description |
|---------|--------------|
| **AvgLatencyMs** | Average response time across all requests. |
| **P95LatencyMs** | 95th percentile latency — useful for tail performance. |
| **RequestsPerSecond** | Total throughput over test duration. |
| **UsedMemoryMB_Before / After** | Redis memory usage before/after test. |
| **MemoryDeltaMB** | Memory growth during the test. |
| **EvictedKeysDelta** | Number of keys Redis evicted during test due to memory pressure. |
| **HitRatio** | Cache hit ratio based on Redis keyspace metrics. |
| **InstantaneousOpsPerSec** | Native Redis metric for total ops/sec. |
| **ConnectedClients** | Number of concurrent Redis client connections. |

---

## Aggressive Mode

When `AggressiveMode = true`, the runner:
- Generates variable payloads between 512 bytes and 8× the configured payload size.  
- Adds occasional `DELETE` operations (~5%).  
- Adds small random delays to break CPU scheduling patterns.  
- Produces a more realistic high-pressure environment.

This mode helps identify cache eviction, latency degradation, or memory fragmentation under stress.

---

## Progress Control

When `ShowProgress = true`, a live progress bar is displayed:

```
[########################--------------------]  60% Running...
```

If disabled (`ShowProgress = false`), the test runs silently — useful for automated benchmarking or CI runs.

---

## Publishing Metrics (Optional)

If `PublishMetrics = true`, test results can be published to:
- **AWS CloudWatch** - via `MetricsPublisherService.PublishToCloudWatch()`
- **Azure Application Insights** - via `MetricsPublisherService.PublishToAppInsights()`

(Requires credentials/environment setup for the respective SDKs.)

---

## Extending the Tool

### Add a new cache backend
1. Implement `ICacheClient`.  
2. Register it in `Program.cs` based on configuration (e.g., `"CacheType": "CustomRedis"`).  

### Add a new metrics publisher
1. Implement `IMetricsPublisher` or extend `MetricsPublisherService`.  
2. Use dependency injection to register custom targets.

---

## Folder Structure

```
CacheStressTester/
│
├── Models/
│   ├── CacheTestConfig.cs
│   ├── CacheResult.cs
│   └── RedisServerMetrics.cs
│
├── Services/
│   ├── CacheStressRunner.cs
│   ├── RedisCacheClient.cs
│   ├── AzureRedisClient.cs
│   ├── RedisMetricsCollector.cs
│   ├── ConsoleProgress.cs
│   ├── ResultReporter.cs
│   └── MetricsPublisherService.cs
│
└── Program.cs
```

---

## Requirements

- **.NET 9.0 SDK**
- **Redis** instance (local, Docker, or remote)
- (Optional) AWS/Azure credentials if using metrics publishing

To run locally with Docker:

```bash
docker run -d --name redis -p 6379:6379 redis:latest
```

Then execute:
```bash
dotnet run --project CacheStressTester
```

---

## Remarks

- The progress bar and spinner are purely cosmetic; disable via `ShowProgress = false` for headless or CI runs.
- Aggressive mode may produce Redis eviction events; this is expected under heavy load and useful for capacity analysis.
- Test duration is enforced via `CancellationTokenSource`, ensuring graceful shutdown.
