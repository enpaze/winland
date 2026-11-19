using System.Globalization;
using static Winland.Domain.OrdinalText;

namespace Winland.Domain;

internal sealed partial class DwindleLayout
{
    public WindowId? MoveFocus(WindowId active, string arguments)
    {
        if (!Sides.TryParse(Tokens.First(arguments), out var direction))
            return null;
        if (!TryGeometry(active, out var origin, out var floating))
            return null;

        if (floating)
            return DirectionalNeighbor.FindByAngle(_clients, origin, direction, active, (WindowId window, out Rectangle box) => TryGeometry(window, out box, out _));

        return Neighbor(origin, direction, active, false)
            ?? Neighbor(origin, direction, active, true);
    }

    private Func<WindowId, int> _recency = static _ => 0;

    public void RankFocus(Func<WindowId, int> recency) => _recency = recency;

    private WindowId? Neighbor(Rectangle origin, Side direction, WindowId except, bool floating) =>
        DirectionalNeighbor.Find(_clients, origin, direction, except, floating, (WindowId window, out Rectangle box) => TryGeometry(window, out box, out _), _recency);

    public void NoteFocus(WindowId window)
    {
        var id = new ClientId(window.Value);
        if (_nodes.TryGetValue(id, out var node) && InTree(node))
            _splitTarget = id;
    }

    public IReadOnlyList<Placement> MoveWindow(WindowId window, string arguments)
    {
        if (!Sides.TryParse(Tokens.First(arguments), out var direction))
            return [];

        var id = new ClientId(window.Value);
        if (Minimized(id))
            return [];

        if (_floating.Contains(id))
            return MoveFloating(id, direction);

        if (!_nodes.TryGetValue(id, out var node) || node.Parent is null || !InTree(node))
            return [];

        var focal = Sides.Focal(node.Box, direction);
        if (LeafContaining(focal) is null)
            return [];

        var towardSibling = TowardLeafSibling(node, direction);
        Detach(node);
        Refit();

        var opening = LeafContaining(focal) ?? ClosestLeaf(focal);
        if (opening is null || _root is null)
        {
            node.Parent = null;
            node.Box = TilingArea();
            _root = node;
            _splitTarget = id;
            Refit();
            return Placements();
        }

        Split(opening, node, focal, towardSibling ? direction : null);
        _splitTarget = id;
        return Placements();
    }

    public IReadOnlyList<Placement> Swap(WindowId first, WindowId second)
    {
        if (first == second)
            return [];

        var left = new ClientId(first.Value);
        var right = new ClientId(second.Value);
        if (Minimized(left) || Minimized(right))
            return [];

        if (_nodes.TryGetValue(left, out var leftNode)
            && _nodes.TryGetValue(right, out var rightNode)
            && InTree(leftNode)
            && InTree(rightNode))
        {
            leftNode.Id = right;
            rightNode.Id = left;
            _nodes[left] = rightNode;
            _nodes[right] = leftNode;
            Refit();
            return Placements();
        }

        if (_floating.TryGet(left, out var leftBox) && _floating.TryGet(right, out var rightBox))
        {
            _floating.Set(left, rightBox);
            _floating.Set(right, leftBox);
            return [new Placement(left, rightBox), new Placement(right, leftBox)];
        }

        if (TryNode(first, out var tiledNode) && _floating.TryGet(right, out var floatBox))
            return SwapAcross(tiledNode, right, floatBox);
        if (TryNode(second, out tiledNode) && _floating.TryGet(left, out floatBox))
            return SwapAcross(tiledNode, left, floatBox);

        return [];
    }

    private List<Placement> SwapAcross(Node node, ClientId floater, Rectangle box)
    {
        var tiled = node.Id!.Value;
        node.Id = floater;
        _nodes.Remove(tiled);
        _nodes[floater] = node;
        _floating.Sink(floater);
        _floating.Set(tiled, box);
        SetFloating(floater, false);
        SetFloating(tiled, true);
        if (_splitTarget == tiled)
            _splitTarget = floater;

        Refit();
        return [.. Placements(), new Placement(tiled, box)];
    }

