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
            .AddVerb(() => ListDiagnosticsJson);

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
