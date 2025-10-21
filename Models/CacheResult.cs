namespace CacheStressTester.Models;

public class CacheResult
{
    public string Environment { get; set; } = string.Empty;
    public int ThreadCount { get; set; }
    public int RequestsPerThread { get; set; }
    public int PayloadSizeBytes { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessCount { get; set; }
    public int TimeoutCount { get; set; }
    public int ErrorCount { get; set; }
    public double AvgLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double RequestsPerSecond { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public double UsedMemoryMB_Before { get; set; }
    public double UsedMemoryMB_After { get; set; }
    public double MemoryDeltaMB { get; set; }
    public long EvictedKeys_Before { get; set; }
    public long EvictedKeys_After { get; set; }
    public long EvictedKeysDelta { get; set; }
    public int ConnectedClients { get; set; }
    public double HitRatio { get; set; }
    public int InstantaneousOpsPerSec { get; set; }

    public RedisServerMetrics? InfoMetricsBefore { get; set; }
    public RedisServerMetrics? InfoMetricsAfter { get; set; }
}
