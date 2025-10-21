using CacheStressTester.Models;
using StackExchange.Redis;
using System.Diagnostics;

namespace CacheStressTester.Services;

public class CacheStressRunner
{
    private readonly ICacheClient _client;
    private readonly CacheTestConfig _config;
    private readonly Random _random = new();

    public CacheStressRunner(ICacheClient client, CacheTestConfig config)
    {
        _client = client;
        _config = config;
    }

    /// <summary>
    /// Executes the full Redis cache stress test lifecycle asynchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method orchestrates the end-to-end process of running a configurable stress test
    /// against a Redis cache endpoint. It performs the following major steps:
    /// </para>
    ///
    /// <list type="number">
    ///   <item>
    ///     <description><b>Baseline Metrics Collection:</b> Connects to the target Redis instance
    ///     and captures initial metrics such as memory usage and evicted key count.
    ///     These serve as a baseline for post-test comparisons.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Workload Preparation:</b> Generates randomized payloads based on the
    ///     configured payload size and aggressive mode settings. Payloads are used to simulate
    ///     variable traffic loads during the test.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Stress Execution:</b> Runs concurrent operations (GET, SET, DELETE)
    ///     across multiple logical threads, respecting the configured read/write ratio and total
    ///     test duration. Each operation is timed to measure per-request latency.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Progress Tracking:</b> Displays a live progress bar in the console
    ///     showing overall completion percentage and runtime messages. Execution stops gracefully
    ///     when the configured duration is reached.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Result Aggregation:</b> Aggregates local metrics such as total requests,
    ///     successes, timeouts, and errors. Calculates key performance indicators including average
    ///     and P95 latency, and requests per second.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Post-Test Metrics Collection:</b> Reconnects to Redis and captures
    ///     memory usage, eviction delta, client connections, operations per second, and hit ratio
    ///     for comparison with the pre-test baseline.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>Result Return:</b> Returns a fully populated <see cref="CacheResult"/>
    ///     object containing all runtime statistics and Redis server metrics before and after
    ///     the test. These results can be serialized or exported for analysis.</description>
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// The test duration, thread count, payload size, and ratios are all defined by the active
    /// <see cref="CacheTestConfig"/> instance. The method supports both normal and aggressive
    /// modes, the latter introducing random payload variation, jitter, and occasional key deletions
    /// to simulate real-world high-load scenarios.
    /// </para>
    /// </remarks>
    /// <returns>
    /// A <see cref="CacheResult"/> containing aggregated performance metrics, latency statistics,
    /// and Redis server metrics before and after the test run.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the configured test duration elapses before all requests complete.
    /// </exception>
    /// <exception cref="RedisConnectionException">
    /// Thrown if a connection to the Redis server cannot be established before or after the test.
    /// </exception>
    public async Task<CacheResult> RunAsync()
    {
        CacheResult result = new CacheResult
        {
            Environment = _config.Environment,
            ThreadCount = _config.Threads,
            RequestsPerThread = _config.RequestsPerThread,
            PayloadSizeBytes = _config.PayloadSizeBytes,
            StartTimeUtc = DateTime.UtcNow
        };

        Console.WriteLine(_config.AggressiveMode
            ? "Running in AGGRESSIVE MODE (high pressure load enabled)"
            : "Running in normal mode");

        // Collect metrics before
        try
        {
            //ConnectionMultiplexer connBefore = await ConnectionMultiplexer.ConnectAsync(_config.RedisConnectionString);
            RedisServerMetrics before = new RedisMetricsCollector(_client.GetConnection()).CaptureMetrics();

            result.UsedMemoryMB_Before = before.UsedMemoryMB; // Initianl memory usage before test execution
            result.EvictedKeys_Before = before.EvictedKeys; // Number of keys evicted before test execution

            result.InfoMetricsBefore = before;

            //await connBefore.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to collect initial metrics: {ex.Message}");
        }

        // Prepare randomized payloads if aggressive mode where each array item represents an independet payload
        // otherwise, use fix payload size from configuration into a single item array payload
        byte[][] payloads = _config.AggressiveMode
            ? Enumerable.Range(0, 10) // 10 different payloads
                .Select(_ =>
                {
                    // Payload random size between 512 and 8 times the PayloadSizeBytes from configuration
                    int size = _random.Next(512, _config.PayloadSizeBytes * 8);

                    // Generate a payload with random data
                    byte[] p = new byte[size];
                    _random.NextBytes(p);
                    return p;
                })
                .ToArray()
            // Non-aggressive mode use one fixed-size payload
            : new[] { new byte[_config.PayloadSizeBytes] };

        // Initialize latency tracking and counters
        List<double> latencies = new List<double>();
        int total = 0;
        int timeouts = 0;
        int errors = 0;
        int success = 0;

        // Configure global test duration and timer
        CancellationTokenSource? cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.DurationSeconds));
        Stopwatch globalStopwatch = Stopwatch.StartNew();

        // Progress bar, can be removed
        // Initialize progress
        int totalExpected = _config.Threads * _config.RequestsPerThread;
        ConsoleProgress progress = new ConsoleProgress(totalExpected);
        // Progress bar, can be removed

        try
        {
            // Run parallel load simulation using logical threads
            await Parallel.ForEachAsync(Enumerable.Range(0, _config.Threads), new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.Threads,
                CancellationToken = cts.Token
            }, async (threadId, token) =>
            {
                for (int i = 0; i < _config.RequestsPerThread; i++)
                {
                    // Stop gracefully when time limit is reached
                    if (token.IsCancellationRequested) break;

                    // Progress bar, can be removed
                    // Update progress periodically
                    if (_config.ShowProgress)
                    {
                        int current = Interlocked.Add(ref total, 1);
                        if (current % 1000 == 0)
                        {
                            progress.Update(current, "Running...");
                        }
                    }
                    // Progress bar, can be removed

                    string key = $"stress{(string.IsNullOrEmpty(_config.Tag) ? string.Empty : $"_{_config.Tag}")}:{threadId}:{i}"; // Key for the Redis Cache item
                    Stopwatch requestLatencyStopwatch = Stopwatch.StartNew();

                    try
                    {
                        // Decide operation type based on configured Read/Write ratio (0.7 means 70% reads and 30% writes)
                        // In aggressive mode, we also inject a small percentage (~5%) of delete ops.
                        double readRatio = Math.Clamp(_config.ReadWriteRatio, 0.0, 1.0);
                        double deleteRatio = _config.AggressiveMode ? 0.05 : 0.0; // Optional aggressive delete
                        double writeRatio = 1.0 - readRatio - deleteRatio;

                        if (writeRatio < 0)
                        {
                            writeRatio = 0; // safety clamp
                        }

                        double operation = _random.NextDouble();

                        if (operation < readRatio)
                        {
                            // Read
                            await _client.GetAsync(key);
                        }
                        else if (operation < readRatio + writeRatio)
                        {
                            // Write
                            await _client.SetAsync(key, payloads[_random.Next(payloads.Length)]);
                        }
                        else if (_config.AggressiveMode && _client is RedisCacheClient redisClient)
                        {
                            // Occasional deletes in aggressive mode

                            // Get the Connection
                            var redisConn = typeof(RedisCacheClient)
                                .GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                                .GetValue(redisClient) as ConnectionMultiplexer;

                            if (redisConn != null)
                            {
                                // Perform the deletion
                                await redisConn.GetDatabase().KeyDeleteAsync(key);
                            }
                        }

                        requestLatencyStopwatch.Stop();

                        // Track latency safely across threads
                        lock (latencies)
                        {
                            latencies.Add(requestLatencyStopwatch.Elapsed.TotalMilliseconds);
                        }

                        Interlocked.Increment(ref success);
                    }
                    catch (RedisTimeoutException)
                    {
                        Interlocked.Increment(ref timeouts);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                    finally
                    {
                        Interlocked.Increment(ref total);
                    }

                    // Add small random delay to break CPU lockstep
                    if (_config.AggressiveMode && _random.NextDouble() < 0.1)
                    {
                        await Task.Delay(_random.Next(0, 2), token);
                    }
                }
            });

            // Progress bar, can be removed
            if (_config.ShowProgress)
            {
                // Normal completion
                progress.Complete();
            }
            // Progress bar, can be removed
        }
        catch (OperationCanceledException)
        {
            // Progress bar, can be removed
            if (_config.ShowProgress)
            {
                // Graceful exit on timeout or manual cancellation
                progress.End("Test duration reached, stopping...");
            }
            // Progress bar, can be removed
        }
        finally
        {
            Console.WriteLine("Stress test completed.");
        }

        globalStopwatch.Stop();

        result.TotalRequests = total; // Total number of operations/requests
        result.SuccessCount = success; // Total number of successful operations/requests
        result.TimeoutCount = timeouts; // Total number of timed-out operations/requests
        result.ErrorCount = errors; // Total number of failed operations/requests
        result.AvgLatencyMs = latencies.Count > 0 ? Math.Round(latencies.Average(), 2) : 0; // Average latency across all successful operations/requests in milliseconds
        result.P95LatencyMs = latencies.Count > 0 ? Math.Round(Percentile(latencies, 95), 2) : 0; // 95th percentile latency(P95)
        result.RequestsPerSecond = Math.Round(total / Math.Max(0.001, globalStopwatch.Elapsed.TotalSeconds), 2); // Total requests completed per second
        result.EndTimeUtc = DateTime.UtcNow; // Timestamp when the test ended

        // Collect metrics after
        try
        {
            //ConnectionMultiplexer connAfter = await ConnectionMultiplexer.ConnectAsync(_config.RedisConnectionString);
            RedisServerMetrics after = new RedisMetricsCollector(_client.GetConnection()).CaptureMetrics();

            result.UsedMemoryMB_After = after.UsedMemoryMB; // Memory usage after the test
            result.EvictedKeys_After = after.EvictedKeys; // Evicted keys since start
            result.ConnectedClients = after.ConnectedClients; // Current connected clients
            result.InstantaneousOpsPerSec = (int)after.InstantaneousOpsPerSec; // Instantaneous operations per second reported by Redis (native metric)
            result.HitRatio = after.HitRatio; // Cache hit ratio observed (hits / total lookups)
            result.MemoryDeltaMB = Math.Round(result.UsedMemoryMB_After - result.UsedMemoryMB_Before, 2); // Memory growth during test
            result.EvictedKeysDelta = result.EvictedKeys_After - result.EvictedKeys_Before; // Evicted keys delta (how many keys were evicted during the stress test)

            result.InfoMetricsAfter = after;

            //await connAfter.CloseAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to collect final metrics: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Calculates the specified percentile (e.g., P95 or P99) from a collection of numeric values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method computes the percentile using linear interpolation between values when
    /// the desired rank falls between two elements. It assumes the input list represents
    /// a sample of latency or duration values (in milliseconds) collected during the test.
    /// </para>
    ///
    /// <para>
    /// The algorithm:
    /// <list type="number">
    ///   <item>
    ///     <description>Sorts the list in ascending order.</description>
    ///   </item>
    ///   <item>
    ///     <description>Computes the rank position <c>n = (N - 1) * percentile / 100 + 1</c>,
    ///     where <c>N</c> is the number of values.</description>
    ///   </item>
    ///   <item>
    ///     <description>If <c>n</c> is not an integer, interpolates linearly between the
    ///     nearest ranked values to obtain a precise percentile estimate.</description>
    ///   </item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// This approach aligns with the Excel and NIST definition of percentiles and
    /// avoids step-function bias seen in simpler integer-based methods.
    /// </para>
    /// </remarks>
    /// <param name="values">A list of numeric values from which to compute the percentile.</param>
    /// <param name="percentile">
    /// The desired percentile (e.g., 50 for median, 95 for P95 latency).
    /// </param>
    /// <returns>
    /// The interpolated percentile value. Returns 0 if the list is empty.
    /// </returns>
    private static double Percentile(List<double> values, double percentile)
    {
        if (values.Count == 0) return 0; // No data return 0

        // Sort values ascending to position them by rank
        values.Sort();

        int N = values.Count;

        // Calculate the rank position for the desired percentile
        // Formula: n = (N - 1) * p/100 + 1
        double n = (N - 1) * percentile / 100.0 + 1;

        if (n == 1d)
        {
            return values[0]; // If percentile is the minimum (P0)
        }
        else if (n == N)
        {
            return values[N - 1]; // If percentile is the maximum (P100)
        }
        else
        {
            // Find the two surrounding ranks (k and k+1)
            int k = (int)n;
            double d = n - k;

            // Linear interpolation between values[k-1] and values[k]
            return values[k - 1] + d * (values[k] - values[k - 1]);
        }
    }

}
