namespace Winland.Application;

public sealed partial class WindowManager
{
    private readonly HashSet<WindowId> _renamed = [];

    private List<Placement> MatchRulesAgain()
    {
        var placements = new List<Placement>();
        if (_renamed.Count == 0)
            return placements;

        foreach (var desk in _workspaces)
        {
            var changed = false;
            foreach (var client in desk.Layout.Clients.Concat(desk.WaitingClients))
            {
                if (!_renamed.Contains(client.Window) || _props.Subject(client.Window) is not { } kept)
                    continue;

                var traits = kept.Traits with { Class = client.Class, Title = client.Title };
                var decision = WindowRuleParser.Decide(traits, JoinRules(_config.WindowRules, kept.Followed));
                var border = _props.RuleProp(client.Window, "border_size");
                _props.Rematched(client.Window, decision);
                changed |= !Equals(border, _props.RuleProp(client.Window, "border_size"));
            }

            if (changed && _workspaces.Shown.IsShown(desk.Id))
                placements.AddRange(desk.Layout.Apply(desk.Options));
        }

        _renamed.Clear();
        return placements;
    }
}
