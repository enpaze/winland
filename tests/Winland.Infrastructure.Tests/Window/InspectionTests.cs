using System.Text.Json;

namespace Winland.Infrastructure.Tests;
[TestClass]
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

            Assert.AreEqual("found", inspected.GetProperty("status").GetString());
            Assert.IsTrue(inspected.GetProperty("managed").GetBoolean());
            Assert.AreEqual("WinlandLimitProbe", inspected.GetProperty("class").GetString());
            Assert.AreEqual(Environment.ProcessId, inspected.GetProperty("pid").GetInt64());
            Assert.AreEqual(Path.GetFileName(Environment.ProcessPath), inspected.GetProperty("exe").GetString(), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            probe.Close();
        }
    }
}
