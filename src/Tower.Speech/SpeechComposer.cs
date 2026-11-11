namespace TowerSpeech.Core;

public sealed class SpeechComposer(VoiceCatalog catalog, VocAudioRepository audioRepository, CallsignLexicon? callsignLexicon = null,
    IReadOnlyList<SpeechSequence>? sequences = null)
{
    private static readonly string[] Digits =
        ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "niner"];

    // User-reviewed sequence templates can override radio intentions. If there is
    // no unique matching template, the original simulation behavior is retained.
    public IReadOnlyList<SpeechPart>? SelectTemplateParts(RadioMessage message, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(message.IntentKey))
        {
            return null;
        }
        List<SpeechSequence> applicable = [];
        foreach (var seq in sequences ?? [])
        {
            if (!string.Equals(seq.IntentKey, message.IntentKey, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(seq.Speaker, message.Speaker.ToString(), StringComparison.OrdinalIgnoreCase) ||
                !(seq.AirportCode == "*" || string.Equals(seq.AirportCode, message.AirportCode, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            applicable.Add(seq);
        }
        if (applicable.Count == 0)
        {
            return null;
        }

        if (applicable.Count > 1)
        {
            warnings.Add($"Multiple speech templates for {message.IntentKey} / {message.Speaker}; no random choice was made");
            return null;
        }
        List<SpeechPart> result = [];
        foreach (var step in applicable[0].Steps)
        {
            switch (step.Kind)
            {
                case "fragment":
                    var frag = catalog.Get(step.Id);
                    if (frag is null || string.IsNullOrWhiteSpace(frag.Transcript))
                    {
                        warnings.Add($"Speech template: {step.Id} has no usable transcript");
                        break;
                    }
                    result.Add(new RecordedPhrase(frag.Transcript, step.Id));
                    break;
                case "text":
                    if (!string.IsNullOrWhiteSpace(step.Text))
                    {
                        result.Add(new RecordedPhrase(step.Text));
                    }
                    else
                    {
                        warnings.Add("Empty TTS text step in speech template");
                    }

                    break;
                case "distance":
                    var distance = message.Parts.OfType<SpokenDigits>()
                        .FirstOrDefault(n => n.Context == NumberContext.Distance);
                    if (distance is null)
                    {
                        warnings.Add("Template requests [DISTANCE], but the message has no distance");
                    }
                    else
                    {
                        result.Add(distance);
                    }

                    break;
                case "distanceunit":
                    var miles = message.Parts.OfType<SpokenDigits>()
                        .FirstOrDefault(n => n.Context == NumberContext.Distance)?.Digits;
                    if (miles is null)
                    {
                        warnings.Add("Template requests a distance unit without a distance");
                    }
                    else
                    {
                        result.Add(new RecordedPhrase(miles == "1" ? "mile" : "miles"));
                    }

                    break;
                case "callsign":
                    result.Add(new AircraftCallsign(message.AircraftId));
                    break;
                case "runway":
                    result.Add(new RunwayNumber(message.Runway ?? ""));
                    break;
                default:
                    warnings.Add($"Unknown speech template step: {step.Kind}");
                    break;
            }
        }
        return result;
    }

    public RenderedMessage Render(RadioMessage message, VoiceMode mode)
    {
        List<VoicePiece> pieces = [];
        List<string> warnings = [];
        var parts = SelectTemplateParts(message, warnings) ?? message.Parts;
        void Phrase(string text, FragmentId? preferred = null)
        {
            if (catalog.TryResolve(text, message.AirportCode, mode, preferred, out var piece, out var issue))
            {
                pieces.Add(piece!);
            }
            else
            {
                warnings.Add(issue ?? $"Unknown: {text}");
            }
        }
        void DigitsOnly(string value, string kind)
        {
            if (!value.All(char.IsAsciiDigit))
            {
                warnings.Add($"{kind} '{value}': not a safe spoken digit pattern");
                return;
            }
            foreach (char c in value)
            {
                Phrase(Digits[c - '0']);
            }
        }
        void Cardinal(int value)
        {
            if (value >= 100)
            {
                var unit = value >= 1000 ? 1000 : 100;
                var unitId = new FragmentId(2, unit == 1000 ? 182 : 183);
                Cardinal(value / unit);
                Phrase(catalog.Get(unitId)?.Transcript ?? (unit == 1000 ? "thousand" : "hundred"), unitId);
                if (value % unit > 0)
                {
                    Cardinal(value % unit);
                }
                return;
            }
            var index = value < 10 ? 27 + value : value < 20 ? 148 + value - 10 : 158 + value / 10 - 2;
            var spoken = value < 20 ? AviationSpeechFormatter.Cardinal(value) : AviationSpeechFormatter.Cardinal(value / 10 * 10);
            // The original number routine selects VOICE indices, not a text search.
            // Preserve the reviewed spelling (e.g. "fourty") without changing reviews.
            var id = new FragmentId(2, index);
            Phrase(catalog.Get(id)?.Transcript ?? spoken, id);
            if (value >= 20 && value % 10 != 0)
            {
                Cardinal(value % 10);
            }
        }
        foreach (var part in parts)
        {
            switch (part)
            {
                case RecordedPhrase phrase:
                    var catalogText = phrase.Preferred is { } preferred ? catalog.Get(preferred)?.Transcript : null;
                    Phrase(phrase.CatalogText is { } recordingAlias && VoiceCatalog.Normalize(catalogText ?? "") == VoiceCatalog.Normalize(recordingAlias)
                        ? recordingAlias : phrase.Text, phrase.Preferred);
                    break;
                case AircraftCallsign callsign:
                    // Full CALLSIGN.VOC recordings preserve the original airline voice and
                    // pronunciation of flight numbers. They must not be split into words.
                    if (callsignLexicon is not null &&
                        callsignLexicon.TryGetFull(callsign.Value, out var fullAlias) && fullAlias is not null)
                    {
                        Phrase(fullAlias.Text, new FragmentId(fullAlias.Bank, fullAlias.Index, fullAlias.AirportCode));
                        break;
                    }
                    // Without a full recording, use explicitly configured prefixes then
                    // VOICE phonetic digits; do not fabricate a callsign bank mapping.
                    var prefixLength = callsign.Value.TakeWhile(char.IsAsciiLetter).Count();
                    string prefix = callsign.Value[..prefixLength];
                    string suffix = callsign.Value[prefixLength..];
                    if (prefix.Length > 0)
                    {
                        if (callsignLexicon is not null && callsignLexicon.TryGet(prefix, out var alias) && alias is not null)
                        {
                            Phrase(alias.Text, new FragmentId(alias.Bank, alias.Index, alias.AirportCode));
                        }
                        else
                        {
                            warnings.Add($"Callsign-prefix '{prefix}' is not linked to a reviewed CALLSIGN.VOC fragment");
                        }
                    }
                    // Fallback: speak only a purely numeric remainder. Never guess abbreviated registrations.
                    if (suffix.Length > 0)
                    {
                        DigitsOnly(suffix, "Callsign number");
                    }

                    break;
                case RunwayNumber runway:
                    var raw = runway.Value.Trim().ToUpperInvariant();
                    if (!System.Text.RegularExpressions.Regex.IsMatch(raw, @"^\d{1,2}[LCR]?$"))
                    {
                        warnings.Add($"Runway '{raw}': expected one or two digits with optional L/R/C");
                        break;
                    }
                    var runwayId = audioRepository.FindRunway(message.AirportCode, raw);
                    if (runwayId is { } id && catalog.Get(id) is { Transcript: { Length: > 0 } transcript })
                    {
                        // SPF binds the runway name to its complete airport R.VOC recording.
                        Phrase(transcript, id);
                    }
                    else if (audioRepository.UsesRunwayDigits(message.AirportCode, raw))
                    {
                        // New airports may explicitly compose existing reviewed digits, without inventing a review.
                        if (audioRepository.RunwayDigitPrefix(message.AirportCode) is { Length: > 0 } runwayPrefix)
                        {
                            Phrase(runwayPrefix);
                        }
                        DigitsOnly(raw.TrimEnd('L', 'C', 'R'), "Runway");
                        if (raw[^1] is 'L' or 'C' or 'R')
                        {
                            Phrase(raw[^1] switch { 'L' => "left", 'C' => "center", _ => "right" });
                        }
                    }
                    else
                    {
                        warnings.Add($"Runway '{raw}': no reviewed whole-runway recording mapped by {message.AirportCode}.SPF [RUNWAYS]");
                    }
                    break;
                case SpokenDigits num:
                    if (num.Context is NumberContext.Distance or NumberContext.Altitude or NumberContext.Cardinal &&
                        int.TryParse(num.Digits, out int distance) && distance is >= 0 and < 1_000_000)
                    {
                        Cardinal(distance);
                    }
                    else
                    {
                        DigitsOnly(num.Digits, num.Context.ToString());
                    }

                    break;
            }
        }

        List<byte[]> waveClips = [];
        foreach (var piece in pieces)
        {
            try
            {
                waveClips.Add(audioRepository.ReadWav(piece.Id));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
            {
                warnings.Add($"Could not read VOC: {piece.Id}: {ex.Message}");
            }
        }
        byte[]? audio = null;
        if (waveClips.Count > 0)
        {
            try
            {
                audio = WaveComposer.Compose(waveClips);
            }
            catch (InvalidDataException ex) { warnings.Add($"WAV format error: {ex.Message}"); }
        }
        if (pieces.Any(p => p.IsCandidate))
        {
            warnings.Add("EXPERIMENTAL: these transcripts are ASR candidates, not approved by listening");
        }

        // Generated phrases have their own listening review; they do not borrow
        // the original pilot VOICE bank merely because the speaker is Controller.
        var pushbackCompass = message.IntentKey == "command.PB.controller" && pieces.Any(p => p.Id.Bank == 6) &&
            pieces.All(p => p.Id.Bank is 1 or 6 || p.Id.Bank == 2 && p.Id.Index is 37 or 39 or 41 or 43);
        if (message.Speaker == RadioSpeaker.Controller && !pushbackCompass && pieces.Any(p => p.Id.Bank is >= 2 and <= 5))
        {
            warnings.Add("The VOICE bank has not been validated as a controller voice; this is a technical demonstration");
        }

        if (warnings.Count > 0)
        {
            warnings.Insert(0, "INCOMPLETE / UNVERIFIED RADIO SPEECH — check the log");
        }

        return new RenderedMessage(message, pieces, warnings, audio);
    }
}
