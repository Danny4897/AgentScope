using AgentScope.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AgentScope.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Create_SetsFreeTierAndNormalisesEmail()
    {
        var user = User.Create("Danny@Example.COM", "Danny");

        user.Email.Should().Be("danny@example.com");
        user.Tier.Should().Be(SubscriptionTier.Free);
        user.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void UpgradeTier_ChangesTier()
    {
        var user = User.Create("a@b.com", "Test");

        user.UpgradeTier(SubscriptionTier.Pro);

        user.Tier.Should().Be(SubscriptionTier.Pro);
    }
}
