using CacheStressTester.Models;
using CacheStressTester.Services;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Starting CacheStressTester...");

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "STRESS_")
    .AddCommandLine(args)
    .Build();

CacheTestConfig testConfig = new CacheTestConfig();
configuration.Bind("TestConfig", testConfig);
configuration.Bind(testConfig);

Console.WriteLine("\n--- Effective Test Configuration ---");
Console.WriteLine($"Environment        : {testConfig.Environment}");
Console.WriteLine($"Redis Endpoint     : {testConfig.RedisConnectionString}");
Console.WriteLine($"Duration (seconds) : {testConfig.DurationSeconds}");
Console.WriteLine($"Threads            : {testConfig.Threads}");
Console.WriteLine($"Requests per thread: {testConfig.RequestsPerThread}");
Console.WriteLine($"Payload size (B)   : {testConfig.PayloadSizeBytes}");
Console.WriteLine($"Read/Write Ratio   : {testConfig.ReadWriteRatio}");
Console.WriteLine($"Publish Metrics    : {testConfig.PublishMetrics}");
Console.WriteLine("------------------------------------\n");

ICacheClient client = testConfig.Environment.ToUpperInvariant() switch
{
    "AZURE" => new AzureRedisClient(testConfig.RedisConnectionString),
    _ => new RedisCacheClient(testConfig.RedisConnectionString)
};

await using (client)
{
    CacheStressRunner runner = new CacheStressRunner(client, testConfig);
    CacheResult result = await runner.RunAsync();
    ResultReporter.PrintSummary(result, testConfig.Tag);

    string reportDir = Path.Combine(Directory.GetCurrentDirectory(), "Reports");
    Directory.CreateDirectory(reportDir);    
    string fileName = $"result_{DateTime.UtcNow:yyyyMMdd_HHmmss}{(string.IsNullOrEmpty(testConfig.Tag) ? string.Empty : $"_{testConfig.Tag}")}.json";
    string reportPath = Path.Combine(reportDir, fileName);
    
    await ResultReporter.SaveAsync(result, reportPath);
    
    Console.WriteLine($"Report saved: {reportPath}");

    if (testConfig.PublishMetrics)
    {
        Console.WriteLine("\nPublishing metrics...");

        MetricsPublisherService publisher = new MetricsPublisherService(testConfig.Environment);

        await publisher.PublishAsync(result);
    }
}

Console.WriteLine("\nStress test completed.");
