namespace CacheStressTester.Models;

public class RedisServerMetrics
{
    public double UsedMemoryMB { get; set; }
    public int ConnectedClients { get; set; }
    public long EvictedKeys { get; set; }
    public long InstantaneousOpsPerSec { get; set; }
    public long KeyspaceHits { get; set; }
    public long KeyspaceMisses { get; set; }
    public double HitRatio { get; set; }
    public override string ToString()
        => $"UsedMemoryMB: {UsedMemoryMB}, Clients: {ConnectedClients}, EvictedKeys: {EvictedKeys}, Ops/s: {InstantaneousOpsPerSec}, HitRatio: {HitRatio}";
}
