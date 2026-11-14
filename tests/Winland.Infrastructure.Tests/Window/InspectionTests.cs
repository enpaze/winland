using System.Text.Json;

namespace Winland.Infrastructure.Tests;

[Collection(DesktopCollection.Name)]
public class InspectionTests
{
    [DesktopFact]
    public void AClientIsInspectedFromWhatItsWindowSaysNow()
    {
        _ = ProbeNative.SetProcessDpiAwarenessContext(new IntPtr(-4));
        using var probe = new LimitProbe();
        probe.Open();
        try
        {
            var core = new WindowManager();
            using var adapter = new WindowAdapter(core);
            core.ReducedMotion = () => true;
            adapter.AdoptExisting([probe.Anchor]);

            var inspected = JsonElement.Parse(core.Execute("j/inspect address:" + new WindowId(probe.Anchor.ToInt64()).Address).Text);

            Assert.Equal("found", inspected.GetProperty("status").GetString());
            Assert.True(inspected.GetProperty("managed").GetBoolean());
            Assert.Equal("WinlandLimitProbe", inspected.GetProperty("class").GetString());
            Assert.Equal(Environment.ProcessId, inspected.GetProperty("pid").GetInt64());
            Assert.Equal(Path.GetFileName(Environment.ProcessPath), inspected.GetProperty("exe").GetString(), ignoreCase: true);
        }
        finally
        {
            probe.Close();
        }
    }
}
