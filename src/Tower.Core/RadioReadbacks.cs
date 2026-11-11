using System.Globalization;

namespace Tower.Core;

public sealed partial class Simulation
{
    private AircraftRadioMessage CommandReadback(Aircraft aircraft, string command, CommandResult result, string intent)
    {
        AircraftRadioMessage Message(string text, string key, double? value = null, string detail = "", string? runway = null, bool includeRunway = true) =>
            new(aircraft.CallSign, State.Airport.Code, text, runway ?? aircraft.Runway, key, Value: value, Detail: detail, IncludeRunway: includeRunway);
        if (!result.Success)
        {
            // Keep diagnostics in the command result; the pilot says the original "unable".
            return Message("Unable.", "command.rejected");
        }
        if (intent == "command.PB.controller" && aircraft.Pushback is { } pushback)
        {
            return Message($"Roger, {result.Message.ToLowerInvariant()}", intent, detail: PushbackDirection(pushback.FinalHeading), includeRunway: false);
        }
        if (command.StartsWith("pattern ", StringComparison.Ordinal))
        {
            var code = command.Split(' ')[1];
            var leg = aircraft.CircuitLeg;
            var side = leg == "final" ? "" : aircraft.RightTraffic ? "right " : "left ";
            var explicitRunway = command.Split(' ').ElementAtOrDefault(2) ?? "";
            var runwayText = explicitRunway.Length > 0 ? " runway " + explicitRunway : "";
            return Message(code.StartsWith('P')
                ? $"Roger, {(leg == "downwind" ? "will report" : "report")} {side}{leg}{runwayText}."
                : $"{(leg == "base" ? "" : "Roger, ")}{(leg == "final" ? "entering" : "enter")} {side}{leg}{runwayText}.",
                code.StartsWith('P') ? "command.pattern.report" : "command.pattern.enter", detail: side + leg, includeRunway: explicitRunway.Length > 0);
        }
        if (command.StartsWith("report final ", StringComparison.Ordinal))
        {
            var explicitRunway = command.Contains(',') ? aircraft.Runway : "";
            var runwayText = explicitRunway.Length > 0 ? " runway " + explicitRunway : "";
            return Message(FormattableString.Invariant($"Roger, {aircraft.ReportFinalNm:0.#} mile final{runwayText}."),
                "command.final.report", aircraft.ReportFinalNm, includeRunway: explicitRunway.Length > 0);
        }
        var vertical = aircraft.VerticalSpeedFpm > 50 ? "climbing" : aircraft.VerticalSpeedFpm < -50 ? "descending" : "";
        if (command == "say heading")
        {
            return Message(FormattableString.Invariant($"Heading {aircraft.Heading:000}."), "status.heading", Math.Round(aircraft.Heading));
        }
        if (command == "say speed")
        {
            return Message(FormattableString.Invariant($"Indicating {aircraft.SpeedKnots:0} knots."), "status.speed", Math.Round(aircraft.SpeedKnots));
        }
        if (command == "say altitude")
        {
            var altitude = Math.Round(aircraft.AltitudeFeet + (State.Airport.Original.FieldElevationFeetMsl ?? 0));
            return Message(FormattableString.Invariant($"{(vertical.Length > 0 ? vertical + " through" : "At")} {altitude:0} feet."),
                "status.altitude", altitude, vertical);
        }
        if (command == "say rate")
        {
            var rate = Math.Round(Math.Abs(aircraft.VerticalSpeedFpm));
            return Message(FormattableString.Invariant($"{vertical} {rate:0} feet per minute.").Trim(), "status.rate", rate, vertical);
        }
        if (command == "say position")
        {
            if (aircraft.IsAirborne)
            {
                var origin = State.Airport.Observer;
                var miles = Math.Truncate(origin.Distance(aircraft.Position) / 1609);
                var sector = (int)Math.Round(origin.Bearing(aircraft.Position) / 45) % 8;
                var direction = new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" }[sector];
                return Message(FormattableString.Invariant($"{miles:0} {(miles == 1 ? "mile" : "miles")} {direction} of airport."),
                    "status.position", miles, sector.ToString(CultureInfo.InvariantCulture));
            }
            if (aircraft.Phase is FlightPhase.AtGate or FlightPhase.Completed)
            {
                return Message($"At gate {aircraft.Gate}.", "status.position", detail: "gate:" + aircraft.Gate);
            }
            if (aircraft.HoldShortRunway is { } holdingRunway && aircraft.Holding)
            {
                return Message($"Holding short of runway {holdingRunway}.", "status.position", detail: "short", runway: holdingRunway);
            }
            return Message(aircraft.Holding ? "Holding position." : aircraft.IsOnRunway || aircraft.Phase == FlightPhase.HoldingShort
                ? $"At runway {aircraft.Runway}." : "On taxiway.", "status.position",
                detail: aircraft.Holding ? "holding" : aircraft.IsOnRunway || aircraft.Phase == FlightPhase.HoldingShort ? "runway" : "taxiway");
        }
        if (command.StartsWith("heading ", StringComparison.Ordinal) || command.StartsWith("turn left ", StringComparison.Ordinal) ||
            command.StartsWith("turn right ", StringComparison.Ordinal))
        {
            var tokens = command.Split(' ');
            var number = double.Parse(tokens[2], CultureInfo.InvariantCulture);
            var side = tokens[1] is "left" or "L" ? "left" : "right";
            return Message(FormattableString.Invariant($"Roger, {side} {number:000}."),
                command.StartsWith("heading ", StringComparison.Ordinal) ? "command.heading" : "command.turn", number, side);
        }
        if (command is "turn out" or "left turn out" or "right turn out")
        {
            var side = command == "turn out" ? "" : command.Split(' ')[0] + " ";
            return Message($"{(side.Length == 0 ? "Roger, " : side)}turn out approved.", "command.turnout", detail: side.Trim());
        }
        if (command is "handoff" or "contact departure")
        {
            return Message("Switching, good day.", "command.FD.pilot");
        }
        if (command.StartsWith("taxi to gate ", StringComparison.Ordinal))
        {
            return Message("Roger, taxiing to parking.", "command.TP.pilot");
        }
        var canonical = intent switch
        {
            "command.G.pilot" => "Roger, go around.",
            "command.CL.pilot" => "Roger, cleared to land.",
            "command.CT.pilot" => "Cleared for takeoff.",
            "command.CG.pilot" => "Roger, cleared for touch and go.",
            "command.CA.pilot" => "Cleared for low approach.",
            "command.TH.pilot" => "Roger, position and hold.",
            "command.TP.pilot" => "Roger, taxiing to parking.",
            "command.T.pilot" => $"Roger, taxi to runway {aircraft.Runway}.",
            "command.HS.pilot" => $"Roger, holding short runway {aircraft.HoldShortRunway}.",
            "command.HY.pilot" => "Roger.",
            "command.TU.pilot" => "Proceeding.",
            "command.CC.pilot" => "Cancel takeoff clearance.",
            "command.X.pilot" => "Extending downwind.",
            "command.MS.pilot" => "Making short approach.",
            "command.B.pilot" => "Breaking out of traffic.",
            _ => result.Message
        };
        return Message(canonical, intent, runway: intent == "command.HS.pilot" ? aircraft.HoldShortRunway : null);
    }

