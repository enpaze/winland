namespace Winland.Domain;

internal static class SlotHandover
{
    public static Taken Describe(Client client, Rectangle bounds, SizeLimits limits, double? scrollingWidth = null) =>
        new(
            client.Floating,
            client.Minimized,
            bounds,
            client.Class,
            client.Title,
            client.Exe,
            client.ProcessId,
            client.InitialClass,
            client.InitialTitle,
            limits.Min,
            limits.Max,
            scrollingWidth)
        {
            TiledLimits = limits.Tiled,
        };

    public static Client Rename(Client client, WindowId next, Taken incoming) =>
        client with
        {
            Id = new ClientId(next.Value),
            Window = next,
            Class = incoming.Class,
            Title = incoming.Title,
            Exe = incoming.Exe,
            ProcessId = incoming.ProcessId,
            InitialClass = incoming.InitialClass ?? incoming.Class,
            InitialTitle = incoming.InitialTitle ?? incoming.Title,
        };

    public static void Move<TValue>(Dictionary<ClientId, TValue> map, ClientId from, ClientId to)
    {
        if (map.Remove(from, out var value))
            map[to] = value;
    }
}
