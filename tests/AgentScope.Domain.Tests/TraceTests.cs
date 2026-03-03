using AgentScope.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AgentScope.Domain.Tests;

public class TraceTests
{
    [Fact]
    public void Create_HasRunningStatus()
    {
        var trace = Trace.Create(Guid.NewGuid(), "abc123", "MyAgent", DateTimeOffset.UtcNow);

        trace.Status.Should().Be(TraceStatus.Running);
        trace.EndedAt.Should().BeNull();
        trace.DurationMs.Should().Be(0);
    }

    [Fact]
    public void Complete_SetsStatusAndEndTime()
    {
        var started = DateTimeOffset.UtcNow;
        var trace = Trace.Create(Guid.NewGuid(), "abc", "Root", started);

        var ended = started.AddMilliseconds(500);
        trace.Complete(TraceStatus.Success, ended);

        trace.Status.Should().Be(TraceStatus.Success);
        trace.EndedAt.Should().Be(ended);
        trace.DurationMs.Should().Be(500);
    }
}
