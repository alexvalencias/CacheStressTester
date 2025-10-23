# CacheStressTester – Usage Guide

## Overview

`CacheStressTester` is a flexible Redis stress testing tool built in .NET, supporting direct connection strings or AWS Secrets Manager ARNs.  
It can run in **three different modes**, depending on the configuration parameters, and is suitable for **local load testing**, **containerized runs**, or **EC2 deployments**.

---

## 1. Execution Modes

The tester determines its mode automatically based on parameter combinations:

| Mode | Description | Required Parameters |
|------|--------------|----------------------|
| **TimedAndBounded** | Runs for a fixed duration and stops when time expires, even if not all requests complete. | `Threads`, `RequestsPerThread`, `DurationSeconds` |
| **RequestsBounded** | Executes a fixed number of requests per thread, regardless of duration. | `Threads`, `RequestsPerThread` |
| **TimedOnly** | Runs continuously until the duration expires (no request limit). | `Threads`, `DurationSeconds` |

**Important:**  
At least one of `RequestsPerThread` or `DurationSeconds` must be greater than zero.  

---

## 2. Core Parameters

| Parameter | Type | Default | Description |
|------------|------|----------|--------------|
| `Environment` | string | `AWS` | Defines target environment. |
| `RedisConnectionString` | string | — | Redis endpoint connection string. Optional if `RedisSecretArn` is provided. |
| `RedisSecretArn` | string | — | AWS Secrets Manager ARN that contains the Redis connection string. |
| `Threads` | int | `30` | Number of concurrent worker threads. Must be > 0. |
| `RequestsPerThread` | int | `200` | Number of requests per thread (0 = unlimited if using `DurationSeconds`). |
| `DurationSeconds` | int | `15` | Duration limit for the test (0 = unlimited if using `RequestsPerThread`). |
| `PayloadSizeBytes` | int | `1024` | Average size of each payload in bytes. |
| `ReadWriteRatio` | double | `0.5` | Defines proportion of read vs. write operations (0 = all write, 1 = all read). |
| `AggressiveMode` | bool | `true` | Enables randomized payloads and occasional deletes. |
| `ShowProgress` | bool | `true` | Displays progress or periodic console updates during execution. |
| `PublishMetrics` | bool | `false` | Publishes aggregated results to monitoring service (CloudWatch, etc.). |
| `Tag` | string | — | Optional label for grouping test results (e.g. `run1`, `baseline`, etc.). |

---

## 3. Parameter Validation

Parameter validation occurs at startup (`Program.cs`) before the test begins.

**Rules:**
- `Threads` must be **greater than zero**.  
- `RequestsPerThread` and `DurationSeconds` cannot be **negative**.  
- At least one must be **non-zero**.  
- `ReadWriteRatio` must be between **0.0** and **1.0**.  
- Warnings appear if:
  - `Threads > 10,000`  
  - `PayloadSizeBytes > 5,000,000 (5MB)`

Example validation output:
```
[Error] Threads must be greater than zero.
[Error] You must specify either RequestsPerThread or DurationSeconds (or both).
[Warning] Very high thread count detected. Ensure your system can handle this level of concurrency.
```

---

## 4. Running the Tester

### **Option A – Run from source**
```bash
dotnet run --project CacheStressTester -- --Threads 300 --RequestsPerThread 2000 --DurationSeconds 30 --Environment AWS --Tag local-test
```

### **Option B – Run compiled binary**

Once built, you can run the binary directly on any supported OS.

#### **Windows**
```powershell
CacheStressTester.exe --Threads 300 --RequestsPerThread 1000 --DurationSeconds 20 --Environment AWS
```

#### **Linux / macOS**
```powershell
./CacheStressTester --Threads 300 --RequestsPerThread 1000 --DurationSeconds 20 --Environment AWS
```
> **Tip:** You can also pass arguments through environment variables  
> (`STRESS_THREADS`, `STRESS_DURATIONSECONDS`, `STRESS_ENVIRONMENT`, etc.)  
> when running inside containers or automated pipelines.
### **Option C – Docker (local or EC2)**
Build the image:
```bash
docker build -t cachestress-tester .
```

