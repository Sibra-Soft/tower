using Tower.Core;
using TowerSpeech.Core;

namespace Tower.Infrastructure;

/// <summary>Read-only access to the workspace maintained by the speech Workbench.</summary>
public sealed class WorkbenchSpeechRuntime
{
    private readonly SpeechComposer composer;
    private readonly AviationSpeechFormatter formatter;
    private readonly VoiceCatalog catalog;
    private readonly CallsignLexicon aliases;
    private readonly VocAudioRepository audio;

    public SpeechPreferences Preferences
    {
        get;
    }
    public string Workspace
    {
        get;
    }

    public WorkbenchSpeechRuntime(string? workspace = null) : this(workspace ?? WorkspacePaths.RuntimeRoot, false)
    {
    }

    public static WorkbenchSpeechRuntime ForAuthoring(string workspace) => new(workspace, true);

    private WorkbenchSpeechRuntime(string workspace, bool authoring)
    {
        Workspace = workspace ?? WorkspacePaths.RuntimeRoot;
        var data = WorkspacePaths.DataFolder(Workspace);
        var installed = OriginalData.InstalledAirports(data).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var document = authoring ? SpeechDataStore.Load(data) : CompiledSpeechData.LoadRuntime(data, installed);
        catalog = VoiceCatalog.FromFragments(document.Fragments);
        audio = new VocAudioRepository(data);
        audio.ValidateSources(document, authoring ? null : installed.Contains);
        aliases = OriginalCallsignBindings.Resolve(data, document.Callsigns, catalog, audio.HasFragment);
        composer = new SpeechComposer(catalog, audio, aliases, document.Sequences);
        formatter = new AviationSpeechFormatter(aliases);
        Preferences = document.Preferences.Copy();
        if (!Enum.IsDefined(Preferences.Engine) || Preferences.Rate is < -10 or > 10 || Preferences.NoisePercent is < 0 or > 30)
        {
            throw new InvalidDataException("Invalid Workbench speech preferences.");
        }
    }

    public IReadOnlyCollection<string>? GetTrafficCallsigns()
    {
        if (Preferences.Engine == SpeechEngineKind.WindowsTts)
        {
            return null;
        }

        return aliases.FullCallsigns.Keys.Where(callsign =>
        {
            if (!aliases.TryGetFull(callsign, out var alias) || alias is null)
            {
                return false;
            }
            var id = new FragmentId(alias.Bank, alias.Index, alias.AirportCode);
            return catalog.TryResolve(alias.Text, "", VoiceMode.Strict, id, out _, out _) && audio.HasFragment(id);
        }).Select(callsign => callsign.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
    }

    public SpeechOutput Render(AircraftRadioMessage source)
    {
        var message = WorkbenchRadioAdapter.Create(source);
        if (Preferences.Engine == SpeechEngineKind.BaoOriginal)
        {
            var rendered = composer.Render(message, VoiceMode.Strict);
            // Never transmit an incomplete clearance or silently omit a callsign.
            return rendered.Warnings.Count == 0 && rendered.WaveBytes is { Length: > 44 }
                ? new(rendered.WaveBytes, "", null)
                : new(null, "", "Original BAO speech unavailable for " + source.Callsign + ": " +
                    string.Join("; ", rendered.Warnings.Where(w => !w.StartsWith("INCOMPLETE", StringComparison.Ordinal)).Take(3)));
        }

        List<string> warnings = [];
        var parts = composer.SelectTemplateParts(message, warnings) ?? message.Parts;
        if (warnings.Count > 0)
        {
            return new(null, "", "Speech template is incomplete or ambiguous for " + source.Callsign + ".");
        }
        return new(null, formatter.FormatParts(parts), null);
    }
}

public sealed record SpeechOutput(byte[]? Wave, string Text, string? Diagnostic);
