using CacheStressTester.Models;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Amazon.Runtime.Internal;
using Amazon.Runtime;

namespace CacheStressTester.Services;

public class MetricsPublisherService
{
    private readonly string _environment;

    public MetricsPublisherService(string environment) => _environment = environment.ToUpperInvariant();

    public async Task PublishAsync(CacheResult result)
    {
        if (_environment == "AWS" || _environment == "ELASTICACHE")
        {
            await PublishToCloudWatch(result);
        }
        else if (_environment == "AZURE")
        {
            await PublishToAppInsights(result);
        }
    }

    private static async Task PublishToCloudWatch(CacheResult result)
    {
        Console.WriteLine("Publishing metrics to AWS CloudWatch...");

        using (AmazonCloudWatchClient client = new AmazonCloudWatchClient())
        {
            PutMetricDataRequest request = new PutMetricDataRequest
            {
                Namespace = "Custom/CacheStressTester", // Verify this value and if it needs to be configurable
                MetricData = new List<MetricDatum>
                {
                    new MetricDatum { MetricName = "AvgLatencyMs", Value = result.AvgLatencyMs, Unit = StandardUnit.Milliseconds },
                    new MetricDatum { MetricName = "P95LatencyMs", Value = result.P95LatencyMs, Unit = StandardUnit.Milliseconds },
                    new MetricDatum { MetricName = "RequestsPerSecond", Value = result.RequestsPerSecond, Unit = StandardUnit.CountSecond },
                    new MetricDatum { MetricName = "TimeoutCount", Value = result.TimeoutCount, Unit = StandardUnit.Count },
                    new MetricDatum { MetricName = "MemoryDeltaMB", Value = result.MemoryDeltaMB, Unit = StandardUnit.Megabytes }
                }
            };

            try
            {
                await client.PutMetricDataAsync(request);
            }
            catch (AmazonCloudWatchException ex)
            {
                Console.WriteLine($"[WARN] CloudWatch API error: {ex.Message} (Status: {ex.StatusCode})");
            }
            catch (AmazonServiceException ex)
            {
                Console.WriteLine($"[WARN] AWS service error while publishing metrics: {ex.Message}");
            }
            catch (AmazonClientException ex)
            {
                Console.WriteLine($"[WARN] AWS client error (network/config): {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Unexpected error publishing metrics: {ex.Message}");
            }
        }

        Console.WriteLine("Metrics published to CloudWatch");
    }
    private static async Task PublishToAppInsights(CacheResult result)
    {
        Console.WriteLine("Publishing metrics to Azure Application Insights...");

        string? connectionString = Environment.GetEnvironmentVariable("APPINSIGHTS_CONNECTION_STRING");
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("APPINSIGHTS_CONNECTION_STRING not set, skipping");
            return;
        }

        using (var cfg = TelemetryConfiguration.CreateDefault())
        {
            cfg.ConnectionString = connectionString;

            var client = new TelemetryClient(cfg);
            
            client.GetMetric("AvgLatencyMs").TrackValue(result.AvgLatencyMs);
            client.GetMetric("P95LatencyMs").TrackValue(result.P95LatencyMs);
            client.GetMetric("RequestsPerSecond").TrackValue(result.RequestsPerSecond);
            client.GetMetric("TimeoutCount").TrackValue(result.TimeoutCount);
            client.GetMetric("MemoryDeltaMB").TrackValue(result.MemoryDeltaMB);
            client.TrackEvent("CacheStressTestCompleted");
            client.Flush();

            // Allow some time for telemetry to be transmitted before the process exits
            // This delay prevents loss of final metrics in short-lived console apps
            await Task.Delay(1000);
        }
        
        Console.WriteLine("Metrics published to App Insights");
    }
}
