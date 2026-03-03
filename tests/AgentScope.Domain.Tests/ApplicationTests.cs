using AgentScope.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AgentScope.Domain.Tests;

public class ApplicationTests
{
    [Fact]
    public void Create_IsActiveByDefault()
    {
        var app = Application.Create(Guid.NewGuid(), "My App", "api-key-123");

        app.IsActive.Should().BeTrue();
        app.Name.Should().Be("My App");
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var app = Application.Create(Guid.NewGuid(), "My App", "api-key");

        app.Deactivate();

        app.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        var app = Application.Create(Guid.NewGuid(), "Old Name", "key");

        app.Rename("New Name");

        app.Name.Should().Be("New Name");
    }
}
