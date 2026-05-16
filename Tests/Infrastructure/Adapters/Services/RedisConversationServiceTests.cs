using Application.Dto.Conversation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Web.Infrastructure.Adapters.Services;
using Web.Infrastructure.Config.Settings;
using Xunit;

namespace Tests.Infrastructure.Adapters.Services;

public class RedisConversationServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<RedisConversationService>> _loggerMock;
    private readonly IOptions<RedisSettings> _settings;

    public RedisConversationServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisConversationService>>();
        
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);

        _settings = Options.Create(new RedisSettings
        {
            Configuration = "localhost",
            InstanceName = "test",
            ConversationTtlMinutes = 60,
            KeyPrefix = "chat"
        });
    }

    [Fact]
    public async Task GetOrCreateAsync_HandleRedisError_ReturnsNewContext()
    {
        var service = new RedisConversationService(_redisMock.Object, _settings, _loggerMock.Object);
        _dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
               .ThrowsAsync(new RedisException("Connection failed"));

        var result = await service.GetOrCreateAsync("sess_123");

        Assert.NotNull(result);
        Assert.Equal("sess_123", result.SessionId);
        Assert.Empty(result.History);
    }

    [Fact]
    public async Task SaveAsync_HandleRedisError_DoesNotThrow()
    {
        var service = new RedisConversationService(_redisMock.Object, _settings, _loggerMock.Object);
        _dbMock.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
               .ThrowsAsync(new RedisException("Write failed"));

        var context = new ConversationContext { SessionId = "sess_123" };
        
        // Should not throw exception, should just log error (Graceful degradation)
        var exception = await Record.ExceptionAsync(() => service.SaveAsync(context));
        Assert.Null(exception);
    }
}
