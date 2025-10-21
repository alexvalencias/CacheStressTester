using StackExchange.Redis;
using CacheStressTester.Models;
using System.Net;

namespace CacheStressTester.Services;

public class RedisMetricsCollector
{
    private readonly ConnectionMultiplexer _connection;

    public RedisMetricsCollector(ConnectionMultiplexer connection) => _connection = connection;

    /// <summary>
    /// Captures real-time Redis server metrics using the <c>INFO</c> command.
    /// </summary>
    /// <remarks>
    /// This method connects to the first available Redis endpoint, retrieves diagnostic
    /// information, and extracts key performance indicators (memory usage, clients, ops/sec, etc.).
    /// 
    /// Values returned correspond to the state at the time of execution — they are not averaged.
    /// Metrics like <see cref="RedisServerMetrics.HitRatio"/> are derived from hit/miss counts
    /// since the Redis instance started.
    /// </remarks>
    /// <returns>
    /// A <see cref="RedisServerMetrics"/> instance populated with Redis server statistics.
    /// If the connection or metric retrieval fails, returns a default (zeroed) object.
    /// </returns>
    public RedisServerMetrics CaptureMetrics()
    {
        // Initialize a default metrics container to return in case of failure
        RedisServerMetrics metrics = new RedisServerMetrics();

        try
        {
            // Get the first available endpoint from the connection multiplexer
            EndPoint? endpoint = _connection.GetEndPoints().FirstOrDefault();

            if (endpoint == null)
            {
                return metrics; // No endpoints found return default metrics.
            }

            // Obtain a server interface for the selected endpoint
            IServer server = _connection.GetServer(endpoint);

            // The INFO command returns grouped sections of key-value pairs.
            // Flatten all sections into a single dictionary for easier access.
            IEnumerable<IGrouping<string, KeyValuePair<string, string>>> info = server.Info();
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var group in info)
            {
                if (group.Key.Equals("Modules", StringComparison.OrdinalIgnoreCase))
                {
                    int index = 0;
                    foreach (var pair in group)
                    {
                        if (pair.Key.Equals("module", StringComparison.OrdinalIgnoreCase))
                        {
                            index++;
                            dict[$"{group.Key}:{pair.Key}:{index.ToString().PadLeft(2,'0')}"] = pair.Value;
                        }
                        else
                        {
                            dict[$"{group.Key}:{pair.Key}"] = pair.Value;
                        }
                    }
                }
                else
                {
                    foreach (var pair in group)
                    {
                        dict[$"{group.Key}:{pair.Key}"] = pair.Value;
                    }
                }
            }

            // Parse and map relevant Redis metrics.
            if (dict.TryGetValue("used_memory", out string? usedMem))
            {
                metrics.UsedMemoryMB = Math.Round(Convert.ToDouble(usedMem) / 1024 / 1024, 2);
            }

            if (dict.TryGetValue("connected_clients", out string? cc))
            {
                metrics.ConnectedClients = Convert.ToInt32(cc);
            }

            if (dict.TryGetValue("evicted_keys", out string? ev))
            {
                metrics.EvictedKeys = Convert.ToInt64(ev);
            }

            if (dict.TryGetValue("instantaneous_ops_per_sec", out string? ops))
            {
                metrics.InstantaneousOpsPerSec = Convert.ToInt64(ops);
            }

            if (dict.TryGetValue("keyspace_hits", out string? h))
            {
                metrics.KeyspaceHits = Convert.ToInt64(h);
            }

            if (dict.TryGetValue("keyspace_misses", out string? m))
            {
                metrics.KeyspaceMisses = Convert.ToInt64(m);
            }

            // Compute the cache hit ratio (hits / total lookups)
            long totalLookups = metrics.KeyspaceHits + metrics.KeyspaceMisses;
            if (totalLookups == 0)
            {
                metrics.HitRatio = 0;
            }
            else
            {
                metrics.HitRatio = Math.Round((double)metrics.KeyspaceHits / totalLookups, 4);
            }
        }
        catch (Exception ex)
        {
            // Exceptions are swallowed intentionally to avoid failing the stress test run
            Console.WriteLine($"[WARN] Unable to capture Redis metrics: {ex.Message}");
        }

        return metrics;
    }

}
