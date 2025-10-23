using CacheStressTester.Helpers;
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

#region Validate runtime parameters before execution starts
if (testConfig.Threads <= 0)
{
    Console.WriteLine("[Error] Threads must be greater than zero.");
    return;
}

if (testConfig.RequestsPerThread < 0)
{
    Console.WriteLine("[Error] RequestsPerThread cannot be negative.");
    return;
}

if (testConfig.DurationSeconds < 0)
{
    Console.WriteLine("[Error] DurationSeconds cannot be negative.");
    return;
}

if (testConfig.RequestsPerThread == 0 && testConfig.DurationSeconds == 0)
{
    Console.WriteLine("[Error] You must specify either RequestsPerThread or DurationSeconds (or both).");
    return;
}

if (testConfig.Threads > 10_000)
{
    Console.WriteLine("[Warning] Very high thread count detected. Ensure your system can handle this level of concurrency.");
}

if (testConfig.PayloadSizeBytes > 5_000_000)
{
    Console.WriteLine("[Warning] Payload size is unusually large (>5MB). This may distort performance results.");
}

if (testConfig.ReadWriteRatio < 0 || testConfig.ReadWriteRatio > 1)
{
    Console.WriteLine("[Error] ReadWriteRatio must be between 0.0 and 1.0.");
    return;
}

Console.WriteLine("Configuration validated successfully. Continuing initialization...\n");
#endregion

if (string.IsNullOrEmpty(testConfig.RedisConnectionString))
{
    string redisConnectionString = string.Empty;
    if (!string.IsNullOrEmpty(testConfig.RedisSecretArn))
    {
        redisConnectionString = AwsSecretHelper.GetRedisConnectionStringAsync(testConfig.RedisSecretArn).Result;
    }

    if (string.IsNullOrEmpty(redisConnectionString))
    {
        Console.WriteLine("Redis connection string not provided, terminating");
        return;
    }

    testConfig.RedisConnectionString = redisConnectionString;
}

if (testConfig.ShowProgress)
{
    Console.WriteLine("\n--- Effective Test Configuration ---");
    Console.WriteLine($"Environment        : {testConfig.Environment}");
    Console.WriteLine($"Redis Endpoint     : {testConfig.RedisConnectionString}");
    Console.WriteLine($"Duration (seconds) : {testConfig.DurationSeconds}");
    Console.WriteLine($"Threads            : {testConfig.Threads}");
    Console.WriteLine($"Requests per thread: {testConfig.RequestsPerThread}");
    Console.WriteLine($"Payload size (B)   : {testConfig.PayloadSizeBytes}");
    Console.WriteLine($"Read/Write Ratio   : {testConfig.ReadWriteRatio}");
    Console.WriteLine($"Aggressive Mode    : {testConfig.AggressiveMode}");
    Console.WriteLine($"Publish Metrics    : {testConfig.PublishMetrics}");
    Console.WriteLine("------------------------------------\n");
}

ICacheClient client = testConfig.Environment.ToUpperInvariant() switch
{
    "AZURE" => new AzureRedisClient(testConfig.RedisConnectionString),
    _ => new RedisCacheClient(testConfig.RedisConnectionString)
};

if (!client.IsConnected)
{
    Console.WriteLine("\nConnection could not be stablished, terminating");
    return;
}

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
