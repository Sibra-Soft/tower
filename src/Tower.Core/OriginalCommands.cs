using System.Globalization;
using System.Text.RegularExpressions;
namespace Tower.Core;

public sealed partial class Simulation
{
    private static readonly Dictionary<string, string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CL"] = "land",
        ["CG"] = "touch and go",
        ["CA"] = "low approach",
        ["CO"] = "option",
        ["G"] = "go around",
        ["CT"] = "takeoff",
        ["TP"] = "taxi to parking",
        ["TU"] = "continue taxi",
        ["TH"] = "line up",
        ["HY"] = "hold",
        ["CC"] = "cancel takeoff",
        ["FD"] = "handoff",
        ["SA"] = "say altitude",
        ["SH"] = "say heading",
        ["SR"] = "say rate",
        ["SP"] = "say position",
        ["SG"] = "say again",
        ["SS"] = "say speed",
        ["B"] = "break out",
        ["X"] = "extend downwind",
        ["MS"] = "short approach",
        ["AT"] = "turn out",
        ["ALT"] = "left turn out",
        ["ART"] = "right turn out",
        ["ML1"] = "turn left 180",
        ["ML3"] = "turn left 360",
        ["MR1"] = "turn right 180",
        ["MR3"] = "turn right 360"
    };

    public CommandResult Execute(string input, string? selectedCallsign = null)
    {
        input = input.Trim();
        if (input.Length == 0)
        {
            return new(false, "Use: CALLSIGN/command.");
        }

        var firstToken = input.Split(['/', ',', ' '], 2)[0];
        var explicitAddress = firstToken.Length > 0 && firstToken.Length < input.Length && State.Aircraft.Any(a => a.Phase != FlightPhase.Completed && a.CallSign.EndsWith(firstToken, StringComparison.OrdinalIgnoreCase));
        // Selected-aircraft shorthand is explicit for compact commands (/CL) or known English verbs.
        if (!string.IsNullOrEmpty(selectedCallsign) && !explicitAddress && (input.StartsWith('/') ||
            (!input.Contains('/') && Regex.IsMatch(input, @"^(pushback|land|cleared|go|ga|hold|continue|taxi|line|takeoff|speed|contact|handoff|say|cancel|touch|low|option|break|extend|short|turn|left|right)\b", RegexOptions.IgnoreCase))))
        {
            input = selectedCallsign + " " + input.TrimStart('/');
        }

        var split = input.IndexOfAny(['/', ',', ' ']);
        if (split < 1)
        {
            return new(false, "Use: CALLSIGN/command. Type HELP for the command list.");
        }

        var id = input[..split].ToUpperInvariant();
        var active = State.Aircraft.Where(a => a.IsOnFrequency && a.Phase != FlightPhase.Completed).ToArray();
        var exact = active.FirstOrDefault(a => a.CallSign.Equals(id, StringComparison.OrdinalIgnoreCase));
        var matches = exact is null ? active.Where(a => a.CallSign.EndsWith(id, StringComparison.OrdinalIgnoreCase)).ToArray() : [exact];
        if (matches.Length != 1)
        {
            return new(false, matches.Length == 0 ? $"Unknown callsign: {id}" : $"Ambiguous callsign: {id}. Use more trailing characters.");
        }

        var ac = matches[0];
        var commands = input[(split + 1)..].Trim().Split('/');
        var replies = new List<string>();
        foreach (var raw in commands)
        {
            State.Performance.CommandsIssued++;
            var command = Decode(raw.Trim());
            State.Performance.RecordCommand(command);
            var obstruction = ac.SpeedKnots < 0.5 ? GroundObstruction(ac) : null;
            var reportObstruction = obstruction is not null && !WaitingInDepartureQueue(ac, obstruction);
            CommandResult result;
            try
            {
                result = Apply(ac, command);
            }
            catch (InvalidDataException e) { result = new(false, e.Message); }
            if (command == "say again" && result.Success)
            {
                if (ac.LastRadioMessage is { } repeated)
                {
                    Transmit(ac, result.Message, repeated.IntentKey, repeated.DistanceNm, repeated.Runway, repeated.Value, repeated.Detail, repeated.IncludeRunway);
                }
                else
                {
                    Transmit(ac, result.Message);
                }
            }
            else if (result.Success && reportObstruction)
            {
                const string text = "There's an aircraft in front of me.";
                Transmit(ac, text, "ground.blocked");
                ac.TrafficObstructionReported = true;
                result = result with { Message = text };
            }
            else
            {
                var intent = result.Success ? command switch
                {
                    _ when command == "pushback" || command.StartsWith("pushback ", StringComparison.Ordinal) => "command.PB.controller",
                    "land" or "cleared to land" or "cleared for landing" => "command.CL.pilot",
                    "go around" or "ga" => "command.G.pilot",
                    "taxi to parking" => "command.TP.pilot",
                    _ when command.StartsWith("taxi to runway ", StringComparison.Ordinal) => "command.T.pilot",
                    _ when command.StartsWith("hold short ", StringComparison.Ordinal) => "command.HS.pilot",
                    "takeoff" or "cleared for takeoff" => "command.CT.pilot",
                    "touch and go" => "command.CG.pilot",
                    "low approach" => "command.CA.pilot",
                    "line up" or "line up and wait" => "command.TH.pilot",
                    "hold" or "hold position" => "command.HY.pilot",
                    "continue" or "continue taxi" => "command.TU.pilot",
                    "cancel takeoff" => "command.CC.pilot",
                    "extend downwind" => "command.X.pilot",
                    "short approach" => "command.MS.pilot",
                    "break out" => "command.B.pilot",
                    _ => ""
                } : "command.rejected";
                var radio = CommandReadback(ac, command, result, intent);
                if (!result.Success)
                {
                    Message?.Invoke($"{ac.CallSign}: {result.Message}");
                }
                Transmit(ac, radio.Text, radio.IntentKey, radio.DistanceNm, radio.Runway, radio.Value, radio.Detail, radio.IncludeRunway);
                if (result.Success)
                {
                    result = result with { Message = radio.Text };
                }
            }
            replies.Add(result.Message);
            if (!result.Success)
            {
                State.Performance.CommandErrors++;
                return new(false, string.Join(" ", replies));
            }
        }
        ac.WaitingSeconds = 0;
        return new(true, string.Join(" ", replies));
    }
    private static string Decode(string text)
    {
        if (text.Equals("PB", StringComparison.OrdinalIgnoreCase) || text.StartsWith("PB ", StringComparison.OrdinalIgnoreCase))
        {
            return text.Length == 2 ? "pushback" : "pushback " + text[3..].Trim().ToLowerInvariant();
        }
        if (Codes.TryGetValue(text, out var command))
        {
            return command;
        }

        var match = Regex.Match(text, @"^(ELD|ERD|ELB|ERB|PLD|PRD|PLB|PRB|EF)(\d{1,2}[LCR]?)?$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return "pattern " + match.Groups[1].Value.ToUpperInvariant() + " " + match.Groups[2].Value.ToUpperInvariant();
        }

        if (Regex.IsMatch(text, @"^T\d{1,2}[LCR]?$", RegexOptions.IgnoreCase))
        {
            return "taxi to runway " + text[1..].ToUpperInvariant();
        }

        if (Regex.IsMatch(text, @"^HS(\d{1,2}[LCR]?)?$", RegexOptions.IgnoreCase))
        {
            return "hold short " + text[2..].ToUpperInvariant();
        }

        if (text.StartsWith("ULH", StringComparison.OrdinalIgnoreCase) || text.StartsWith("URH", StringComparison.OrdinalIgnoreCase))
        {
            return "heading " + text[1] + " " + text[3..];
        }

        if (text.StartsWith("PF", StringComparison.OrdinalIgnoreCase))
        {
            return "report final " + text[2..];
        }

        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }

    private void Transmit(Aircraft ac, string text, string intentKey = "", double? distanceNm = null, string? spokenRunway = null,
        double? value = null, string detail = "", bool includeRunway = true)
    {
        if (!ac.IsOnFrequency)
        {
            return;
        }
        ac.LastTransmission = text;
        var radio = new AircraftRadioMessage(ac.CallSign, State.Airport.Code, text, spokenRunway ?? ac.Runway, intentKey, distanceNm, value, detail, includeRunway);
        ac.LastRadioMessage = radio;
        Message?.Invoke($"{ac.CallSign}: {text}");
        RadioTransmission?.Invoke(ac.CallSign, text);
        RadioMessage?.Invoke(radio);
    }

    private CommandResult? ApplyOriginal(Aircraft ac, string command)
    {
        CommandResult Ok(string s) => new(true, s);
        CommandResult No(string s) => new(false, s);
        if (ac.Phase == FlightPhase.AbortedTakeoff && command is not ("takeoff" or "cleared for takeoff"))
        {
            return No("Takeoff cancelled. Only CT is accepted after stopping.");
        }

        var airport = State.Airport;
        var rw = airport.GetRunway(ac.Runway);
        var threshold = airport.Point(rw.Threshold);
        if (command.StartsWith("say ", StringComparison.Ordinal))
        {
            return command switch
            {
                "say altitude" => Ok($"Altitude {ac.AltitudeFeet:0} feet AGL."),
                "say heading" => Ok($"Heading {ac.Heading:000} degrees."),
                "say rate" => Ok($"Vertical speed {ac.VerticalSpeedFpm:0} feet per minute."),
                "say speed" => Ok($"Speed {ac.SpeedKnots:0} knots."),
                "say position" => Ok(ac.IsAirborne ? $"{threshold.Distance(ac.Position) / 1852:0.0} NM bearing {threshold.Bearing(ac.Position):000} from runway {rw.Name} threshold; {ac.Phase} {ac.CircuitLeg}." : $"{ac.Phase}, runway {rw.Name}, gate {ac.Gate}."),
                "say again" => Ok(ac.LastTransmission.Length > 0 ? ac.LastTransmission : "No previous transmission."),
                _ => No("Unknown say command.")
            };
        }

        if (command == "cancel takeoff")
        {
            if (ac.Phase != FlightPhase.TakeoffRoll)
            {
                return No("No active takeoff clearance to cancel.");
            }

            ac.Phase = FlightPhase.AbortedTakeoff;
            State.RunwayDepartures.RemoveAll(departure => departure.Id == ac.RunwayDepartureId);
            ac.RunwayDepartureId = null;
            ac.LandingIntent = LandingIntent.FullStop;
            return Ok("Cancelling takeoff; stopping on the runway.");
        }
        if (command.StartsWith("hold short ", StringComparison.Ordinal))
        {
            var requested = command[11..].Trim();
            if (requested.Length == 0)
            {
                requested = ac.Runway;
            }

            var holdRunway = airport.GetRunway(requested);
            if (ac.Phase is not (FlightPhase.TaxiIn or FlightPhase.TaxiOut))
            {
                return No("HS requires taxiing traffic.");
            }
            if (DistanceToRunway(ac.Position, holdRunway) < RunwayHoldDistance - 0.001)
            {
                return No($"Too close to runway {holdRunway.Name} to hold short before it on this route.");
            }
            if (TaxiHoldDistance(ac, holdRunway) is null &&
                !(ac.Phase == FlightPhase.TaxiOut && airport.GetRunway(ac.Runway).PhysicalId == holdRunway.PhysicalId))
            {
                return No($"Current taxi route does not reach runway {holdRunway.Name}.");
            }
            var sameHold = ac.HoldShortRunway is { } previousHold && airport.GetRunway(previousHold).PhysicalId == holdRunway.PhysicalId;
            ac.HoldShortRunway = holdRunway.Name;
            ac.HoldShortReached = sameHold && ac.Holding && ac.HoldShortReached != false;
            return Ok($"Will hold short runway {requested}.");
        }
        if (command is "touch and go" or "low approach" or "option")
        {
            if (ac.Phase is not (FlightPhase.Approach or FlightPhase.Circuit))
            {
                return No("Requires approach or circuit traffic.");
            }

            var landingIntent = command == "touch and go" ? LandingIntent.TouchAndGo : command == "low approach" ? LandingIntent.LowApproach :
                ac.OriginalActions is null ? LandingIntent.Option : PlannedLandingIntent(ac);
            if (landingIntent is LandingIntent.FullStop or LandingIntent.Option && RunwayPerformance.Metres(ac.Specification.LandingDistanceFeet) is { } landingDistance &&
                RunwayPerformance.LandingExit(airport, rw, landingDistance) is null)
            {
                return No("No runway exit beyond the required landing distance.");
            }
            if (landingIntent == LandingIntent.TouchAndGo && RunwayPerformance.Metres(ac.Specification.TakeoffDistanceFeet) is { } takeoffDistance &&
                takeoffDistance > threshold.Distance(airport.Point(rw.End)))
            {
                return No("Insufficient runway for touch and go.");
            }
            ac.LandingIntent = landingIntent;
            ac.LandingCleared = true;
            var selected = landingIntent switch
            {
                LandingIntent.TouchAndGo => "touch and go",
                LandingIntent.LowApproach => "low approach",
                _ => "full stop"
            };
            return Ok($"Cleared for {command}, runway {rw.Name}." + (command == "option" ? $" Choosing {selected}." : ""));
        }
        if (command.StartsWith("pattern ", StringComparison.Ordinal))
        {
            if (!ac.IsAirborne || ac.Phase == FlightPhase.LowApproach)
            {
                return No("Pattern entry requires airborne traffic away from the runway.");
            }

            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var code = parts[1];
            if (!Regex.IsMatch(code, @"^(ELD|ERD|ELB|ERB|PLD|PRD|PLB|PRB|EF)$") || parts.Length > 3)
            {
                return No("Invalid pattern instruction.");
            }

            var runway = parts.Length > 2 ? parts[2] : ac.Runway;
            _ = airport.GetRunway(runway); // Validate before changing any aircraft state.
            BeginCircuit(ac, code == "EF" ? "final" : code.EndsWith('D') ? "downwind" : "base", code.Contains('R'), code.StartsWith('P'), runway);
            return Ok($"Enter {(ac.RightTraffic ? "right" : "left")} {ac.CircuitLeg} runway {ac.Runway}" + (ac.ReportLeg ? " and report established." : "."));
        }
        if (command.StartsWith("report final ", StringComparison.Ordinal))
        {
            if (!ac.IsAirborne)
            {
                return No("Report final requires airborne traffic.");
            }

            var args = command[13..].Split(',');
            var miles = 5.0;
            if (args.Length > 2 || (args[0].Length > 0 && (!double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out miles) || !double.IsFinite(miles) || miles < 0.5 || miles > 20)))
            {
                return No("Use PF, PF<miles>, or PF<miles>,<runway>; range 0.5–20 NM.");
            }

            var requested = airport.GetRunway(args.Length == 2 ? args[1] : ac.Runway);
            // GAMENT.DLL REPORT_[XMILE_]FINAL rebuilds final via 0x10009E30,
            // which clears the old landing intent even on the same runway.
            BeginCircuit(ac, "final", ac.RightTraffic, false, requested.Name, Math.Max(3, miles));
            ac.ReportFinalNm = miles;
            return Ok($"Will report {miles:0.0} mile final runway {ac.Runway}.");
        }
        if (command == "extend downwind")
        {
            if (ac.Phase != FlightPhase.Circuit || ac.CircuitLeg != "downwind")
            {
                return No("Aircraft must be on downwind.");
            }

            ac.ExtendDownwind = true;
            return Ok("Extending downwind; awaiting base instruction.");
        }
        if (command == "short approach")
        {
            if (ac.Phase is not (FlightPhase.Circuit or FlightPhase.Approach))
            {
                return No("Aircraft must be in the approach pattern.");
            }

            var clearance = ac.LandingCleared;
            var intent = ac.LandingIntent;
            var speed = ac.ApproachSpeedOverrideKnots ?? (ac.GeneratedArrival is { Stage: not GeneratedArrivalStage.Cancelled } arrival ? arrival.SpeedOverrideKnots : null);
            BeginCircuit(ac, "final", ac.RightTraffic, false, ac.Runway, 1);
            ac.LandingCleared = clearance;
            ac.LandingIntent = intent;
            ac.ApproachSpeedOverrideKnots = speed;
            if (speed is { } target)
            {
                ac.TargetSpeedKnots = target;
            }
            return Ok("Making short approach.");
        }
        if (command is "turn out" or "left turn out" or "right turn out" or "break out")
        {
            if (!ac.IsAirborne || ac.IsOnRunway)
            {
                return No("Requires airborne traffic away from the runway.");
            }
            ac.FollowOriginalDepartureRoute = false;
            ac.FollowOriginalDepartureAltitude = false;
            CancelOriginalRoute(ac);
            ResetAirborneInstruction(ac);
            ac.LandingCleared = false;
            ac.LandingIntent = LandingIntent.FullStop;
            ac.ReportFinalNm = 0;
            if (command == "break out")
            {
                ac.Phase = FlightPhase.Breakout;
                var h = threshold.Bearing(airport.Point(rw.End));
                var center = threshold.Offset(h + 180, 8 * 1852).Offset(h + 90, 3 * 1852);
                SetPath(ac, [center.Offset(0, 1000), center.Offset(90, 1000), center.Offset(180, 1000), center.Offset(270, 1000)]);
            }
            else
            {
                ac.Phase = FlightPhase.Departing;
                ac.DepartureTurnHeading = command == "left turn out" ? Coordinates.NormalizeHeading(ac.Heading - 90)
                    : command == "right turn out" ? Coordinates.NormalizeHeading(ac.Heading + 90) : null;
            }
            return Ok(command == "break out" ? "Breaking out; will hold outside the pattern for instructions." : "Turning out of traffic.");
        }
        if (command.StartsWith("heading ", StringComparison.Ordinal) || command.StartsWith("turn left ", StringComparison.Ordinal) || command.StartsWith("turn right ", StringComparison.Ordinal))
        {
            if (!ac.IsAirborne || ac.IsOnRunway)
            {
                return No("Turns require airborne traffic away from the runway.");
            }

            var parts = command.Split(' ');
            if (parts.Length != 3 || (parts[0] == "heading" && !parts[1].Equals("L", StringComparison.OrdinalIgnoreCase) && !parts[1].Equals("R", StringComparison.OrdinalIgnoreCase)))
            {
                return No("Use ULH<degrees> or URH<degrees>.");
            }

            var direction = parts[1].Equals("L", StringComparison.OrdinalIgnoreCase) || parts[1] == "left" ? -1 : 1;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < 0 || value > 360)
            {
                return No("Heading must be 0–360 degrees.");
            }

            var degrees = parts[0] == "heading" ? (direction == 1 ? (value - ac.Heading + 360) % 360 : (ac.Heading - value + 360) % 360) : value;
            var resume = parts[0] == "turn" && value == 360;
            if (!resume)
            {
                ac.DepartureTurnHeading = null;
                CancelOriginalRoute(ac);
                ac.FollowOriginalDepartureRoute = false;
                ac.FollowOriginalDepartureAltitude = false;
            }
            // A full orbit resumes the current instruction. A permanent heading
            // must not resurrect the approach which preceded that vector.
            ac.ResumePhase = ac.Phase == FlightPhase.Vectoring && ac.ResumeAfterTurn ? ac.ResumePhase : ac.Phase;

            ac.ResumeAfterTurn = resume;
            ac.TurnRemaining = degrees;
            ac.TurnDirection = direction;
            ac.Phase = FlightPhase.Vectoring;
            if (!resume)
            {
                ac.LandingCleared = false;
                ac.LandingIntent = LandingIntent.FullStop;
                ac.ReportFinalNm = 0;
                ac.ReportLeg = false;
                ac.ExtendDownwind = false;
                ac.CircuitLeg = "";
            }
            return Ok($"Turning {(direction == 1 ? "right" : "left")} {degrees:0} degrees.");
        }
        return null;
    }

    private static void ResetAirborneInstruction(Aircraft aircraft)
    {
        aircraft.ApproachSpeedOverrideKnots = null;
        aircraft.DepartureTurnHeading = null;
        aircraft.TurnRemaining = 0;
        aircraft.TurnDirection = 1;
        aircraft.ResumeAfterTurn = false;
        aircraft.ResumePhase = FlightPhase.Approach;
        aircraft.ReportLeg = false;
        aircraft.ExtendDownwind = false;
        aircraft.ReportFinalNm = 0;
        aircraft.CircuitLeg = "";
    }

    private void BeginCircuit(Aircraft ac, string leg, bool right, bool report, string runway, double? finalNm = null)
    {
        ac.TouchdownDistanceMetres = null;
        CancelOriginalRoute(ac);
        var rw = State.Airport.GetRunway(runway);
        var t = State.Airport.Point(rw.Threshold);
        var h = t.Bearing(State.Airport.Point(rw.End));
        var pattern = State.Airport.Original;
        var final = t.Offset(h + 180, finalNm is { } miles ? miles * 1852 : pattern.PatternDistance(ac.Type, "final") ?? 3 * 1852);
        var side = right ? 90 : -90;
        var upwind = pattern.PatternDistance(ac.Type, "upwind") ?? 1852;
        var width = pattern.PatternDistance(ac.Type, "base") ?? 2 * 1852;

        ResetAirborneInstruction(ac);
        ac.TargetSpeedKnots = ac.Specification.PatternSpeed ?? ac.Specification.ApproachSpeed;
        ac.Runway = rw.Name;
        ac.Phase = FlightPhase.Circuit;
        ac.GoAroundApproachRequests = 0;
        ac.CircuitLeg = leg;
        ac.RightTraffic = right;
        ac.ReportLeg = report;
        ac.ExtendDownwind = false;
        ac.LandingCleared = false;
        ac.LandingIntent = LandingIntent.FullStop;
        ac.ReportFinalNm = 0;

        SetPath(ac, leg switch
        {
            "downwind" => [t.Offset(h, upwind).Offset(h + side, width), final.Offset(h + side, width), final],
            "base" => [final.Offset(h + side, width), final],
            _ => [final]
        }, h);
    }

    private bool UpdateOriginal(Aircraft ac, double dt)
    {
        if (ac.Phase == FlightPhase.AbortedTakeoff)
        {
            var old = ac.SpeedKnots;
            if (RunwayPerformance.Metres(ac.Specification.StoppingDistanceFeet) is { } stoppingDistance)
            {
                var braking = RunwayPerformance.Acceleration(ac.Specification.TakeoffSpeed, 0, stoppingDistance);
                ac.SpeedKnots = Approach(ac.SpeedKnots, 0, braking / MetresPerKnotSecond * dt);
            }
            else
            {
                ApproachSpeed(ac, 0, dt, false, 8);
            }
            var distance = (old + ac.SpeedKnots) / 2 * MetresPerKnotSecond * dt;
            if (ac.Path.Count == 3 && ac.PathIndex == 1)
            {
                Move(ac, distance);
                return true;
            }
            var runway = State.Airport.GetRunway(ac.Runway);
            var end = State.Airport.Point(runway.End);
            var heading = State.Airport.Point(runway.Threshold).Bearing(end);
            var remaining = Math.Max(0, (end.X - ac.Position.X) * Math.Sin(heading * Math.PI / 180) + (end.Y - ac.Position.Y) * Math.Cos(heading * Math.PI / 180));
            ac.Position = ac.Position.Offset(heading, distance);
            if (distance > remaining + 0.01 && !ac.RunwayOverrunReported)
            {
                ac.RunwayOverrunReported = true;
                State.Performance.RunwayOverruns++;
                State.Performance.Incidents++;
                Message?.Invoke($"{ac.CallSign}: runway overrun after cancelled takeoff.");
            }
            return true;
        }
        if (ac.Phase == FlightPhase.Vectoring)
        {
            if (ac.ApproachSpeedOverrideKnots is { } speed)
            {
                ApproachSpeed(ac, speed, dt, true, 2);
            }
            var turn = Math.Min(ac.TurnRemaining, dt * FlightDynamics.TurnRate(ac));
            ac.Heading = (ac.Heading + turn * ac.TurnDirection + 360) % 360;
            ac.TurnRemaining -= turn;
            FlyHeading(ac, ac.Heading, dt);
            if (ac.TurnRemaining < 0.000001 && ac.ResumeAfterTurn)
            {
                ac.Phase = ac.ResumePhase;
                ac.ResumeAfterTurn = false;
            }
            else if (ac.TurnRemaining < 0.000001 && ac.ResumePhase == FlightPhase.Departing &&
                ac.GeneratedDeparture is { Stage: GeneratedDepartureStage.Cancelled })
            {
                ac.Phase = FlightPhase.Departing;
            }
            return true;
        }
        if (ac.Phase == FlightPhase.Breakout)
        {
            ApproachAltitude(ac, 2000, dt, 1200);
            if (Move(ac, ac.SpeedKnots * MetresPerKnotSecond * dt))
            {
                ac.PathIndex = 0;
            }

            return true;
        }
        if (ac.Phase == FlightPhase.LowApproach)
        {
            ac.AltitudeFeet = 50;
            if (Move(ac, ac.SpeedKnots * MetresPerKnotSecond * dt))
            {
                if (!ContinueOriginalActionsAfterAirborne(ac))
                {
                    BeginCircuit(ac, "downwind", ac.RightTraffic, false, ac.Runway);
                }
                TransmitOnTheGo(ac, true);
            }
            return true;
        }
        if (ac.Phase != FlightPhase.Circuit)
        {
            return false;
        }

        ApproachSpeed(ac, ac.TargetSpeedKnots, dt, true, 2);
        ApproachAltitude(ac, State.Airport.Original.PatternAltitudeAgl(ac.Type) ?? 1500, dt, 1200);
        var index = ac.PathIndex;
        if (ac.ExtendDownwind && index >= 1)
        {
            var rw = State.Airport.GetRunway(ac.Runway);
            var course = (State.Airport.Point(rw.Threshold).Bearing(State.Airport.Point(rw.End)) + 180) % 360;
            FlyTrack(ac, course, dt);
            return true;
        }
        var done = Move(ac, ac.SpeedKnots * MetresPerKnotSecond * dt);
        if (index == 0 && ac.PathIndex > 0 && ac.ReportLeg)
        {
            if (ac.CircuitLeg == "downwind")
            {
                Transmit(ac, "Downwind.", "flight.downwind");
            }
            else
            {
                TransmitBaseReport(ac);
            }
            ac.ReportLeg = false;
        }
        if (ac.CircuitLeg == "downwind" && ac.PathIndex >= 2)
        {
            ac.CircuitLeg = "base";
            TransmitBaseReport(ac);
        }

        if (done)
        {
            ac.Phase = FlightPhase.Approach;
            ac.CircuitLeg = "final";
            ac.TargetSpeedKnots = ac.ApproachSpeedOverrideKnots ?? ac.Specification.ApproachSpeed;
            Transmit(ac, "Turning final.", "flight.turning.final");
        }
        return true;
    }

    private bool RunwaySegmentsIntersect(Runway a, Runway b)
    {
        var p = State.Airport.Point(a.Threshold);
        var q = State.Airport.Point(a.End);
        var r = State.Airport.Point(b.Threshold);
        var s = State.Airport.Point(b.End);
        static double Cross(Position a, Position b, Position c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        return Cross(p, q, r) * Cross(p, q, s) <= 0 && Cross(r, s, p) * Cross(r, s, q) <= 0 &&
            Math.Max(Math.Min(p.X, q.X), Math.Min(r.X, s.X)) <= Math.Min(Math.Max(p.X, q.X), Math.Max(r.X, s.X)) &&
            Math.Max(Math.Min(p.Y, q.Y), Math.Min(r.Y, s.Y)) <= Math.Min(Math.Max(p.Y, q.Y), Math.Max(r.Y, s.Y));
    }
}
