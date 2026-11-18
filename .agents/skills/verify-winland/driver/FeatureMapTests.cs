using System.Text.RegularExpressions;
using Winland.Application;
using Winland.Install;

namespace Winland.Verify.Tests;

[TestClass]
public partial class FeatureMapTests
{
    private const string Skill = ".agents/skills/verify-winland";
    private const string Harness = "drive.ps1";
    private static readonly string[] Sections = ["Sub-features", "How to get to it (user POV)", $"Driving it with {Harness}", "Gotchas"];
    private static readonly string[] Kinds = ["dispatcher", "request", "install"];
    private static readonly string Root = FindRoot();
    private static readonly string Folder = Path.Combine(Root, Skill, "features");
    private static readonly IReadOnlyList<Feature> Map = Feature.ReadAll(Folder);

    [TestMethod]
    public void EveryFeatureFileHasTheSkillShape()
    {
        Assert.IsNotEmpty(Map);
        var problems = new List<string>();
        foreach (var feature in Map)
        {
            var file = $"{Skill}/features/{feature.Id}.md";
            if (!IdPattern().IsMatch(feature.Id))
                problems.Add($"{file}: name the file with the feature id in lowercase words joined by hyphens.");
            if (feature.Title.Length == 0)
                problems.Add($"{file}: start with '# <Title>'.");
            if (feature.Intro.Length == 0)
                problems.Add($"{file}: follow the title with one paragraph that says what the user sees.");
            if (!feature.Headings.SequenceEqual(Sections))
                problems.Add($"{file}: use exactly the H2 sections {string.Join(", ", Sections.Select(name => $"'{name}'"))}, in that order. Found {string.Join(", ", feature.Headings.Select(name => $"'{name}'"))}.");
            problems.AddRange(Sections.Where(name => !feature.Bullets(name).Any()).Select(name => $"{file}: '## {name}' needs at least one '- ' bullet."));
            if (feature.Spec.Count == 0)
                problems.Add($"{file}: link its section of docs/behavior.md from the intro paragraph, as ../../../../docs/behavior.md#<section>.");

            var driving = feature.Section($"Driving it with {Harness}");
            if (!driving.Contains("Preconditions:"))
                problems.Add($"{file}: start '## Driving it with {Harness}' with 'Preconditions:'.");
            if (!driving.Any(line => line.Contains($"`{Command(feature.Id)}`", StringComparison.Ordinal)))
                problems.Add($"{file}: name its driver as `{Command(feature.Id)}` in '## Driving it with {Harness}'.");

            problems.AddRange(feature.SubFeatures
                .Where(sub => sub.Kind.Length > 0 && !Kinds.Contains(sub.Kind))
                .Select(sub => $"{file}: the sub-feature `{sub.Kind}:{sub.Name}` has an unknown kind. Use {string.Join(", ", Kinds.Select(kind => kind + ":"))} or a plain id."));
        }

        Assert.IsTrue(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void TheIndexListsEveryFeatureFile()
    {
        var linked = IndexLinkPattern().Matches(File.ReadAllText(Path.Combine(Folder, "README.md")))
            .Select(match => match.Groups[1].Value)
            .ToList();
        var ids = Map.Select(feature => feature.Id).ToList();
        var unlisted = ids.Except(linked, StringComparer.Ordinal).ToList();
        var missing = linked.Except(ids, StringComparer.Ordinal).ToList();
        Assert.IsTrue(unlisted.Count == 0, $"{Skill}/features/README.md does not list these features. Add '- [<Title>](./<id>.md) <what it covers>' under '## Features': " + string.Join(", ", unlisted));
        Assert.IsTrue(missing.Count == 0, $"{Skill}/features/README.md links feature files that do not exist: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void EverySectionOfTheBehaviorSpecBelongsToAFeature()
    {
        var headings = File.ReadAllLines(Path.Combine(Root, "docs", "behavior.md"))
            .Select(line => SpecHeadingPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => (Level: match.Groups[1].Value.Length, Anchor: Anchor(match.Groups[2].Value)))
            .ToList();
        var anchors = headings.Select(heading => heading.Anchor).ToHashSet(StringComparer.Ordinal);
        var linked = Map.SelectMany(feature => feature.Spec).ToHashSet(StringComparer.Ordinal);

        var broken = Map
            .SelectMany(feature => feature.Spec.Where(anchor => !anchors.Contains(anchor)).Select(anchor => $"{feature.Id}: #{anchor}"))
            .ToList();
        Assert.IsTrue(broken.Count == 0, "These feature files link sections docs/behavior.md does not have: " + string.Join(", ", broken));

        var unclaimed = headings.Where(heading => heading.Level == 2 && !linked.Contains(heading.Anchor)).Select(heading => heading.Anchor).ToList();
        Assert.IsTrue(
            unclaimed.Count == 0,
            "These sections of docs/behavior.md belong to no feature. Link each from the intro paragraph of the feature file that drives it, or add a feature: " + string.Join(", ", unclaimed));
    }

    [TestMethod]
    public void EveryDispatcherBelongsToExactlyOneFeature() =>
        AssertClaimedOnce("dispatcher", WindowManager.DispatcherNames);

    [TestMethod]
    public void EveryPipeRequestBelongsToExactlyOneFeature() =>
        AssertClaimedOnce("request", CommandProtocol.VerbForms.Select(form => form.Split(' ')[0]));

    [TestMethod]
    public void EveryInstallCommandBelongsToExactlyOneFeature() =>
        AssertClaimedOnce("install", typeof(InstallCommand).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(InstallCommand))).Select(type => type.Name));

    [TestMethod]
    public void EveryFeatureHasExactlyOneDriverClass()
    {
        var drivers = Directory.GetFiles(Path.Combine(Root, Skill, "driver", "Features"), "*.cs")
            .Select(file => (File: Path.GetFileName(file), Text: File.ReadAllText(file)))
            .Select(driver => (driver.File, driver.Text, Ids: DriverPattern().Matches(driver.Text).Select(match => match.Groups[1].Value).ToList()))
            .ToList();

        var malformed = drivers
            .Where(driver => driver.Ids.Count != 1 || !driver.Text.Contains("VerifyFact]", StringComparison.Ordinal))
            .Select(driver => driver.File)
            .ToList();
        Assert.IsTrue(
            malformed.Count == 0,
            $"Each file in {Skill}/driver/Features needs exactly one [Trait(\"Feature\", \"<id>\")] naming its feature file, and a [VerifyFact] or [SystemVerifyFact] test: " + string.Join(", ", malformed));

        var driven = drivers.SelectMany(driver => driver.Ids).ToList();
        var ids = Map.Select(feature => feature.Id).ToList();
        var undriven = ids.Except(driven, StringComparer.Ordinal).ToList();
        var unmapped = driven.Except(ids, StringComparer.Ordinal).ToList();
        var twice = driven.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        Assert.IsTrue(undriven.Count == 0, $"These features have no driver. Add {Skill}/driver/Features/<Name>.cs with [Trait(\"Feature\", \"<id>\")]: " + string.Join(", ", undriven));
        Assert.IsTrue(unmapped.Count == 0, $"These drivers name features {Skill}/features has no file for. Add the feature file or fix the trait: " + string.Join(", ", unmapped));
        Assert.IsTrue(twice.Count == 0, "These features have more than one driver class. Keep one class per feature: " + string.Join(", ", twice));
    }

    [TestMethod]
    public void ClaudeCodeLoadsTheSameSkill()
    {
        var skill = File.ReadAllText(Path.Combine(Root, Skill, "SKILL.md"));
        var copy = Path.Combine(Root, ".claude", "skills", "verify-winland", "SKILL.md");
        Assert.IsTrue(
            File.Exists(copy) && File.ReadAllText(copy) == skill,
            $"Claude Code reads .claude/skills/verify-winland/SKILL.md, a copy of {Skill}/SKILL.md, because Windows checkouts do not keep symlinks. Copy {Skill}/SKILL.md over it.");
    }

    private static string Command(string id) => $"pwsh -NoProfile -File {Skill}/{Harness} -Feature {id}";

    private static void AssertClaimedOnce(string kind, IEnumerable<string> names)
    {
        var known = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claimed = Map.SelectMany(feature => feature.SubFeatures.Where(sub => sub.Kind == kind).Select(sub => (feature.Id, sub.Name))).ToList();
        var unclaimed = known.Where(name => !claimed.Any(claim => claim.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).Order(StringComparer.Ordinal).ToList();
        var unknown = claimed.Where(claim => !known.Contains(claim.Name)).Select(claim => $"{claim.Id}: {kind}:{claim.Name}").ToList();
        var twice = claimed.GroupBy(claim => claim.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => $"{group.Key} ({string.Join(", ", group.Select(claim => claim.Id))})").ToList();
        Assert.IsTrue(unclaimed.Count == 0, $"These belong to no feature. Add '- `{kind}:<name>` <what it does>' to '## Sub-features' of the feature whose driver exercises it: " + string.Join(", ", unclaimed));
        Assert.IsTrue(unknown.Count == 0, $"These {kind}: sub-features name nothing the code has. Remove them or use the current name: " + string.Join(", ", unknown));
        Assert.IsTrue(twice.Count == 0, $"These {kind}: sub-features belong to more than one feature. Keep each in one: " + string.Join(", ", twice));
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

    [GeneratedRegex(@"^[a-z]+(-[a-z]+)*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^(#{2,3}) (.+)$")]
    private static partial Regex SpecHeadingPattern();

    [GeneratedRegex(@"[^\p{L}\p{N} _-]")]
    private static partial Regex AnchorPunctuation();

    [GeneratedRegex(@"\[Trait\(""Feature"", ""([^""]+)""\)\]")]
    private static partial Regex DriverPattern();

    [GeneratedRegex(@"^- \[[^\]]+\]\(\./([^)]+)\.md\)", RegexOptions.Multiline)]
    private static partial Regex IndexLinkPattern();

    private sealed partial record Feature(string Id, string Title, string Intro, IReadOnlyList<(string Heading, IReadOnlyList<string> Lines)> Body)
    {
        public IEnumerable<string> Headings => Body.Select(section => section.Heading);

        public IReadOnlyList<string> Spec =>
            [.. SpecLinkPattern().Matches(Intro).Select(match => match.Groups[1].Value)];

        public IEnumerable<(string Kind, string Name)> SubFeatures =>
            Bullets("Sub-features")
                .Select(line => SubFeaturePattern().Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups[1].Value.Split(':', 2))
                .Select(parts => parts.Length == 2 ? (parts[0], parts[1]) : ("", parts[0]));

        public IReadOnlyList<string> Section(string heading) =>
            Body.FirstOrDefault(section => section.Heading == heading).Lines ?? [];

        public IEnumerable<string> Bullets(string heading) =>
            Section(heading).Where(line => line.StartsWith("- ", StringComparison.Ordinal));

        public static List<Feature> ReadAll(string folder) =>
            [.. Directory.GetFiles(folder, "*.md")
                .Where(file => !Path.GetFileName(file).Equals("README.md", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(Read)];

        private static Feature Read(string path)
        {
            var lines = File.ReadAllLines(path);
            var title = lines.Length > 0 && lines[0].StartsWith("# ", StringComparison.Ordinal) ? lines[0][2..].Trim() : "";
            var intro = string.Join(' ', lines.Skip(1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)).Where(line => line.Length > 0 && !line.StartsWith('-')));
            var body = new List<(string, IReadOnlyList<string>)>();
            foreach (var (line, index) in lines.Select((line, index) => (line, index)).Where(entry => entry.line.StartsWith("## ", StringComparison.Ordinal)))
            {
                var content = lines.Skip(index + 1).TakeWhile(next => !next.StartsWith("## ", StringComparison.Ordinal)).ToList();
                body.Add((line[3..].Trim(), content));
            }

            return new Feature(Path.GetFileNameWithoutExtension(path), title, intro, body);
        }

        [GeneratedRegex(@"\]\(\.\./\.\./\.\./\.\./docs/behavior\.md#([^)\s]+)\)")]
        private static partial Regex SpecLinkPattern();

        [GeneratedRegex(@"^- `([^`]+)` ")]
        private static partial Regex SubFeaturePattern();
    }
}
