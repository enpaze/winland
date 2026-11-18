using System.Text.RegularExpressions;
using Winland.Install;

namespace Winland.Daemon.Tests;

public partial class FeatureMapTests
{
    private static readonly string Root = FindRoot();
    private static readonly IReadOnlyList<Feature> Map = Feature.Read(Path.Combine(Root, "docs", "features.md"));

    [Fact]
    public void EveryFeatureNamesItsSpecCodeAndDriver()
    {
        Assert.NotEmpty(Map);
        var problems = new List<string>();
        foreach (var feature in Map)
        {
            if (feature.Spec.Count == 0)
                problems.Add($"'{feature.Id}' needs a '- Spec:' line linking its section of docs/behavior.md.");
            if (feature.Code.Count == 0)
                problems.Add($"'{feature.Id}' needs a '- Code:' line linking the files that implement it.");
            if (feature.Driver != $"pwsh -NoProfile -File scripts/smoke.ps1 -Feature {feature.Id}")
                problems.Add($"'{feature.Id}' needs the line '- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature {feature.Id}`'.");
        }

        problems.AddRange(Map.GroupBy(feature => feature.Id).Where(group => group.Count() > 1).Select(group => $"'{group.Key}' is listed twice."));
        Assert.True(problems.Count == 0, "docs/features.md: " + string.Join(" ", problems));
    }

    [Fact]
    public void EveryCodeLinkExists()
    {
        var missing = Map
            .SelectMany(feature => feature.Code.Select(link => (feature.Id, link)))
            .Where(entry => !Path.Exists(Path.GetFullPath(Path.Combine(Root, "docs", entry.link))))
            .Select(entry => $"{entry.Id}: {entry.link}")
            .ToList();
        Assert.True(missing.Count == 0, "docs/features.md links code that does not exist. Point each link at the file that now holds the feature: " + string.Join(", ", missing));
    }

    [Fact]
    public void EverySectionOfTheBehaviorSpecBelongsToAFeature()
    {
        var headings = File.ReadAllLines(Path.Combine(Root, "docs", "behavior.md"))
            .Select(line => HeadingPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => (Level: match.Groups[1].Value.Length, Anchor: Anchor(match.Groups[2].Value)))
            .ToList();
        var anchors = headings.Select(heading => heading.Anchor).ToHashSet(StringComparer.Ordinal);
        var linked = Map.SelectMany(feature => feature.Spec).ToHashSet(StringComparer.Ordinal);

        var broken = Map
            .SelectMany(feature => feature.Spec.Where(anchor => !anchors.Contains(anchor)).Select(anchor => $"{feature.Id}: #{anchor}"))
            .ToList();
        Assert.True(broken.Count == 0, "docs/features.md links sections docs/behavior.md does not have: " + string.Join(", ", broken));

        var unclaimed = headings.Where(heading => heading.Level == 2 && !linked.Contains(heading.Anchor)).Select(heading => heading.Anchor).ToList();
        Assert.True(
            unclaimed.Count == 0,
            "These sections of docs/behavior.md belong to no feature. Link each from the '- Spec:' line of the feature that drives it, or add a feature: " + string.Join(", ", unclaimed));
    }

    [Fact]
    public void EveryDispatcherBelongsToExactlyOneFeature() =>
        AssertClaimedOnce("Dispatchers", WindowManager.DispatcherNames, feature => feature.Dispatchers);

    [Fact]
    public void EveryPipeRequestBelongsToExactlyOneFeature() =>
        AssertClaimedOnce("Requests", CommandProtocol.VerbForms.Select(form => form.Split(' ')[0]), feature => feature.Requests);

