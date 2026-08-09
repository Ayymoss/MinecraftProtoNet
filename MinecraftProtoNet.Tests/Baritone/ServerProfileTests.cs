using FluentAssertions;
using MinecraftProtoNet.Baritone.Settings;
using Xunit;

namespace MinecraftProtoNet.Tests.Baritone;

/// <summary>
/// Guards the permissions Baritone runs with per server.
///
/// The cost of getting this wrong is not a crash but a bot that visibly mines the scenery on a public server:
/// the pathfinder costs a route through a block it may not break, walks to it, swings until the movement times
/// out, and re-plans — drifting away from its goal the whole time.
/// </summary>
public class ServerProfileTests
{
    [Theory]
    [InlineData("mc.hypixel.net")]
    [InlineData("hypixel.net")]
    [InlineData("HYPIXEL.NET")]
    public void For_Hypixel_ForbidsBuilding(string host)
    {
        var profile = ServerProfiles.For(host);

        profile.Name.Should().Be("hypixel");
        profile.AllowBreak.Should().BeFalse();
        profile.AllowPlace.Should().BeFalse();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.10.1.5")]
    [InlineData("localhost")]
    [InlineData(null)]
    public void For_UnlistedHost_KeepsVanillaPermissions(string? host)
    {
        var profile = ServerProfiles.For(host);

        profile.Should().BeSameAs(ServerProfiles.Default);
        profile.AllowBreak.Should().BeTrue();
        profile.AllowPlace.Should().BeTrue();
    }

    /// <summary>
    /// A host that merely CONTAINS a known suffix is not that server — "nothypixel.net.example.com" must not
    /// inherit Hypixel's profile, and more importantly a lookalike must not be granted permissions by accident.
    /// </summary>
    [Fact]
    public void For_LookalikeHost_DoesNotMatch()
    {
        ServerProfiles.For("hypixel.net.example.com").Should().BeSameAs(ServerProfiles.Default);
    }

    [Fact]
    public void Apply_WritesPermissionsIntoLiveSettings()
    {
        try
        {
            ServerProfiles.Apply(ServerProfiles.For("mc.hypixel.net"));

            var settings = MinecraftProtoNet.Baritone.Core.Baritone.Settings();
            settings.AllowBreak.Value.Should().BeFalse();
            settings.AllowPlace.Value.Should().BeFalse();
        }
        finally
        {
            // Settings are process-global, so leaving them off would silently disarm other tests.
            ServerProfiles.Apply(ServerProfiles.Default);
        }
    }
}
