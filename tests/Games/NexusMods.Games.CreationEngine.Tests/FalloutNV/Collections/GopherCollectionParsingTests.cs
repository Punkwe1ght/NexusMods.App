using System.Text.Json;
using FluentAssertions;
using NexusMods.Abstractions.Collections.Json;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV.Collections;

/// <summary>
/// Validates that representative Gopher-style collection.json fixtures parse correctly
/// and that mod source types, FOMOD choices, modRules, and file classification all behave
/// as expected for the FNV installer pipeline.
/// </summary>
public class GopherCollectionParsingTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public GopherCollectionParsingTests(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
    }

    private CollectionRoot LoadFixture(string fixtureName)
    {
        var assembly = typeof(GopherCollectionParsingTests).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fixtureName, StringComparison.OrdinalIgnoreCase));

        resourceName.Should().NotBeNull($"fixture {fixtureName} should exist as embedded resource");

        using var stream = assembly.GetManifestResourceStream(resourceName!);
        stream.Should().NotBeNull();

        var root = JsonSerializer.Deserialize<CollectionRoot>(stream!, _jsonOptions);
        root.Should().NotBeNull();
        return root!;
    }

    // === Fixture Parsing ===

    [Theory]
    [InlineData("gopher-stable-nv.json")]
    [InlineData("gopher-remaster.json")]
    [InlineData("gopher-darnified-ui.json")]
    [InlineData("gopher-qol-tweaks.json")]
    public void FixturesParsesWithoutError(string fixture)
    {
        var root = LoadFixture(fixture);
        root.Info.Should().NotBeNull();
        root.Info.Author.Should().Be("Gopher");
        root.Mods.Should().NotBeEmpty();
    }

    // === Source Type Routing ===

    [Theory]
    [InlineData("gopher-stable-nv.json", 2)]   // xNVSE + 4GB patcher
    [InlineData("gopher-remaster.json", 0)]
    [InlineData("gopher-darnified-ui.json", 0)]
    [InlineData("gopher-qol-tweaks.json", 0)]
    public void ExternalDownloadsIdentifiedCorrectly(string fixture, int expectedBrowseCount)
    {
        var root = LoadFixture(fixture);
        var browseCount = root.Mods.Count(m => m.Source.Type == ModSourceType.Browse);
        browseCount.Should().Be(expectedBrowseCount);
    }

    [Theory]
    [InlineData("gopher-remaster.json", 1)]  // Compatibility Patches
    [InlineData("gopher-qol-tweaks.json", 1)] // INI Tweaks
    public void BundledModsIdentifiedCorrectly(string fixture, int expectedBundleCount)
    {
        var root = LoadFixture(fixture);
        var bundleCount = root.Mods.Count(m => m.Source.Type == ModSourceType.Bundle);
        bundleCount.Should().Be(expectedBundleCount);
    }

    [Theory]
    [InlineData("gopher-stable-nv.json")]
    [InlineData("gopher-remaster.json")]
    [InlineData("gopher-darnified-ui.json")]
    [InlineData("gopher-qol-tweaks.json")]
    public void AllModsHaveValidSourceType(string fixture)
    {
        var root = LoadFixture(fixture);
        foreach (var mod in root.Mods)
        {
            mod.Source.Type.Should().BeOneOf(
                ModSourceType.NexusMods,
                ModSourceType.Bundle,
                ModSourceType.Browse,
                ModSourceType.Direct);
        }
    }

    // === FOMOD Handling ===

    [Fact]
    public void DarnifiedUiFomodChoicesParseSuccessfully()
    {
        var root = LoadFixture("gopher-darnified-ui.json");
        var darnifiedMod = root.Mods.First(m => m.Name == "DarNified UI");

        darnifiedMod.Choices.Should().NotBeNull();
        darnifiedMod.Choices!.Type.Should().Be(ChoicesType.fomod);
        darnifiedMod.Choices.Options.Should().HaveCount(3,
            "DarNified UI FOMOD has three steps: Font, HUD, Dialog");

        darnifiedMod.Choices.Options[0].name.Should().Be("Font Options");
        darnifiedMod.Choices.Options[1].name.Should().Be("HUD Layout");
        darnifiedMod.Choices.Options[2].name.Should().Be("Dialog Style");
    }

    [Fact]
    public void RemasterFomodChoicesParseSuccessfully()
    {
        var root = LoadFixture("gopher-remaster.json");
        var fomodMods = root.Mods.Where(m => m.Choices is not null).ToArray();

        fomodMods.Should().HaveCount(2, "ILO and EVE have FOMOD choices");
        fomodMods.Select(m => m.Name).Should().Contain("Interior Lighting Overhaul");
        fomodMods.Select(m => m.Name).Should().Contain("EVE - Essential Visual Enhancements");
    }

    [Fact]
    public void ModsWithoutFomodHaveNullChoices()
    {
        var root = LoadFixture("gopher-stable-nv.json");
        root.Mods.Should().OnlyContain(m => m.Choices == null,
            "Stable NV collection has no FOMOD installers");
    }

    // === Plugin Ordering (ModRules) ===

    [Theory]
    [InlineData("gopher-stable-nv.json")]
    [InlineData("gopher-remaster.json")]
    [InlineData("gopher-darnified-ui.json")]
    [InlineData("gopher-qol-tweaks.json")]
    public void ModRulesParseWithValidTypes(string fixture)
    {
        var root = LoadFixture(fixture);
        root.ModRules.Should().NotBeEmpty();

        foreach (var rule in root.ModRules)
        {
            rule.Type.Should().BeOneOf(
                VortexModRuleType.Before,
                VortexModRuleType.After);
            rule.Source.Should().NotBeNull();
            rule.Other.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("gopher-stable-nv.json")]
    [InlineData("gopher-remaster.json")]
    [InlineData("gopher-darnified-ui.json")]
    [InlineData("gopher-qol-tweaks.json")]
    public void ModRulesProduceValidTopologicalOrder(string fixture)
    {
        var root = LoadFixture(fixture);

        // Build directed graph from before/after rules
        // before: source comes before reference (source -> reference edge)
        // after: source comes after reference (reference -> source edge)
        var modNames = root.Mods.Select(m => m.Name).ToHashSet();
        var edges = new List<(string from, string to)>();

        foreach (var rule in root.ModRules)
        {
            var sourceName = rule.Source.LogicalFileName;
            var refName = rule.Other.LogicalFileName;
            if (sourceName is null || refName is null) continue;

            switch (rule.Type)
            {
                case VortexModRuleType.Before:
                    edges.Add((sourceName, refName));
                    break;
                case VortexModRuleType.After:
                    edges.Add((refName, sourceName));
                    break;
            }
        }

        // Kahn's algorithm for cycle detection
        var inDegree = new Dictionary<string, int>();
        var adjacency = new Dictionary<string, List<string>>();
        var allNodes = edges.SelectMany(e => new[] { e.from, e.to }).Distinct().ToList();

        foreach (var node in allNodes)
        {
            inDegree[node] = 0;
            adjacency[node] = [];
        }

        foreach (var (from, to) in edges)
        {
            adjacency[from].Add(to);
            inDegree[to]++;
        }

        var queue = new Queue<string>(allNodes.Where(n => inDegree[n] == 0));
        var sorted = new List<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            sorted.Add(node);
            foreach (var neighbor in adjacency[node])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0) queue.Enqueue(neighbor);
            }
        }

        sorted.Should().HaveCount(allNodes.Count,
            "topological sort should process all nodes (no cycles)");
    }

    [Fact]
    public void BeforeAfterConstraintsAreConsistent()
    {
        var root = LoadFixture("gopher-darnified-ui.json");

        // UIO before DarNified UI
        var uioBefore = root.ModRules.FirstOrDefault(r =>
            r.Type == VortexModRuleType.Before &&
            r.Source.LogicalFileName == "UIO - User Interface Organizer" &&
            r.Other.LogicalFileName == "DarNified UI");

        uioBefore.Should().NotBeNull("UIO should load before DarNified UI");

        // DarNified UI after MCM
        var darnAfterMcm = root.ModRules.FirstOrDefault(r =>
            r.Type == VortexModRuleType.After &&
            r.Source.LogicalFileName == "DarNified UI" &&
            r.Other.LogicalFileName == "The Mod Configuration Menu");

        darnAfterMcm.Should().NotBeNull("DarNified UI should load after MCM");
    }

    // === xNVSE Plugin Detection ===

    [Fact]
    public void StableNvDetectsNvsePlugins()
    {
        var root = LoadFixture("gopher-stable-nv.json");
        var nvsePluginMods = root.Mods.Where(m =>
            m.Hashes.Any(h => h.Path.ToString().Contains("NVSE/Plugins/") &&
                              h.Path.ToString().EndsWith(".dll"))).ToArray();

        nvsePluginMods.Should().HaveCountGreaterOrEqualTo(4,
            "NVAC, NVTF, JIP LN, Johnny Guitar, Mod Limit Fix are all NVSE plugins");
    }

    [Fact]
    public void QolTweaksDetectsStewiePluginAndIni()
    {
        var root = LoadFixture("gopher-qol-tweaks.json");
        var stewie = root.Mods.First(m => m.Name.Contains("Stewie"));

        var hasNvseDll = stewie.Hashes.Any(h =>
            h.Path.ToString().Contains("NVSE/Plugins/") &&
            h.Path.ToString().EndsWith(".dll"));
        hasNvseDll.Should().BeTrue("Stewie's Tweaks has an NVSE plugin DLL");

        var hasNvseIni = stewie.Hashes.Any(h =>
            h.Path.ToString().Contains("NVSE/Plugins/") &&
            h.Path.ToString().EndsWith(".ini"));
        hasNvseIni.Should().BeTrue("Stewie's Tweaks has an NVSE plugin INI config");
    }

    // === INI Handling ===

    [Fact]
    public void QolTweaksBundledInisDetectedAsIniTweaks()
    {
        var root = LoadFixture("gopher-qol-tweaks.json");
        var iniMod = root.Mods.First(m => m.Name == "INI Tweaks");

        iniMod.Source.Type.Should().Be(ModSourceType.Bundle);

        var iniFiles = iniMod.Hashes
            .Where(h => h.Path.ToString().EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Path.ToString())
            .ToArray();

        iniFiles.Should().Contain("Fallout.ini");
        iniFiles.Should().Contain("FalloutPrefs.ini");
    }

    // === BSA Validation ===

    [Fact]
    public void RemasterBsaArchivesHaveMatchingPlugins()
    {
        var root = LoadFixture("gopher-remaster.json");

        var allHashes = root.Mods.SelectMany(m => m.Hashes).ToArray();
        var bsaFiles = allHashes
            .Where(h => h.Path.ToString().EndsWith(".bsa", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        bsaFiles.Should().NotBeEmpty("remaster collection includes BSA archives");

        foreach (var bsa in bsaFiles)
        {
            var bsaName = Path.GetFileNameWithoutExtension(bsa.Path.ToString());
            var hasMatchingPlugin = allHashes.Any(h =>
            {
                var fileName = Path.GetFileNameWithoutExtension(h.Path.ToString());
                var ext = Path.GetExtension(h.Path.ToString()).ToLowerInvariant();
                return fileName == bsaName && (ext == ".esp" || ext == ".esm");
            });

            hasMatchingPlugin.Should().BeTrue(
                $"BSA '{bsa.Path}' should have a matching .esp or .esm plugin");
        }
    }

    // === Optional Mod Handling ===

    [Fact]
    public void RemasterHasOptionalMods()
    {
        var root = LoadFixture("gopher-remaster.json");
        var optionalMods = root.Mods.Where(m => m.Optional).ToArray();
        optionalMods.Should().NotBeEmpty("remaster collection has optional mods");
        optionalMods.Select(m => m.Name).Should().Contain("Ojo Bueno Texture Pack");
    }

    [Fact]
    public void StableNvHasNoOptionalMods()
    {
        var root = LoadFixture("gopher-stable-nv.json");
        root.Mods.Should().OnlyContain(m => !m.Optional,
            "stability collection should have no optional mods");
    }

    // === Domain Name ===

    [Theory]
    [InlineData("gopher-stable-nv.json")]
    [InlineData("gopher-remaster.json")]
    [InlineData("gopher-darnified-ui.json")]
    [InlineData("gopher-qol-tweaks.json")]
    public void AllModsTargetNewVegasDomain(string fixture)
    {
        var root = LoadFixture(fixture);
        root.Mods.Should().OnlyContain(m => m.DomainName.ToString() == "newvegas",
            "all mods should target the newvegas domain");
    }
}