    private void TransmitReadyForTakeoff(Aircraft aircraft) =>
        Transmit(aircraft, "Ready for takeoff.", "departure.takeoff.ready");

    private void TransmitBaseReport(Aircraft aircraft)
    {
        var intent = aircraft.LandingIntent switch
        {
            LandingIntent.TouchAndGo => "flight.base.touchandgo",
            LandingIntent.LowApproach => "flight.base.lowapproach",
            _ => "flight.base.fullstop"
        };
        var text = aircraft.LandingIntent switch
        {
            LandingIntent.TouchAndGo => "Base, gear down for touch and go.",
            LandingIntent.LowApproach => "Base for low approach.",
            _ => "Base for full stop."
        };
        Transmit(aircraft, text, intent);
    }

    private void TransmitOnTheGo(Aircraft aircraft, bool runwayPass)
    {
        if (!runwayPass)
        {
            return;
        }
        // Source 1002B810 selects a separate turn-out request for a departing
        // pattern flight. Restrict the reconstruction to an explicit @LP plan;
        // ordinary departures and circuits must not all request a right turn.
        if (aircraft.OriginalActions is { Completed: false } actions && actions.Current() == OriginalFlightAction.LeavePattern &&
            aircraft.Specification.OriginalTypeGroup is not ("Airline" or "Heavy"))
        {
            Transmit(aircraft, "On the go, request right turn out of traffic.", "flight.onthego.turnout");
        }
        else
        {
            Transmit(aircraft, "On the go.", "flight.onthego");
        }
    }
}
