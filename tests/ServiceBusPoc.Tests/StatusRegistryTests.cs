using Microsoft.Extensions.Logging;
using ServiceBusPoc.Core.Dashboard;
using ServiceBusPoc.Dashboard.Status;

namespace ServiceBusPoc.Tests;

public sealed class StatusRegistryTests
{
    [Fact]
    public void RecordHeartbeat_NullHeartbeat_Throws()
    {
        var registry = new StatusRegistry(Mock.Of<ILogger<StatusRegistry>>());

        Assert.Throws<ArgumentNullException>(() => registry.RecordHeartbeat(null!));
    }

    [Fact]
    public void GetServiceStatuses_RecentHeartbeat_PreservesReportedStateAndDetails()
    {
        var sentAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        var heartbeat = new ServiceHeartbeat
        {
            ServiceName = "insurance",
            SubscriptionName = "insurance",
            State = ServiceState.Running,
            SentAt = sentAt,
            MessagesHandled = 4,
            LastMessageAt = sentAt.AddMilliseconds(-100),
            LastEventId = "event-1"
        };
        var registry = new StatusRegistry(Mock.Of<ILogger<StatusRegistry>>());

        registry.RecordHeartbeat(heartbeat);

        var status = Assert.Single(registry.GetServiceStatuses());
        Assert.Equal("insurance", status.ServiceName);
        Assert.Equal("insurance", status.SubscriptionName);
        Assert.Equal(ServiceState.Running, status.State);
        Assert.Equal(sentAt, status.LastHeartbeatAt);
        Assert.Equal(4, status.MessagesHandled);
        Assert.Equal(heartbeat.LastMessageAt, status.LastMessageAt);
        Assert.Equal("event-1", status.LastEventId);
    }

    [Fact]
    public void GetServiceStatuses_ExpiredHeartbeat_MarksServiceOffline()
    {
        var sentAt = DateTimeOffset.UtcNow.AddSeconds(-30);
        var registry = new StatusRegistry(Mock.Of<ILogger<StatusRegistry>>());

        registry.RecordHeartbeat(new ServiceHeartbeat
        {
            ServiceName = "producer",
            State = ServiceState.Running,
            SentAt = sentAt
        });

        var status = Assert.Single(registry.GetServiceStatuses());
        Assert.Equal(ServiceState.Offline, status.State);
        Assert.Equal(sentAt, status.LastHeartbeatAt);
    }

    [Fact]
    public void RecordHeartbeat_SameService_ReplacesPreviousHeartbeat()
    {
        var registry = new StatusRegistry(Mock.Of<ILogger<StatusRegistry>>());
        registry.RecordHeartbeat(new ServiceHeartbeat
        {
            ServiceName = "producer",
            State = ServiceState.Starting,
            SentAt = DateTimeOffset.UtcNow.AddSeconds(-30),
            MessagesHandled = 1
        });
        registry.RecordHeartbeat(new ServiceHeartbeat
        {
            ServiceName = "producer",
            State = ServiceState.Running,
            SentAt = DateTimeOffset.UtcNow,
            MessagesHandled = 2
        });

        var status = Assert.Single(registry.GetServiceStatuses());
        Assert.Equal(ServiceState.Running, status.State);
        Assert.Equal(2, status.MessagesHandled);
    }
}
