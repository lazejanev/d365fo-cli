using D365FO.Core.Bridge;
using Xunit;

namespace D365FO.Core.Tests;

/// <summary>
/// <c>D365FO_BRIDGE_ENABLED=0</c> switches the bridge off for writes too. Before, the write
/// helpers asked only whether the executable existed, so <c>generate --install-to</c> created
/// objects through the live metadata provider while <c>doctor</c> reported the bridge disabled.
/// </summary>
[Collection(EnvironmentCollectionDefinition.Name)]
public sealed class BridgeDisabledFlagTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "d365fo-bridgeflag-" + Guid.NewGuid().ToString("N"));
    private readonly string? _enabled = Environment.GetEnvironmentVariable("D365FO_BRIDGE_ENABLED");
    private readonly string? _path = Environment.GetEnvironmentVariable("D365FO_BRIDGE_PATH");

    public BridgeDisabledFlagTests()
    {
        Directory.CreateDirectory(_dir);
        // An executable that exists, so only the flag can stop the call. It is not a real
        // program: if the gate let the call through, starting it would fail with a different error.
        var exe = Path.Combine(_dir, "D365FO.Bridge.exe");
        File.WriteAllText(exe, "not a program");
        Environment.SetEnvironmentVariable("D365FO_BRIDGE_PATH", exe);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("D365FO_BRIDGE_ENABLED", _enabled);
        Environment.SetEnvironmentVariable("D365FO_BRIDGE_PATH", _path);
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    public void Install_through_the_provider_is_refused_when_the_bridge_is_switched_off(string flag)
    {
        Environment.SetEnvironmentVariable("D365FO_BRIDGE_ENABLED", flag);

        var (ok, error) = BridgeGate.TrySaveObject("table", "Foo", "MyModel", "<AxTable />");

        Assert.False(ok);
        Assert.Contains("D365FO_BRIDGE_ENABLED", error);
        Assert.True(BridgeGate.ExplicitlyDisabled());
    }

    [Fact]
    public void Update_and_delete_are_refused_too_when_the_bridge_is_switched_off()
    {
        Environment.SetEnvironmentVariable("D365FO_BRIDGE_ENABLED", "0");

        Assert.Contains("D365FO_BRIDGE_ENABLED", BridgeGate.TryUpdateObject("table", "Foo", "MyModel", "<AxTable />").error);
        Assert.Contains("D365FO_BRIDGE_ENABLED", BridgeGate.TryDeleteObject("table", "Foo", "MyModel").error);
        Assert.Null(BridgeGate.TryGetModelFolder("MyModel"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("true")]
    public void An_unset_or_enabled_flag_is_not_a_switch_off(string? flag)
    {
        // Unset keeps the old behaviour (use the executable when it is there), so an
        // installation that never set the flag is not broken.
        Environment.SetEnvironmentVariable("D365FO_BRIDGE_ENABLED", flag);

        Assert.False(BridgeGate.ExplicitlyDisabled());
    }
}
