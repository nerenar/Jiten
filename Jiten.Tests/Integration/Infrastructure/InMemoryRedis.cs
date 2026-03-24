using System.Collections.Concurrent;
using Moq;
using StackExchange.Redis;

namespace Jiten.Parser.Tests.Integration.Infrastructure;

public static class InMemoryRedis
{
    public static IConnectionMultiplexer Create()
    {
        var store = new ConcurrentDictionary<string, RedisValue>();

        var dbMock = new Mock<IDatabase>();

        dbMock.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, TimeSpan? _, bool _, When _, CommandFlags _) =>
            {
                store[key.ToString()] = value;
                return Task.FromResult(true);
            });

        dbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) =>
            {
                store.TryGetValue(key.ToString(), out var value);
                return Task.FromResult(value);
            });

        dbMock.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, CommandFlags _) =>
            {
                var removed = store.TryRemove(key.ToString(), out RedisValue _);
                return Task.FromResult(removed);
            });

        var muxMock = new Mock<IConnectionMultiplexer>();
        muxMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(dbMock.Object);

        return muxMock.Object;
    }
}
