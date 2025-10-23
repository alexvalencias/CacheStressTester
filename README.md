# CacheStressTester

A .NET-based Redis stress testing tool for benchmarking ElastiCache and Redis performance under configurable workloads.

## Overview

CacheStressTester allows engineers to simulate high concurrency workloads, measure Redis performance metrics (latency, throughput, memory usage), and optionally publish results to CloudWatch or other monitoring systems.

It supports:
- AWS Secrets Manager integration via ARN (`AwsSecretArn` / `AwsSecretHelper`)
- Three execution modes (`TimedAndBounded`, `RequestsBounded`, `TimedOnly`)
- Configurable read/write ratio and payload size
- Optional metric publishing and report generation

---

## Quick Start

```bash
dotnet run --project CacheStressTester -- --Threads 300 --RequestsPerThread 2000 --DurationSeconds 30 --Environment AWS
```

For detailed configuration, execution modes, and usage examples, see the [Usage Guide](USAGE_GUIDE.md).

---

## Requirements
- .NET 8 SDK or runtime
- Access to a Redis-compatible endpoint (local, AWS ElastiCache, Azure, etc.)
- Optional: AWS credentials with `secretsmanager:GetSecretValue` permission (for `RedisSecretArn`)

---

## Reports

Test results are stored automatically in:
```
/Reports/result_<timestamp>_<tag>.json
```

Each file includes total requests, success/error counts, latency percentiles, and Redis memory deltas before/after.