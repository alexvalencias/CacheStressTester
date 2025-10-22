using StackExchange.Redis;
using System.Data.Common;

namespace CacheStressTester.Services;

public interface ICacheClient : IAsyncDisposable
{
    bool IsConnected { get; }

    Task SetAsync(string key, byte[] payload);
    Task<byte[]?> GetAsync(string key);
    ConnectionMultiplexer GetConnection();
}
