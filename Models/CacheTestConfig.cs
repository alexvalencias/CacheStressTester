namespace CacheStressTester.Models;

public class CacheTestConfig
{
    public string Environment { get; set; } = "AWS";
    public string RedisConnectionString { get; set; } = "localhost:6379";
    public string RedisSecretArn { get; set; } = string.Empty;
    public int DurationSeconds { get; set; } = 20;
    public int Threads { get; set; } = 5;
    public int RequestsPerThread { get; set; } = 200;
    public int PayloadSizeBytes { get; set; } = 256;
    public double ReadWriteRatio { get; set; } = 0.7;
    public bool PublishMetrics { get; set; } = false;
    public bool AggressiveMode { get; set; } = false;
    public bool ShowProgress { get; set; } = true;
    public string Tag { get; set; } = string.Empty;
}