    public IReadOnlyList<Placement> ToggleFloating(WindowId window, Point? pointer = null)
    {
        var id = new ClientId(window.Value);
        var index = IndexOf(id);
        if (index < 0 || _clients[index].Minimized)
            return [];

        if (_floating.Contains(id))
            return Tile(id, index, pointer);

        if (!_nodes.TryGetValue(id, out var node) || !InTree(node))
            return [];

        var tile = node.Box;
        var sibling = Detach(node);
        _nodes.Remove(id);
        if (_splitTarget == id)
            _splitTarget = sibling is null ? null : Leaf(sibling.Node).Id;

        var floated = FloatingLimits(id).Clamp(_floating.Around(id, tile, _options.FloatingOnScreen(_area)));
        _floating.Set(id, floated);
        _clients[index] = _clients[index] with { Floating = true };
        Refit();

        var placements = new List<Placement>(Placements()) { new(id, floated) };
        return placements;
    }

    public string? LayoutMessageError { get; private set; }

    public IReadOnlyList<Placement> LayoutMessage(WindowId? active, string arguments)
    {
        LayoutMessageError = "error: layoutmsg works in the scrolling layout";
        return [];
    }

    public IReadOnlyList<Placement> ToggleSplit(WindowId? active)
    {
        LayoutMessageError = null;
        if (!TryNode(active, out var node))
            return [];
        if (node.Parent is null || Covers(node.Id!.Value))
        {
            LayoutMessageError = "error: can't split in the current workspace";
            return [];
        }

        node.Parent.SplitTop = !node.Parent.SplitTop;
        Refit();
        return Placements();
    }

    private IReadOnlyList<Placement> Tile(ClientId id, int index, Point? pointer)
    {
        _floating.Sink(id);
        _clients[index] = _clients[index] with { Floating = false };
        var node = new Node { Id = id };
        _nodes[id] = node;

        if (_root is null)
        {
            node.Box = TilingArea();
            _root = node;
        }
        else
        {
            Split(SplitTarget() ?? Leaf(_root), node, pointer);
        }

        _splitTarget = id;
        Refit();
        return FloatIfShort(id, node);
    }

    private IReadOnlyList<Placement> MoveFloating(ClientId id, Side direction) =>
        _floating.MoveToEdge(id, direction, _area, _options.BorderFor(id));

    private bool TryGeometry(WindowId window, out Rectangle box, out bool floating)
    {
        var id = new ClientId(window.Value);
        if (_floating.TryGet(id, out box))
        {
            floating = true;
            return true;
        }

        floating = false;
        if (_nodes.TryGetValue(id, out var node) && InTree(node))
        {
            box = node.Box;
            return true;
        }

        box = default;
        return false;
    }

    private bool TryNode(WindowId? window, out Node node)
    {
        node = null!;
        if (window is not WindowId id)
            return false;
        if (!_nodes.TryGetValue(new ClientId(id.Value), out var found) || !InTree(found))
            return false;

        node = found;
        return true;
    }

    private bool Covers(ClientId id) =>
        _clients.Exists(client => client.Id == id && client.Fullscreen != FullscreenMode.None);

    private static bool TowardLeafSibling(Node node, Side direction)
    {
        var parent = node.Parent;
        if (parent is null)
            return false;

        var first = ReferenceEquals(parent.First, node);
        var sibling = first ? parent.Second : parent.First;
        if (sibling?.Id is null)
            return false;

        return direction switch
        {
            Side.Up => parent.SplitTop && !first,
            Side.Down => parent.SplitTop && first,
            Side.Left => !parent.SplitTop && !first,
            Side.Right => !parent.SplitTop && first,
            _ => false,
        };
    }
}