Run the container:
```bash
docker run --rm -e STRESS_ENVIRONMENT=AWS     -e STRESS_REDISSECRETARN="arn:aws:secretsmanager:us-east-1:123456789012:secret:MyRedisSecret-AbCdEf:RedisConnectionString::"     -e STRESS_THREADS=300     -e STRESS_DURATIONSECONDS=60     -e STRESS_AGGRESSIVEMODE=true     cachestress-tester
```

### **Option D – EC2 deployment**
For EC2 instances with AWS CLI/SDK credentials:
1. Ensure IAM Role includes `secretsmanager:GetSecretValue`.
2. Install .NET 8 runtime.
3. Copy the executable and configuration file.
4. Run using:
   ```bash
   ./CacheStressTester --Environment AWS --RedisSecretArn arn:aws:secretsmanager:us-east-1:... --DurationSeconds 120 --Threads 500
   ```

---

## 5. Output & Reports

When the test finishes:
- A JSON report is written to `/Reports/`  
  Example:
  ```
  Reports/result_20251022_221504_run1.json
  ```

Each report contains:
- Total Requests, Success, Errors, Timeouts  
- P95 latency  
- Requests per second  
- Redis memory delta and eviction counts (before/after)  
- Test configuration snapshot  

---

## 6. Console Progress

- **Bounded tests** (Modes 1–2): show progress bar in real time.  
- **TimedOnly mode**: prints updates every 5 seconds:
  ```
  [Progress] Elapsed: 10.0s | TotalOps: 30,200 | RPS: 3,020.0 | Remaining: 50.0s
  ```
You can adjust this interval in `CacheStressRunner.cs` (`intervalSeconds` constant).

---

## 7. Metrics Publishing

If `PublishMetrics = true`, the tester will attempt to push results to the configured metrics backend (CloudWatch, Datadog, etc.)  
Metrics published include:
- `RequestsPerSecond`
- `AvgLatencyMs`
- `P95LatencyMs`
- `MemoryDeltaMB`
- `EvictedKeysDelta`

---

## 8. Example Configurations

### **1. Timed & Bounded (60 seconds or until requests complete)**
```json
{
  "Environment": "AWS",
  "RedisSecretArn": "arn:aws:secretsmanager:us-east-1:123456789012:secret:MyRedisSecret-AbCdEf:RedisConnectionString::",
  "Threads": 300,
  "RequestsPerThread": 10000,
  "DurationSeconds": 60,
  "AggressiveMode": true
}
```

### **2. Requests Only (run all, no time limit)**
```json
{
  "Threads": 200,
  "RequestsPerThread": 5000,
  "DurationSeconds": 0,
  "Environment": "AWS"
}
```

### **3. Time Only (continuous load for 2 minutes)**
```json
{
  "Threads": 400,
  "RequestsPerThread": 0,
  "DurationSeconds": 120,
  "Environment": "AWS"
}
```

---

## 9. Troubleshooting

| Issue | Description / Resolution |
|--------|--------------------------|
| `Unable to capture Redis metrics: This operation is not available unless admin mode is enabled` | Enable `allowadmin=true` in Redis connection string or disable metrics collection. |
| `Connection could not be established` | Verify Redis endpoint, port, or Secrets Manager ARN. |
| `AccessDeniedException` | Ensure EC2 IAM Role or CLI credentials have permission to read the ARN. |
| Very low throughput | Reduce `Threads`, lower payload size, or disable `AggressiveMode`. |

---

## 10. Sample Output Summary

```
--- Cache Stress Test Summary ---
Environment       : AWS
Threads           : 300
Requests/Thread   : 20000
Duration (s)      : 60
Total Requests    : 4,821,500
Requests/sec      : 80,358.33
Average Latency   : 1.24 ms
P95 Latency       : 3.90 ms
Timeouts          : 0
Errors            : 0
Evicted Keys Δ    : 15
Memory Δ (MB)     : +22.45
---------------------------------
Report saved: Reports/result_20251022_221504_run1.json
```

---

## 11. Recommended Defaults for Testing

| Environment | Threads | Duration | Notes |
|--------------|----------|-----------|-------|
| Local (Docker Desktop) | 50–100 | 20–30s | For functional validation. |
| Dev EC2 (small) | 200–400 | 60s | Moderate stress. |
| Prod Benchmark | 500–1000 | 120–300s | High load / capacity validation. |

---

**Last updated:** October 2025
