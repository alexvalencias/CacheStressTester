using StackExchange.Redis;

namespace CacheStressTester.Services;

public class RedisCacheClient : ICacheClient
{
    private readonly ConnectionMultiplexer _connection;
    private readonly IDatabase _db;

    public bool IsConnected => _connection == null ? false : _connection.IsConnected;

    public RedisCacheClient(string connectionString)
    {
        try
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AllowAdmin = true;
            // For localhost test only to force some timeouts
            if (connectionString.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                connectionString.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                options.SyncTimeout = 100;       // ms
                options.AsyncTimeout = 100;      // ms
                options.ConnectTimeout = 100;    // ms
            }
            // For localhost test only

            _connection = ConnectionMultiplexer.Connect(options);
            _db = _connection.GetDatabase();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to connect to Redis instance: {ex.Message}");
        }
    }
    public async Task SetAsync(string key, byte[] payload) => await _db.StringSetAsync(key, payload);
    public async Task<byte[]?> GetAsync(string key)
    {
        var val = await _db.StringGetAsync(key);
        return val.HasValue ? (byte[]?)val : null;
    }
    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    public ConnectionMultiplexer GetConnection() => _connection;
}
