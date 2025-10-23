using CacheStressTester.Enums;
using CacheStressTester.Helpers;
using CacheStressTester.Models;
using StackExchange.Redis;
using System.Diagnostics;

namespace CacheStressTester.Services;

/// <summary>
/// Responsible for executing the end-to-end Redis stress workload,
/// collecting metrics before and after the run, and producing the final result summary.
/// </summary>
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
    /// This method supports three execution modes determined by parameter combinations:
    /// <list type="bullet">
    ///   <item>
    ///     <description><b>TimedAndBounded:</b> Threads + RequestsPerThread + DurationSeconds > 0 - Runs until duration expires, even if not all requests complete.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>RequestsBounded:</b> Threads + RequestsPerThread > 0, DurationSeconds = 0 - Runs all requests with no time limit.</description>
    ///   </item>
    ///   <item>
    ///     <description><b>TimedOnly:</b> Threads + DurationSeconds > 0, RequestsPerThread = 0 - Runs continuously until the duration expires (no request limit).</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public async Task<CacheResult> RunAsync()
    {
        CacheResult result = new()
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

        // Collect baseline metrics before test execution
        try
        {
            RedisServerMetrics before = new RedisMetricsCollector(_client.GetConnection()).CaptureMetrics();
            result.UsedMemoryMB_Before = before.UsedMemoryMB;
            result.EvictedKeys_Before = before.EvictedKeys;
            result.InfoMetricsBefore = before;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to collect initial metrics: {ex.Message}");
        }

        // Prepare payloads (randomized in aggressive mode)
        byte[][] payloads = _config.AggressiveMode
            ? Enumerable.Range(0, 10).Select(_ =>
            {
                int size = _random.Next(512, _config.PayloadSizeBytes * 8);
                var p = new byte[size];
                _random.NextBytes(p);
                return p;
            }).ToArray()
            : new[] { new byte[_config.PayloadSizeBytes] };

        // Determine execution mode
        var mode = TestModeHelper.DetermineMode(_config.Threads, _config.RequestsPerThread, _config.DurationSeconds);
        if (mode == TestExecutionMode.Undefined)
            throw new InvalidOperationException("Invalid parameter combination. Threads must be > 0 and at least one of RequestsPerThread or DurationSeconds must be > 0.");

        Console.WriteLine($"[Mode] {mode} | Threads={_config.Threads}, RequestsPerThread={_config.RequestsPerThread}, DurationSeconds={_config.DurationSeconds}");

        CancellationTokenSource? cts = (_config.DurationSeconds > 0)
            ? new CancellationTokenSource(TimeSpan.FromSeconds(_config.DurationSeconds))
            : null;

        Stopwatch globalStopwatch = Stopwatch.StartNew();

        // Initialize counters and latency tracking
        List<double> latencies = new();
        int total = 0; 
        int timeouts = 0; 
        int errors = 0; 
        int success = 0;

        // Progress initialization (bar or periodic updates)
        int? totalExpected = (mode == TestExecutionMode.TimedOnly)
            ? null
            : _config.Threads * _config.RequestsPerThread;

        ConsoleProgress? progress = (totalExpected.HasValue)
            ? new ConsoleProgress(totalExpected.Value)
            : null;

        // Optional periodic console updates for TimedOnly mode
        CancellationTokenSource? progressCts = null;
        Task? progressTask = null;

        if (mode == TestExecutionMode.TimedOnly && _config.ShowProgress && _config.DurationSeconds > 5)
        {
            progressCts = new CancellationTokenSource();
            progressTask = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                int intervalSeconds = 5;

                while (!progressCts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), progressCts.Token);

                    double elapsed = sw.Elapsed.TotalSeconds;
                    double estimatedRps = total / Math.Max(elapsed, 1);
                    double remaining = Math.Max(0, _config.DurationSeconds - elapsed);

                    Console.WriteLine($"[Progress] Elapsed: {elapsed:F1}s | TotalOps: {total:N0} | RPS: {estimatedRps:F1} | Remaining: {remaining:F1}s");
                }
            }, progressCts.Token);
        }

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, _config.Threads),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _config.Threads,
                    CancellationToken = cts?.Token ?? CancellationToken.None
                },
                async (threadId, token) =>
                {
                    switch (mode)
                    {
                        case TestExecutionMode.TimedAndBounded:
                        case TestExecutionMode.RequestsBounded:
                            for (int i = 0; i < _config.RequestsPerThread; i++)
                            {
                                if (token.IsCancellationRequested) break;
                                await ExecuteOperationAsync(threadId, i, token);
                            }
                            break;

                        case TestExecutionMode.TimedOnly:
                            int opIndex = 0;
                            while (!token.IsCancellationRequested)
                            {
                                await ExecuteOperationAsync(threadId, opIndex++, token);
                            }
                            break;
                    }

                    async Task ExecuteOperationAsync(int tId, int opIndex, CancellationToken ct)
                    {
                        string key = $"stress{(string.IsNullOrEmpty(_config.Tag) ? "" : $"_{_config.Tag}")}:{tId}:{opIndex}";
                        Stopwatch requestLatency = Stopwatch.StartNew();

                        try
                        {
                            // Decide operation type based on configured Read/Write ratio (0.7 means 70% reads and 30% writes)
                            // In aggressive mode, we also inject a small percentage (~5%) of delete ops.
                            double readRatio = Math.Clamp(_config.ReadWriteRatio, 0.0, 1.0);
                            double deleteRatio = _config.AggressiveMode ? 0.05 : 0.0;
                            double writeRatio = Math.Max(0.0, 1.0 - readRatio - deleteRatio);
                            double redisOperation = _random.NextDouble();

                            if (redisOperation < readRatio)
                            {
                                // Read
                                await _client.GetAsync(key);
                            }
                            else if (redisOperation < readRatio + writeRatio)
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

                            requestLatency.Stop();

                            lock (latencies)
                            {
                                latencies.Add(requestLatency.Elapsed.TotalMilliseconds);
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

                            if (_config.ShowProgress && progress != null)
                            {
                                progress.Update(total, "Running...");
                            }

                            // Add small random delay to break CPU lockstep
                            if (_config.AggressiveMode && _random.NextDouble() < 0.1)
                            {
                                await Task.Delay(_random.Next(0, 2), token);
                            }
                        }
                    }
                });

            progress?.Complete();
        }
        catch (OperationCanceledException)
        {
            if (_config.ShowProgress)
            {
                progress?.End("Test duration reached, stopping...");
            }
        }
        finally
        {
            Console.WriteLine("Stress test completed.");
        }

        // Stop progress task if active
        if (progressTask != null && progressCts != null)
        {
            progressCts.Cancel();
            try 
            { 
                await progressTask; 
            } 
            catch (TaskCanceledException) 
            { 

            }
        }

        globalStopwatch.Stop();

        // Aggregate results
        result.TotalRequests = total; // Total number of operations/requests
        result.SuccessCount = success; // Total number of successful operations/requests
        result.TimeoutCount = timeouts; // Total number of timed-out operations/requests
        result.ErrorCount = errors; // Total number of failed operations/requests
        result.AvgLatencyMs = latencies.Count > 0 ? Math.Round(latencies.Average(), 2) : 0; // Average latency across all successful operations/requests in milliseconds
        result.P95LatencyMs = latencies.Count > 0 ? Math.Round(Percentile(latencies, 95), 2) : 0; // 95th percentile latency(P95)
        result.RequestsPerSecond = Math.Round(total / Math.Max(0.001, globalStopwatch.Elapsed.TotalSeconds), 2); // Total requests completed per second
        result.EndTimeUtc = DateTime.UtcNow; // Timestamp when the test ended

        // Capture post-test Redis metrics
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to collect final metrics: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Calculates a percentile (e.g., P95) from a collection of latency values.
    /// </summary>
    private static double Percentile(List<double> values, double percentile)
    {
        if (values.Count == 0) return 0;
        values.Sort();

        int N = values.Count;
        double n = (N - 1) * percentile / 100.0 + 1;

        if (n == 1d) return values[0];
        if (n == N) return values[N - 1];

        int k = (int)n;
        double d = n - k;
        return values[k - 1] + d * (values[k] - values[k - 1]);
    }
}
