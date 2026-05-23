using FluentAssertions;
using Xunit;

namespace Sentirum.Agent.Tests;

public sealed class SentirumAgentOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveSensibleDefaults()
    {
        var options = new SentirumAgentOptions();

        options.Name.Should().BeEmpty();
        options.Description.Should().BeNull();
        options.Instructions.Should().BeNull();
        options.Model.Should().BeNull();
        options.Tools.Should().BeEmpty();
        options.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Metadata_AllowsArbitraryKeys()
    {
        var options = new SentirumAgentOptions();

        options.Metadata["tenant"] = "acme";
        options.Metadata["maxTokens"] = 1024;

        options.Metadata.Should().ContainKey("tenant").WhoseValue.Should().Be("acme");
        options.Metadata.Should().ContainKey("maxTokens").WhoseValue.Should().Be(1024);
    }
}
