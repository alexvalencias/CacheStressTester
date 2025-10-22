using StackExchange.Redis;

namespace CacheStressTester.Services;

public class AzureRedisClient : ICacheClient
{
    private readonly ConnectionMultiplexer _connection;
    private readonly IDatabase _db;

    public bool IsConnected => _connection.IsConnected;

    public AzureRedisClient(string connectionString)
    {
        _connection = ConnectionMultiplexer.Connect(connectionString);
        _db = _connection.GetDatabase();
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
