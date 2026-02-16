using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Cli;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Sdk.Loadouts;
using NexusMods.Sdk.ProxyConsole;

namespace NexusMods.DataModel.CommandLine.Verbs;

/// <summary>
/// CLI verbs for querying loadout diagnostics (Health Check).
/// </summary>
public static class DiagnosticVerbs
{
    /// <summary>
    /// Register the diagnostic verbs.
    /// </summary>
    public static IServiceCollection AddDiagnosticVerbs(this IServiceCollection services) =>
        services
            .AddVerb(() => ListDiagnostics)
            .AddVerb(() => ListDiagnosticsJson)
            .AddVerb(() => ShowLocations)
            .AddVerb(() => DebugIni);

    [Verb("loadout diagnostics", "Lists all Health Check diagnostics for a loadout")]
    private static async Task<int> ListDiagnostics(
        [Injected] IRenderer renderer,
        [Option("l", "loadout", "Loadout to diagnose")] Loadout.ReadOnly loadout,
        [Injected] IDiagnosticManager diagnosticManager,
        [Injected] CancellationToken token)
    {
        var diagnostics = await diagnosticManager
            .GetLoadoutDiagnostics(loadout.LoadoutId)
            .FirstAsync();

        if (diagnostics.Length == 0)
        {
            await renderer.TextLine("No diagnostics found. Health Check passed.");
            return 0;
        }

        var writer = new PlainTextDiagnosticWriter();

        await diagnostics
            .Select(d => (
                d.Severity.ToString(),
                d.Id.ToString(),
                d.Title,
                d.FormatSummary(writer)
            ))
            .RenderTable(renderer, "Severity", "ID", "Title", "Summary");

        return 0;
    }

    [Verb("loadout diagnostics-json", "Lists all Health Check diagnostics for a loadout as JSON")]
    private static async Task<int> ListDiagnosticsJson(
        [Injected] IRenderer renderer,
        [Option("l", "loadout", "Loadout to diagnose")] Loadout.ReadOnly loadout,
        [Injected] IDiagnosticManager diagnosticManager,
        [Injected] CancellationToken token)
    {
        var diagnostics = await diagnosticManager
            .GetLoadoutDiagnostics(loadout.LoadoutId)
            .FirstAsync();

        var writer = new PlainTextDiagnosticWriter();

        var entries = diagnostics.Select(d => new
        {
            severity = d.Severity.ToString(),
            id = d.Id.ToString(),
            title = d.Title,
            summary = d.FormatSummary(writer),
            details = d.FormatDetails(writer),
        });

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await renderer.TextLine(json);
        return 0;
    }

    [Verb("loadout locations", "Shows resolved filesystem locations for a loadout")]
    private static async Task<int> ShowLocations(
        [Injected] IRenderer renderer,
        [Option("l", "loadout", "Loadout to inspect")] Loadout.ReadOnly loadout,
        [Injected] CancellationToken token)
    {
        var locations = loadout.InstallationInstance.Locations;
        foreach (var kv in locations.GetTopLevelLocations())
        {
            await renderer.TextLine($"{kv.Key}: {kv.Value}");
        }
        return 0;
    }

    [Verb("loadout debug-ini", "Debug INI file resolution for archive invalidation")]
    private static async Task<int> DebugIni(
        [Injected] IRenderer renderer,
        [Option("l", "loadout", "Loadout to inspect")] Loadout.ReadOnly loadout,
        [Injected] CancellationToken token)
    {
        var prefsPath = loadout.InstallationInstance.Locations[NexusMods.Sdk.Games.LocationId.Preferences].Path;
        await renderer.TextLine($"PrefsPath: {prefsPath}");
        await renderer.TextLine($"PrefsPath.ToString(): {prefsPath.ToString()}");
        await renderer.TextLine($"PrefsPath.FileSystem type: {prefsPath.FileSystem.GetType().FullName}");

        var customIni = prefsPath / "FalloutCustom.ini";
        var falloutIni = prefsPath / "Fallout.ini";

        await renderer.TextLine($"CustomIni path: {customIni}");
        await renderer.TextLine($"CustomIni.FileExists: {customIni.FileExists}");
        await renderer.TextLine($"FalloutIni path: {falloutIni}");
        await renderer.TextLine($"FalloutIni.FileExists: {falloutIni.FileExists}");

        // Also try raw File.Exists
        await renderer.TextLine($"File.Exists(customIni): {System.IO.File.Exists(customIni.ToString())}");
        await renderer.TextLine($"File.Exists(falloutIni): {System.IO.File.Exists(falloutIni.ToString())}");

        if (customIni.FileExists)
        {
            var lines = System.IO.File.ReadAllLines(customIni.ToString());
            await renderer.TextLine($"CustomIni lines: {lines.Length}");
            foreach (var line in lines)
            {
                await renderer.TextLine($"  '{line}'");
                if (line.Trim().StartsWith("bInvalidateOlderFiles", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains('=') &&
                    line.Split('=')[1].Trim() == "1")
                {
                    await renderer.TextLine("  >> MATCH: archive invalidation enabled");
                }
            }
        }

        if (falloutIni.FileExists)
        {
            await renderer.TextLine("FalloutIni exists, searching for bInvalidateOlderFiles...");
            var lines = System.IO.File.ReadAllLines(falloutIni.ToString());
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("bInvalidateOlderFiles", StringComparison.OrdinalIgnoreCase))
                {
                    await renderer.TextLine($"  Found: '{line}'");
                    if (line.Contains('=') && line.Split('=')[1].Trim() == "1")
                        await renderer.TextLine("  >> MATCH: archive invalidation enabled");
                    else
                        await renderer.TextLine("  >> NO MATCH");
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// A minimal diagnostic writer for plain-text CLI output.
    /// Falls back to ToString() for all types — no UI dependency needed.
    /// </summary>
    private sealed class PlainTextDiagnosticWriter : IDiagnosticWriter
    {
        public void Write<T>(ref DiagnosticWriterState state, T value) where T : notnull
        {
            state.StringBuilder.Append(value.ToString());
        }

        public void WriteValueType<T>(ref DiagnosticWriterState state, T value) where T : struct
        {
            state.StringBuilder.Append(value.ToString());
        }

        public void Write(ref DiagnosticWriterState state, ReadOnlySpan<char> value)
        {
            state.StringBuilder.Append(value);
        }
    }
}
