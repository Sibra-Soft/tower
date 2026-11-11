using System.Text.RegularExpressions;
using Tower.Core;
using TowerSpeech.Core;

namespace Tower.Infrastructure;

public static class WorkbenchRadioAdapter
{
    public static RadioMessage Create(AircraftRadioMessage message)
    {
        var arrivalMiles = message.IntentKey == "arrival.initial"
            ? OriginalRadioDistance.SpokenMiles(message.DistanceNm ?? OriginalRadioDistance.NauticalMiles(1)) : 0;
        List<SpeechPart> parts = message.IntentKey switch
        {
            "departure.pushback.request" => [new AircraftCallsign(message.Callsign), new RecordedPhrase("request pushback", new(6, 1))],
            "command.PB.controller" when message.Detail == "west" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), new RecordedPhrase("pushback approved face west", new(6, 3))],
            "command.PB.controller" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), new RecordedPhrase("pushback approved face", new(6, 2)),
                Voice(message.Detail switch { "north" => 37, "east" => 39, "south" => 41, _ => 43 }, message.Detail)],
            "arrival.initial" => [new AircraftCallsign(message.Callsign), Voice(52, "with you"),
                new SpokenDigits(arrivalMiles.ToString(System.Globalization.CultureInfo.InvariantCulture), NumberContext.Distance),
                Voice(arrivalMiles == 1 ? 54 : 55, arrivalMiles == 1 ? "mile" : "miles")],
            "departure.ready" => [new AircraftCallsign(message.Callsign), Voice(57, "ready for taxi")],
            "departure.takeoff.ready" => [new AircraftCallsign(message.Callsign), Voice(58, "ready for takeoff")],
            "command.T.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"),
                Voice(117, "taxi to", "taxi two"), new RunwayNumber(message.Runway ?? "")],
            "command.CL.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), Voice(75, "cleared to land")],
            "command.CT.pilot" => [new AircraftCallsign(message.Callsign), Voice(69, "cleared"), Voice(71, "for takeoff", "four takeoff")],
            "command.CG.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), Voice(69, "cleared"), Voice(72, "for touch and go", "four touch and go")],
            "command.CA.pilot" => [new AircraftCallsign(message.Callsign), Voice(69, "cleared"), Voice(73, "for low approach", "four low approach")],
            "command.G.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), Voice(83, "go around", "around")],
            "flight.goaround" => [new AircraftCallsign(message.Callsign), Voice(128, "executing missed approach")],
            "command.TP.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), Voice(193, "taxiing to parking", "taxiing and parking")],
            "command.HS.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), Voice(119, "holding short of"), new RunwayNumber(message.Runway ?? "")],
            "command.TH.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger"), Voice(114, "position and hold")],
            "command.HY.pilot" => [new AircraftCallsign(message.Callsign), Voice(45, "roger")],
            "command.TU.pilot" => [new AircraftCallsign(message.Callsign), Voice(106, "proceeding")],
            "command.CC.pilot" => [new AircraftCallsign(message.Callsign), Voice(100, "cancel takeoff clearance")],
            "command.X.pilot" => [new AircraftCallsign(message.Callsign), Voice(76, "extending downwind")],
            "command.MS.pilot" => [new AircraftCallsign(message.Callsign), Voice(77, "making short approach")],
            "command.B.pilot" => [new AircraftCallsign(message.Callsign), Voice(78, "breaking out of traffic")],
            "ground.holding.short" => [new AircraftCallsign(message.Callsign), Voice(119, "holding short of"), new RunwayNumber(message.Runway ?? "")],
            "ground.parking.request" => [new AircraftCallsign(message.Callsign), Voice(192, "up your frequency request taxi to parking")],
            "command.rejected" => [new AircraftCallsign(message.Callsign), Voice(122, "unable")],
            "command.FD.pilot" => [new AircraftCallsign(message.Callsign), Voice(110, "switching"), Voice(49, "good day")],
            "command.pattern.enter" or "command.pattern.report" => PatternParts(message),
            "command.final.report" => WithRunway(message, [new AircraftCallsign(message.Callsign), Voice(45, "roger"),
                Number(message.Value), Voice(87, "mile final")]),
            "flight.final.distance" => [new AircraftCallsign(message.Callsign), Number(message.Value), Voice(87, "mile final")],
            "command.turnout" => TurnoutParts(message),
            "command.heading" or "command.turn" => TurnParts(message),
            "flight.downwind" => [new AircraftCallsign(message.Callsign), Voice(66, "downwind")],
            "flight.base.fullstop" => [new AircraftCallsign(message.Callsign), Voice(67, "base"), Voice(74, "for full stop", "four full stop")],
            "flight.base.touchandgo" => [new AircraftCallsign(message.Callsign), Voice(67, "base"), Voice(70, "gear down"), Voice(72, "for touch and go", "four touch and go")],
            "flight.base.lowapproach" => [new AircraftCallsign(message.Callsign), Voice(67, "base"), Voice(73, "for low approach", "four low approach")],
            "flight.final" => [new AircraftCallsign(message.Callsign), Voice(68, "final")],
            "flight.turning.final" => [new AircraftCallsign(message.Callsign), Voice(132, "turning final")],
            "flight.established.final" => [new AircraftCallsign(message.Callsign), Voice(131, "established on final")],
            "flight.approach.request" => [new AircraftCallsign(message.Callsign), Voice(166, "request approach to", "request approach two"), new RunwayNumber(message.Runway)],
            "flight.onthego" => [new AircraftCallsign(message.Callsign), Voice(63, "on the go")],
            "flight.onthego.turnout" => [new AircraftCallsign(message.Callsign), Voice(63, "on the go"), Voice(60, "request right turn out of traffic")],
            "ground.blocked" => [new AircraftCallsign(message.Callsign), Voice(194, "there's an aircraft in front of me")],
            "ground.crossing.request" => [new AircraftCallsign(message.Callsign), Voice(123, "request permission to cross", "request permission across"), new RunwayNumber(message.Runway)],
            "status.heading" => [new AircraftCallsign(message.Callsign), Voice(178, "heading"), Number(message.Value, NumberContext.Heading)],
            "status.speed" => [new AircraftCallsign(message.Callsign), Voice(179, "indicating"), Number(message.Value, NumberContext.Cardinal), Voice(180, "knots")],
            "status.altitude" or "status.rate" or "status.position" => StatusParts(message),
            _ => GenericParts(message)
        };

        if (message.IntentKey == "arrival.initial" && OriginalRadioDirection.Sector(message.Detail) is var sector && sector >= 0)
        {
            parts.Add(Voice(37 + sector, message.Detail.ToLowerInvariant()));
        }
        // Keep the legacy PB intent key, but its readback is spoken by the pilot.
        return new(message.Callsign, message.AirportCode, RadioSpeaker.Pilot,
            message.Text, parts, message.Runway, message.IntentKey);
    }

    private static RecordedPhrase Voice(int index, string text, string? catalogText = null) => new(text, new FragmentId(2, index), catalogText);

    private static SpeechPart Number(double? value, NumberContext context = NumberContext.Distance)
    {
        // Original numeric routines use integers. Preserve modern fractional PF
        // requests for TTS; BAO reports an unsupported fragment instead of omitting it.
        var text = (value ?? 0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return text.All(char.IsAsciiDigit) ? new SpokenDigits(context == NumberContext.Heading ? text.PadLeft(3, '0') : text, context)
            : new RecordedPhrase(text);
    }

    private static List<SpeechPart> PatternParts(AircraftRadioMessage message)
    {
        var report = message.IntentKey == "command.pattern.report";
        var (index, text) = (report, message.Detail) switch
        {
            (false, "left downwind") => (184, "enter left downwind"),
            (false, "right downwind") => (185, "enter right downwind"),
            (false, "left base") => (186, "enter left base"),
            (false, "right base") => (187, "enter right base"),
            (true, "left downwind") => (188, "will report left downwind"),
            (true, "right downwind") => (189, "will report right downwind"),
            (true, "left base") => (190, "report left base"),
            (true, "right base") => (191, "report right base"),
            (true, "final") => (86, "call you final"),
            _ => (92, "entering final")
        };
        var catalogText = index is 188 or 189 ? text.Replace("will", "we'll", StringComparison.Ordinal) : null;
        List<SpeechPart> parts = [new AircraftCallsign(message.Callsign)];
        // The original ENTER_BASE branches omit Roger; downwind/final include it.
        if (report || !message.Detail.EndsWith("base", StringComparison.Ordinal))
        {
            parts.Add(Voice(45, "roger"));
        }
        parts.Add(Voice(index, text, catalogText));
        return WithRunway(message, parts);
    }

    private static List<SpeechPart> WithRunway(AircraftRadioMessage message, List<SpeechPart> parts)
    {
        if (message.IncludeRunway && message.Runway.Length > 0)
        {
            parts.Add(new RunwayNumber(message.Runway));
        }
        return parts;
    }

    private static List<SpeechPart> TurnoutParts(AircraftRadioMessage message)
    {
        List<SpeechPart> parts = [new AircraftCallsign(message.Callsign)];
        if (message.Detail.Length == 0)
        {
            parts.Add(Voice(45, "roger"));
        }
        else
        {
            parts.Add(Voice(message.Detail == "left" ? 51 : 50, message.Detail));
        }
        parts.Add(Voice(84, "turn out approved"));
        return parts;
    }

    private static List<SpeechPart> TurnParts(AircraftRadioMessage message)
    {
        List<SpeechPart> parts = [new AircraftCallsign(message.Callsign), Voice(45, "roger")];
        if (message.IntentKey == "command.turn" && message.Value == 360)
        {
            parts.Add(Voice(message.Detail == "left" ? 80 : 79, message.Detail + " three sixty"));
        }
        else if (message.IntentKey == "command.turn" && message.Value == 180)
        {
            // 00443FAD/00443FF6 use "one eighty", without "hundred".
            parts.Add(Voice(message.Detail == "left" ? 51 : 50, message.Detail));
            parts.Add(Voice(28, "one"));
            parts.Add(Voice(164, "eighty"));
        }
        else
        {
            parts.Add(Voice(message.Detail == "left" ? 51 : 50, message.Detail));
            parts.Add(Number(message.Value, message.IntentKey == "command.heading" ? NumberContext.Heading : NumberContext.Cardinal));
        }
        return parts;
    }

    private static List<SpeechPart> StatusParts(AircraftRadioMessage message)
    {
        List<SpeechPart> parts = [new AircraftCallsign(message.Callsign)];
        if (message.IntentKey == "status.rate")
        {
            if (message.Detail.Length > 0)
            {
                parts.Add(Voice(message.Detail == "climbing" ? 174 : 175, message.Detail));
            }
            parts.Add(Number(message.Value, NumberContext.Cardinal));
            parts.Add(Voice(176, "feet per minute"));
        }
        else if (message.IntentKey == "status.altitude")
        {
            parts.Add(Voice(message.Detail.Length > 0 ? message.Detail == "climbing" ? 174 : 175 : 137,
                message.Detail.Length > 0 ? message.Detail : "at"));
            if (message.Detail.Length > 0)
            {
                parts.Add(Voice(181, "through"));
            }
            parts.Add(Number(message.Value, NumberContext.Altitude));
            parts.Add(Voice(177, "feet"));
        }
        else
        {
            parts.AddRange(PositionParts(message));
        }
        return parts;
    }

    private static IEnumerable<SpeechPart> PositionParts(AircraftRadioMessage message)
    {
        if (message.Detail.StartsWith("gate:", StringComparison.Ordinal))
        {
            List<SpeechPart> parts = [Voice(138, "at gate")];
            // The original gate-string helper spells identifiers; never parse G3 as zero.
            var letters = new[] { "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india", "juliet",
                "kilo", "lima", "mike", "november", "oscar", "papa", "quebec", "romeo", "sierra", "tango", "uniform", "victor",
                "whiskey", "x-ray", "yankee", "zulu" };
            foreach (var character in message.Detail[5..].ToUpperInvariant())
            {
                parts.Add(character is >= 'A' and <= 'Z' ? Voice(character - 'A' + 1, letters[character - 'A'],
                    character == 'G' ? "all" : character == 'J' ? "julia" : null)
                    : char.IsAsciiDigit(character) ? new SpokenDigits(character.ToString(), NumberContext.Runway)
                    : new RecordedPhrase(character.ToString()));
            }
            return parts;
        }
        if (int.TryParse(message.Detail, out var sector) && sector is >= 0 and <= 7)
        {
            return [Number(message.Value), Voice(message.Value == 1 ? 54 : 55, message.Value == 1 ? "mile" : "miles"),
                Voice(37 + sector, new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" }[sector]),
                Voice(56, "of airport")];
        }
        return message.Detail switch
        {
            "holding" => [Voice(121, "holding"), Voice(141, "position")],
            "runway" => [Voice(137, "at"), new RunwayNumber(message.Runway)],
            "short" => [Voice(119, "holding short of"), new RunwayNumber(message.Runway)],
            "taxiway" => [Voice(139, "on taxiway")],
            _ => [new RecordedPhrase("unknown position")]
        };
    }

    private static List<SpeechPart> GenericParts(AircraftRadioMessage message)
    {
        var parts = new List<SpeechPart> { new AircraftCallsign(message.Callsign) };
        var position = 0;
        foreach (Match match in Regex.Matches(message.Text,
            @"\brunway\s+(?<runway>\d{1,2}[LCR]?)\b|(?<number>\d+(?:\.\d+)?)(?<unit>\s*(?:degrees|feet|knots|NM|mile[s]?)\b)?", RegexOptions.IgnoreCase))
        {
            if (match.Index > position)
            {
                parts.Add(new RecordedPhrase(message.Text[position..match.Index]));
            }
            if (match.Groups["runway"].Success)
            {
                parts.Add(new RunwayNumber(match.Groups["runway"].Value));
            }
            else
            {
                if (match.Groups["number"].Value.Contains('.'))
                {
                    parts.Add(new RecordedPhrase(match.Value));
                    position = match.Index + match.Length;
                    continue;
                }
                var unit = match.Groups["unit"].Value.Trim().ToLowerInvariant();
                var kind = unit switch
                {
                    "feet" => NumberContext.Altitude,
                    "knots" => NumberContext.Speed,
                    "mile" or "miles" or "nm" => NumberContext.Distance,
                    _ => NumberContext.Heading
                };
                parts.Add(new SpokenDigits(match.Groups["number"].Value, kind));
                if (unit.Length > 0)
                {
                    parts.Add(new RecordedPhrase(unit == "nm" ? "miles" : unit));
                }
            }
            position = match.Index + match.Length;
        }
        if (position < message.Text.Length)
        {
            parts.Add(new RecordedPhrase(message.Text[position..]));
        }
        return parts;
    }
}
