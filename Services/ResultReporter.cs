using CacheStressTester.Models;
using System.Text.Json;

namespace CacheStressTester.Services;

public static class ResultReporter
{
    public static async Task SaveAsync(CacheResult result, string filePath)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
    public static void PrintSummary(CacheResult result, string tag)
    {
        Console.WriteLine("\n--- Stress Test Summary ---");
        Console.WriteLine($"Run Tag        : {tag}");
        Console.WriteLine($"Total Requests : {result.TotalRequests}");
        Console.WriteLine($"Success Count  : {result.SuccessCount}");
        Console.WriteLine($"Timeout Count  : {result.TimeoutCount}");
        Console.WriteLine($"Error Count    : {result.ErrorCount}");
        Console.WriteLine($"Avg Latency    : {result.AvgLatencyMs:F2} ms");
        Console.WriteLine($"P95 Latency    : {result.P95LatencyMs:F2} ms\n");
    }
}