    [Fact]
    public void EveryInstallCommandBelongsToExactlyOneFeature() =>
        AssertClaimedOnce(
            "Install commands",
            typeof(InstallCommand).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(InstallCommand))).Select(type => type.Name),
            feature => feature.InstallCommands);

    [Fact]
    public void EveryFeatureHasExactlyOneDriverClass()
    {
        var folder = Path.Combine(Root, "tests", "Winland.Smoke.Tests", "Features");
        var drivers = Directory.GetFiles(folder, "*.cs")
            .Select(file => (File: Path.GetFileName(file), Text: File.ReadAllText(file)))
            .Select(driver => (driver.File, driver.Text, Ids: DriverPattern().Matches(driver.Text).Select(match => match.Groups[1].Value).ToList()))
            .ToList();

        var malformed = drivers
            .Where(driver => driver.Ids.Count != 1 || !driver.Text.Contains("SmokeFact]", StringComparison.Ordinal))
            .Select(driver => driver.File)
            .ToList();
        Assert.True(
            malformed.Count == 0,
            "Each file in tests/Winland.Smoke.Tests/Features needs exactly one [Trait(\"Feature\", \"<id>\")] naming its feature in docs/features.md, and a [SmokeFact] or [SystemSmokeFact] test: " + string.Join(", ", malformed));

        var driven = drivers.SelectMany(driver => driver.Ids).ToList();
        var ids = Map.Select(feature => feature.Id).ToList();
        var undriven = ids.Except(driven, StringComparer.Ordinal).ToList();
        var unmapped = driven.Except(ids, StringComparer.Ordinal).ToList();
        var twice = driven.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        Assert.True(undriven.Count == 0, "These features have no driver. Add tests/Winland.Smoke.Tests/Features/<Name>.cs with [Trait(\"Feature\", \"<id>\")]: " + string.Join(", ", undriven));
        Assert.True(unmapped.Count == 0, "These drivers name features docs/features.md does not list. Add the feature or fix the trait: " + string.Join(", ", unmapped));
        Assert.True(twice.Count == 0, "These features have more than one driver class. Keep one class per feature: " + string.Join(", ", twice));
    }

    private static void AssertClaimedOnce(string field, IEnumerable<string> names, Func<Feature, IReadOnlyList<string>> claims)
    {
        var known = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claimed = Map.SelectMany(feature => claims(feature).Select(name => (feature.Id, Name: name))).ToList();
        var unclaimed = known.Where(name => !claimed.Any(claim => claim.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).Order(StringComparer.Ordinal).ToList();
        var unknown = claimed.Where(claim => !known.Contains(claim.Name)).Select(claim => $"{claim.Id}: {claim.Name}").ToList();
        var twice = claimed.GroupBy(claim => claim.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => $"{group.Key} ({string.Join(", ", group.Select(claim => claim.Id))})").ToList();
        Assert.True(unclaimed.Count == 0, $"These {field} belong to no feature. Add each to the '- {field}:' line of the feature whose driver exercises it: " + string.Join(", ", unclaimed));
        Assert.True(unknown.Count == 0, $"docs/features.md claims {field} the code does not have. Remove them or use the current name: " + string.Join(", ", unknown));
        Assert.True(twice.Count == 0, $"These {field} belong to more than one feature. Keep each in one: " + string.Join(", ", twice));
    }

    private static string Anchor(string heading) =>
        AnchorPunctuation().Replace(heading.Trim().ToLowerInvariant(), "").Replace(' ', '-');

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Winland.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Winland.slnx was not found above " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"^(#{2,3}) (.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"[^\p{L}\p{N} _-]")]
    private static partial Regex AnchorPunctuation();

    [GeneratedRegex(@"\[Trait\(""Feature"", ""([^""]+)""\)\]")]
    private static partial Regex DriverPattern();

    private sealed partial record Feature(
        string Id,
        IReadOnlyList<string> Spec,
        IReadOnlyList<string> Code,
        IReadOnlyList<string> Dispatchers,
        IReadOnlyList<string> Requests,
        IReadOnlyList<string> InstallCommands,
        string Driver)
    {
        private static readonly string[] Fields = ["Spec", "Code", "Dispatchers", "Requests", "Install commands", "Driver", "Not driven"];

        public static IReadOnlyList<Feature> Read(string path)
        {
            var features = new List<Feature>();
            string? id = null;
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(path).Append("## end"))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    if (id is not null)
                        features.Add(From(id, fields));

                    id = line[3..].Trim();
                    Assert.True(IdPattern().IsMatch(id), $"docs/features.md: the heading '{line}' must be a feature id in lowercase words joined by hyphens.");
                    fields.Clear();
                    continue;
                }

                if (id is null || FieldPattern().Match(line) is not { Success: true } field)
                    continue;

                var name = field.Groups[1].Value;
                Assert.True(Fields.Contains(name), $"docs/features.md, feature '{id}': '{name}' is not a field. Use {string.Join(", ", Fields)}.");
                Assert.True(fields.TryAdd(name, field.Groups[2].Value), $"docs/features.md, feature '{id}': '{name}' appears twice.");
            }

            return features;
        }

        private static Feature From(string id, Dictionary<string, string> fields)
        {
            string Text(string name) => fields.GetValueOrDefault(name, "");
            List<string> Words(string name) => [.. WordPattern().Matches(Text(name)).Select(match => match.Groups[1].Value)];
            List<string> Links(string name, string prefix) =>
                [.. LinkPattern().Matches(Text(name)).Select(match => match.Groups[1].Value).Where(target => target.StartsWith(prefix, StringComparison.Ordinal)).Select(target => target[prefix.Length..])];
            var driver = Words("Driver");
            return new Feature(
                id,
                Links("Spec", "behavior.md#"),
                Links("Code", ""),
                Words("Dispatchers"),
                Words("Requests"),
                Words("Install commands"),
                driver.Count == 1 ? driver[0] : "");
        }

        [GeneratedRegex(@"^[a-z]+(-[a-z]+)*$")]
        private static partial Regex IdPattern();

        [GeneratedRegex(@"^- ([A-Z][a-z]+(?: [a-z]+)*): (.+)$")]
        private static partial Regex FieldPattern();

        [GeneratedRegex(@"`([^`]+)`")]
        private static partial Regex WordPattern();

        [GeneratedRegex(@"\]\(([^)\s]+)\)")]
        private static partial Regex LinkPattern();
    }
}
