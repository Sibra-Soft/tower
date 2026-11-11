using Tower.Core;
using Tower.Infrastructure;
using System.Globalization;
using Xunit;

namespace Tower.Tests;

public sealed class CompatibilityTests
{
    [Theory]
    [InlineData(1, 1, 100)]
    [InlineData(1, 2, 50)]
    [InlineData(3, 2, 150)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 8, 13)]
    public void Original_traffic_rate_caption(int aircraft, int minutes, int expected)
    {
        var adjusted = new TrafficRate(aircraft, minutes).Adjust(TrafficRateField.Aircraft);
        Assert(adjusted.Rate == new TrafficRate(aircraft, minutes));
        Assert(adjusted.Display == expected);
    }

    [Theory]
    [InlineData(2, 1, TrafficRateField.Aircraft, 1, 1)]
    [InlineData(4, 2, TrafficRateField.Aircraft, 3, 2)]
    [InlineData(3, 1, TrafficRateField.Minutes, 3, 2)]
    [InlineData(2, 1, TrafficRateField.Minutes, 2, 1)]
    [InlineData(99, 1, TrafficRateField.Minutes, 99, 66)]
    public void Original_traffic_rate_correction_depends_on_edited_field(int aircraft, int minutes, TrafficRateField field, int expectedAircraft, int expectedMinutes)
    {
        var adjusted = new TrafficRate(aircraft, minutes).Adjust(field);
        Assert(adjusted.Rate == new TrafficRate(expectedAircraft, expectedMinutes));
        Assert(adjusted.Display == 150);
        Near(adjusted.Rate.PerMinute, (double)expectedAircraft / expectedMinutes);
    }
    [Fact]
    public void Traffic_ratio_controls_spawn_interval_and_survives_serialization()
    {
        var rate = new TrafficRate(5, 2);
        Near(rate.PerMinute, 2.5);
        Assert(TrafficRate.FromPerMinute(0.5) == new TrafficRate(1, 2));
        Assert(TrafficRate.FromPerMinute(2.5) == new TrafficRate(5, 2));
        var sim = New();
        sim.State.Options.AircraftPerMinute = rate.PerMinute;
        sim.State.Options.TrafficAircraftCount = rate.Aircraft;
        sim.State.Options.TrafficIntervalMinutes = rate.Minutes;
        sim.Advance(23);
        Assert(sim.State.Aircraft.Count == 0);
        sim.Advance(2);
        Assert(sim.State.Aircraft.Count == 1);
        sim.Advance(24);
        Assert(sim.State.Aircraft.Count == 2);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Session>(System.Text.Json.JsonSerializer.Serialize(sim.State, JsonStore.Options), JsonStore.Options)!;
        Near(restored.Options.AircraftPerMinute, 2.5);
        Assert(restored.Options.TrafficAircraftCount == 5 && restored.Options.TrafficIntervalMinutes == 2);
    }
    [Fact]
    public void Vertical_rates_select_continuous_bands_and_preserve_fallbacks()
    {
        var spec = new AircraftSpecification();
        spec.ClimbRatesFpm[AltitudeBand.SurfaceTo500] = 600;
        spec.ClimbRatesFpm[AltitudeBand.To5000] = 1200;
        spec.DescentRatesFpm[AltitudeBand.To5000] = 900;
        Near(VerticalPerformance.Rate(spec, 500, true, 1800), 600);
        Near(VerticalPerformance.Rate(spec, 500.01, true, 1800), 1200);
        Near(VerticalPerformance.Rate(spec, 5000, false, 1200), 900);
        Near(VerticalPerformance.Rate(spec, 5000.01, false, 1200), 1200);
        Assert(VerticalPerformance.Band(10000) == AltitudeBand.To10000);
        Assert(VerticalPerformance.Band(25000) == AltitudeBand.To25000);
        Assert(VerticalPerformance.Band(25000.01) == AltitudeBand.Above25000);
        spec.ClimbRatesFpm[AltitudeBand.SurfaceTo500] = double.NaN;
        Near(VerticalPerformance.Rate(spec, 0, true, 1800), 1800);
    }

    [Fact]
    public void Departure_and_final_follow_imported_vertical_rates_without_teleporting()
    {
        var sim = New();
        var dep = sim.AddAircraft(FlightDirection.Departure, "CLIMB");
        dep.Phase = FlightPhase.Departing;
        dep.AltitudeFeet = 100;
        dep.Specification.ClimbRatesFpm[AltitudeBand.SurfaceTo500] = 600;
        sim.Advance(10);
        Near(dep.AltitudeFeet, 200);
        Near(dep.VerticalSpeedFpm, 600);
        var approach = New();
        var arr = approach.AddAircraft(FlightDirection.Arrival, "DESCEND");
        arr.Specification.DescentRatesFpm[AltitudeBand.To5000] = 600;
        arr.AltitudeFeet = 3000;
        approach.Advance(1);
        Near(arr.AltitudeFeet, 2990);
        Near(arr.VerticalSpeedFpm, -600);
        var rw = approach.State.Airport.GetRunway(arr.Runway);
        arr.Position = approach.State.Airport.Point(rw.Threshold).Towards(approach.State.Airport.Point(rw.End), 50);
        Assert(approach.Execute("DESCEND/CL").Success);
        approach.Advance(0.05);
        Assert(arr.Phase == FlightPhase.GoAround);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Original_profiles_import_and_serialize_both_vertical_tables()
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Xunit.Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original PRF tables.");
            return;
        }
        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(directory, code);
            Assert(airport.AircraftTypes.Values.All(s => s.ClimbRatesFpm.Count == 5 && s.DescentRatesFpm.Count == 5));
            Near(airport.Difficulty, 50);
            var spec = airport.AircraftTypes["C182"];
            Near(VerticalPerformance.Rate(spec, 100, true, 1800), 700);
            Near(VerticalPerformance.Rate(spec, 100, false, 1200), 700);
            var restored = System.Text.Json.JsonSerializer.Deserialize<AircraftSpecification>(System.Text.Json.JsonSerializer.Serialize(spec, JsonStore.Options), JsonStore.Options)!;
            Near(VerticalPerformance.Rate(restored, 6000, true, 1800), 700);
            Near(VerticalPerformance.Rate(restored, 6000, false, 1200), 700);
        }
    }
    [Fact]
    [Trait("Category", "Integration")]
    public void Imported_categories_select_airport_specific_pattern_altitudes()
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Xunit.Assert.Skip("Set TOWER_ORIGINAL_DATA to enable original PAT/PRF checks.");
            return;
        }
        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(directory, code);
            foreach (var spec in airport.AircraftTypes.Values)
            {
                Assert(spec.WeightCategory is >= 1 and <= 3);
                Near(airport.Original.PatternCategoryByType[spec.Type], spec.WeightCategory!.Value);
                Near(airport.Original.PatternAltitude(spec.Type)!.Value, 500 + spec.WeightCategory.Value * 500);
                Near(airport.Original.PatternDistance(spec.Type, "final")!.Value,
                    airport.Original.PatternValues[$"cat{spec.WeightCategory}_final"] * OriginalPatternUnits.MetresPerSourceUnit);
            }
            var sim = new Simulation(new Session { Airport = airport, Options = new() { Runway = airport.Runways[0].Name, AircraftPerMinute = 0 } });
            var ac = sim.AddAircraft(FlightDirection.Arrival, "PATCAT", "C182");
            Until(sim, () => ac.IsOnFrequency);
            var runway = airport.GetRunway(ac.Runway);
            ac.Position = airport.Point(runway.Threshold).Offset(0, 30000);
            ac.AltitudeFeet = 0;
            Assert(sim.Execute("PATCAT/ELD" + runway.Name).Success);
            sim.Advance(100);
            var sourceAltitude = 1000;
            Near(ac.AltitudeFeet, sourceAltitude - airport.Original.FieldElevationFeetMsl!.Value);
            var restored = System.Text.Json.JsonSerializer.Deserialize<Session>(System.Text.Json.JsonSerializer.Serialize(sim.State, JsonStore.Options), JsonStore.Options)!;
            Near(restored.Airport.Original.PatternAltitude("C182")!.Value, 1000);
            Near(restored.Airport.Original.PatternAltitudeAgl("C182")!.Value, ac.AltitudeFeet);
            Near(restored.Aircraft[0].Specification.WeightCategory!.Value, 1);
        }
    }

    [Fact]
    public void Unavailable_or_invalid_pattern_values_keep_safe_fallbacks()
    {
        var metadata = new OriginalMetadata();
        metadata.PatternCategoryByType["TEST"] = 2;
        Assert(metadata.PatternAltitude("TEST") is null);
        metadata.PatternValues["cat2_altit"] = double.NaN;
        Assert(metadata.PatternAltitude("TEST") is null);
        metadata.PatternValues["cat2_final"] = double.MaxValue;
        metadata.PatternMetresPerUnit = 2;
        Assert(metadata.PatternDistance("TEST", "final") is null);
        Assert(metadata.PatternAltitude("UNKNOWN") is null);
    }
    private static void Assert(bool condition, string message = "Assertion failed")
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
    private static void Near(double a, double b, double tolerance = 0.001) => Assert(Math.Abs(a - b) <= tolerance, $"{a} != {b}");
    private static void Throws(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException) { return; }
        throw new Exception("Expected rejection");
    }
    private static Simulation New() => new(new Session { Options = new() { AircraftPerMinute = 0 } });
    private static void Until(Simulation sim, Func<bool> predicate, int limit = 3000)
    {
        for (var i = 0; i < limit && !predicate(); i++)
        {
            sim.Advance(1);
        }

        Assert(predicate(), "Expected phase was not reached.");
    }

    private static string CanyonFixtures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Canyon");
        if (!Directory.Exists(path))
        {
            var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
            if (!string.IsNullOrWhiteSpace(data) && OriginalData.Find(data, "CYN_MIX.SIM") is { } scenario)
            {
                return Path.GetDirectoryName(scenario)!;
            }
            Xunit.Assert.Skip("Provide the original Fixtures/Canyon files to enable this integration test.");
        }

        return path;
    }

    [Fact(DisplayName = "Explicit altitude and heading references round trip")]
    public void Explicit_altitude_and_heading_references_round_trip()
    {
        Near(Coordinates.AglToMsl(1500, 667), 2167);
        Near(Coordinates.MslToAgl(2167, 667), 1500);
        Near(Coordinates.TrueToMagnetic(2, 5), 357);
        Near(Coordinates.MagneticToTrue(357, 5), 2);
    }

    [Fact(DisplayName = "Taxi redirection preserves position and a continuous route")]
    public void Taxi_redirection_preserves_position_and_a_continuous_route()
    {
        var sim = New();
        var ac = sim.AddAircraft(FlightDirection.Departure, "REDIR");
        Assert(sim.Execute("REDIR/T18").Success);
        sim.Advance(10);
        var position = ac.Position;
        Assert(sim.Execute("REDIR/T18").Success);
        Near(ac.Position.Distance(position), 0);
        Assert(ac.Path[0] == position);
        Until(sim, () => ac.Phase == FlightPhase.HoldingShort);
    }

    [Fact(DisplayName = "PAT requires explicit units and category assignment")]
    public void PAT_requires_explicit_units_and_category_assignment()
    {
        var sim = New();
        var ac = sim.AddAircraft(FlightDirection.Arrival, "PAT");
        var data = sim.State.Airport.Original;
        data.PatternValues["cat1_final"] = 9000;
        data.PatternValues["cat1_altit"] = 1000;
        Assert(data.PatternDistance(ac.Type, "final") is null);
        data.PatternMetresPerUnit = Coordinates.MetresPerFoot;
        data.PatternCategoryByType[ac.Type] = 1;
        Assert(sim.Execute("PAT/EF18").Success);
        Near(ac.Path[^1].Distance(sim.State.Airport.Point("R18")), 2743.2);
        sim.Advance(100);
        Near(ac.AltitudeFeet, 1000);
    }

    [Fact(DisplayName = "Departure can return to parking without earning arrival points")]
    public void Departure_can_return_to_parking_without_earning_arrival_points()
    {
        var sim = New();
        var ac = sim.AddAircraft(FlightDirection.Departure, "RETURN");
        Assert(sim.Execute("RETURN/T18").Success);
        sim.Advance(10);
        Assert(sim.Execute("RETURN/TP").Success);
        Until(sim, () => ac.Phase == FlightPhase.AtGate);
        Assert(sim.State.Performance.Arrivals == 0 && sim.State.Performance.Score == 0);
    }

    [Fact(DisplayName = "Circuit instruction clears prior landing clearance and rejection preserves it")]
    public void Circuit_instruction_clears_prior_landing_clearance_and_rejection_preserves_it()
    {
        var sim = New();
        var ac = sim.AddAircraft(FlightDirection.Arrival, "CLEAR");
        Assert(sim.Execute("CLEAR/CL").Success);
        Assert(!sim.Execute("CLEAR/EF99").Success);
        Assert(ac.LandingCleared);
        Assert(sim.Execute("CLEAR/EF18").Success);
        Assert(!ac.LandingCleared);
    }

    [Fact(DisplayName = "Source metadata and PRF values survive session serialization")]
    [Trait("Category", "Integration")]
    public void Source_metadata_and_PRF_values_survive_session_serialization()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Xunit.Assert.Skip("Set TOWER_ORIGINAL_DATA to enable original source metadata tests.");
            return;
        }
        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(data, code);
            Assert(airport.Original.Files[code + ".PAT"].SequenceEqual(File.ReadAllLines(OriginalData.Find(data, code + ".PAT")!)));
            Near(airport.Original.TowerElevationFeetMsl!.Value, 500);
            Near(airport.Original.SectorElevationFeetMsl!.Value, 750);
            Assert(airport.AircraftTypes.Values.All(s => s.PatternSpeed > 0 && s.OriginalPerformance.ContainsKey("Weight_Category")));
            var json = System.Text.Json.JsonSerializer.Serialize(new Session { Airport = airport }, JsonStore.Options);
            var restored = System.Text.Json.JsonSerializer.Deserialize<Session>(json, JsonStore.Options)!;
            Assert(restored.Airport.Original.Files[code + ".SEC"].SequenceEqual(airport.Original.Files[code + ".SEC"]));
            Near(restored.Airport.Original.PatternMetresPerUnit!.Value, OriginalPatternUnits.MetresPerSourceUnit);
            Assert(restored.Airport.Original.PatternUsesFixedPointRounding);
        }
    }

    [Fact(DisplayName = "North-clockwise bearings and metric offsets")]
    public void North_clockwise_bearings_and_metric_offsets()
    {
        var p = new Position(0, 0);
        Near(p.Bearing(new(0, 100)), 0);
        Near(p.Bearing(new(100, 0)), 90);
        Near(p.Offset(180, 100).Y, -100);
        Near(p.Offset(270, 100).X, -100);
    }

    [Fact(DisplayName = "Taxi route connects gate and hold without runway")]
    public void Taxi_route_connects_gate_and_hold_without_runway()
    {
        var airport = Airport.Training();
        var path = Routing.FindTaxiPath(airport, "G1", "H18");
        Assert(path[0] == airport.Point("G1") && path[^1] == airport.Point("H18"));
        Assert(!path.Contains(airport.Point("R18")));
    }

    [Fact(DisplayName = "Named route chaining rejects teleportation")]
    public void Named_route_chaining_rejects_teleportation()
    {
        Throws(() => Routing.NamedRoute(Airport.Training(), "B-A"));
    }

    [Fact(DisplayName = "Named routes connect through a shared endpoint")]
    public void Named_routes_connect_through_a_shared_endpoint()
    {
        var a = Airport.Training();
        a.Routes["C"] = ["G1", "A2"];
        a.Routes["D"] = ["E18", "A2"];
        var path = Routing.NamedRoute(a, "C-D");
        Assert(path.SequenceEqual(new[] { a.Point("G1"), a.Point("A2"), a.Point("E18") }));
    }

    [Fact(DisplayName = "Unconnected taxi graph fails")]
    public void Unconnected_taxi_graph_fails()
    {
        var a = Airport.Training();
        a.Segments.Clear();
        Throws(() => Routing.FindTaxiPath(a, "G1", "H18"));
    }

    [Fact(DisplayName = "Arrival without clearance goes around")]
    public void Arrival_without_clearance_goes_around()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "TEST1");
        Until(sim, () => a.Phase == FlightPhase.GoAround);
        Assert(!a.LandingCleared && sim.State.Performance.GoArounds == 1);
    }

    [Fact(DisplayName = "Arrival lands, vacates and parks after clearance")]
    public void Arrival_lands_vacates_and_parks_after_clearance()
    {
        var sim = New();
        var log = new List<string>();
        sim.Message += log.Add;
        var a = sim.AddAircraft(FlightDirection.Arrival, "TEST1");
        Assert(sim.Execute("TEST1 cleared to land").Success);
        Until(sim, () => a.Phase == FlightPhase.ArrivalHold);
        Assert(sim.Execute("TEST1 taxi to parking").Success);
        Until(sim, () => a.Phase == FlightPhase.Completed);
        Near(a.Position.Distance(sim.State.Airport.Point(a.Gate)), 0);
        Assert(sim.State.Performance.Arrivals == 1 && sim.State.Performance.Score == 10);
        Assert(log.Any(message => message.Contains("TEST1") && message.Contains("parked at")));
        Assert(!sim.Execute("TEST1/T18").Success);
        sim.Advance(60);
        Assert(sim.State.Performance.Arrivals == 1 && sim.State.Performance.Score == 10);
        var next = sim.AddAircraft(FlightDirection.Departure, "NEXT", assignedGate: a.Gate);
        Assert(next.Gate == a.Gate && next.Phase == FlightPhase.AtGate);
        Assert(log.Any(message => message.Contains("TEST1") && message.Contains("parked at")));
    }

    [Fact(DisplayName = "Departure taxis, lines up, takes off and hands off")]
    public void Departure_taxis_lines_up_takes_off_and_hands_off()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "TEST2");
        Assert(!sim.Execute("TEST2 cleared for takeoff").Success);
        Assert(sim.Execute("TEST2 taxi to runway 18").Success);
        Until(sim, () => a.Phase == FlightPhase.HoldingShort);
        Assert(sim.Execute("TEST2 line up and wait").Success);
        Until(sim, () => a.Phase == FlightPhase.LinedUp);
        // A curved line-up finishes on the centreline beyond the threshold.
        Near(a.Position.X, sim.State.Airport.Point("R18").X);
        Near(a.Heading, 180);
        Assert(a.Position.Y < sim.State.Airport.Point("R18").Y);
        Assert(a.Position.Distance(sim.State.Airport.Point("R18")) < 100);
        Assert(sim.Execute("TEST2 cleared for takeoff").Success);
        Until(sim, () => a.Phase == FlightPhase.Departing);
        Assert(sim.Execute("TEST2 contact departure").Success);
        Assert(a.Phase == FlightPhase.Completed && sim.State.Performance.Departures == 1);
    }

    [Fact(DisplayName = "Takeoff from hold short follows threshold access")]
    public void Takeoff_from_hold_short_follows_threshold_access()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "DEP");
        sim.Execute("DEP taxi to runway 18");
        Until(sim, () => a.Phase == FlightPhase.HoldingShort);
        Assert(sim.Execute("DEP takeoff").Success);
        var original = a.Position;
        sim.Advance(1);
        Assert(a.Position.Distance(original) < 10, "Must taxi before starting takeoff roll");
        Until(sim, () => a.Phase == FlightPhase.Departing);
    }

    [Theory]
    [InlineData("takeoff")]
    [InlineData("line up")]
    public void Landing_reservation_does_not_prevent_departure_clearance(string command)
    {
        var sim = New();
        var inbound = sim.AddAircraft(FlightDirection.Arrival, "ARR");
        var departing = sim.AddAircraft(FlightDirection.Departure, "DEP");
        departing.Phase = FlightPhase.HoldingShort;
        Assert(sim.Execute("ARR land").Success);
        Assert(sim.Execute("DEP " + command).Success);
        Assert(inbound.LandingCleared);
        Assert(departing.Phase == (command == "takeoff" ? FlightPhase.TakeoffRoll : FlightPhase.LineUp));
        Assert(sim.State.Performance.CommandErrors == 0);
    }

    [Fact(DisplayName = "Landing clearance is accepted despite reciprocal runway occupancy")]
    public void Reciprocal_runways_share_occupancy_protection()
    {
        var sim = New();
        sim.State.Airport.Runways.Add(new()
        {
            Name = "36",
            PhysicalId = "18/36",
            Threshold = "R36",
            End = "R18"
        });
        var a = sim.AddAircraft(FlightDirection.Arrival, "ARR");
        var d = sim.AddAircraft(FlightDirection.Departure, "DEP");
        d.Runway = "36";
        d.Phase = FlightPhase.LinedUp;
        Assert(sim.Execute("ARR land").Success);
    }

    [Fact(DisplayName = "Hold and continue retain taxi route")]
    public void Hold_and_continue_retain_taxi_route()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "DEP");
        sim.Execute("DEP taxi to runway 18");
        sim.Advance(5);
        Assert(sim.Execute("DEP hold position").Success);
        var brakingPosition = a.Position;
        Assert(a.SpeedKnots > 0);
        Until(sim, () => a.SpeedKnots == 0);
        Assert(a.Position.Distance(brakingPosition) is > 0 and < 30);
        var p = a.Position;
        sim.Advance(20);
        Assert(a.Position == p);
        Assert(sim.Execute("DEP continue taxi").Success);
        sim.Advance(5);
        Assert(a.Position != p);
    }

    [Fact(DisplayName = "Pause freezes all scenario clocks")]
    public void Pause_freezes_all_scenario_clocks()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival);
        var p = a.Position;
        sim.Paused = true;
        sim.Advance(100);
        Assert(sim.State.ElapsedSeconds == 0 && a.Position == p);
    }

    [Fact(DisplayName = "Simulation timing is independent of UI refresh rate")]
    public void Simulation_timing_is_independent_of_UI_refresh_rate()
    {
        var a = New();
        var b = New();
        var aa = a.AddAircraft(FlightDirection.Departure, "DEP");
        var bb = b.AddAircraft(FlightDirection.Departure, "DEP");
        a.Execute("DEP taxi to runway 18");
        b.Execute("DEP taxi to runway 18");
        a.Advance(100);
        for (var i = 0; i < 1000; i++)
        {
            b.Advance(0.1);
        }

        Near(aa.Position.Distance(bb.Position), 0, 0.02);
        Near(aa.SpeedKnots, bb.SpeedKnots, 0.02);
    }

    [Fact(DisplayName = "Invalid command and duplicate callsign do not mutate state")]
    public void Invalid_command_and_duplicate_callsign_do_not_mutate_state()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "DEP");
        Assert(!sim.Execute("DEP taxi to runway 99").Success);
        Assert(a.Phase == FlightPhase.AtGate);
        Throws(() => sim.AddAircraft(FlightDirection.Arrival, "dep"));
        Assert(!sim.Execute("MISSING land").Success);
        Throws(() => sim.Advance(double.NaN));
    }

    [Fact(DisplayName = "Headwind reduces approach groundspeed")]
    public void Headwind_reduces_approach_groundspeed()
    {
        var s1 = New();
        var s2 = New();
        s1.State.Options.WindKnots = 0;
        s2.State.Options.WindKnots = 30;
        var a = s1.AddAircraft(FlightDirection.Arrival);
        var b = s2.AddAircraft(FlightDirection.Arrival);
        var p = a.Position;
        s1.Advance(10);
        s2.Advance(10);
        Assert(p.Distance(a.Position) > p.Distance(b.Position));
    }

    [Fact(DisplayName = "Separation alert penalizes once per continuous conflict")]
    public void Separation_alert_penalizes_once_per_continuous_conflict()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "A");
        var b = sim.AddAircraft(FlightDirection.Departure, "B");
        b.Position = a.Position;
        sim.Advance(5);
        Assert(sim.State.Performance.Incidents == 1 && sim.State.Performance.Score == -20);
    }

    [Fact(DisplayName = "VOC fragment offsets and phrase bytes match VB6")]
    public void VOC_fragment_offsets_and_phrase_bytes_match_VB6()
    {
        var bank = new VoiceBank();
        byte[] raw = [128, 145, 0, 128, 128, 128, 128, 145, 0, 128, 128, 128, 128, 145, 0];
        bank.Load(2, raw);
        var f = bank.Fragments.Single(x => x.Index == 2);
        Assert(f.Offset == 1 && f.ChunkSize == 7);
        var wav = bank.BuildWave("(2:2) + (2:2)");
        Assert(wav.Length == 48);
        Assert(wav.Skip(44).SequenceEqual(new byte[] { 145, 0, 145, 0 }));
        Assert(BitConverter.ToInt32(wav, 24) == 8900 && BitConverter.ToInt16(wav, 34) == 8);
        Assert(Speak.WithYou == "(2:53) + (2:151) + (2:56) + (2:44)" && Speak.Tower == "(2:98)");
        Throws(() => bank.BuildWave("(2:999)"));
        Throws(() => bank.BuildWave("garbage"));
    }

    [Fact(DisplayName = "TWR import is culture independent and validates references")]
    public void TWR_import_is_culture_independent_and_validates_references()
    {
        var path = Path.GetTempFileName();
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            File.WriteAllText(path, "[POINTS]\nP1 TAXIWAY 1.25 2.5 A\nP2 TAXIWAY 3.5 4.0 B\n[SEGMENTS]\nS1 TAXIWAY P1 P2\n[ROUTES]\nA P1 P2 end\n");
            var airport = new Airport();
            LegacyAirportReader.ReadMap(path, airport, 2);
            Near(airport.Point("P1").X, 2.5);
            Near(airport.Point("P1").Y, 5);
        }
        finally { CultureInfo.CurrentCulture = old; File.Delete(path); }
    }

    [Fact(DisplayName = "Scenario roundtrip preserves aircraft and resumes")]
    public void Scenario_roundtrip_preserves_aircraft_and_resumes()
    {
        var path = Path.GetTempFileName();
        try
        {
            var sim = New();
            var a = sim.AddAircraft(FlightDirection.Arrival, "ARR");
            sim.Execute("ARR land");
            sim.Advance(5);
            JsonStore.Write(path, sim.State);
            var loaded = JsonStore.LoadSession(path);
            Assert(loaded.Aircraft.Single().LandingCleared);
            var resumed = new Simulation(loaded);
            sim.Advance(20);
            resumed.Advance(20);
            Near(a.Position.Distance(loaded.Aircraft.Single().Position), 0);
            loaded.Version = 99;
            JsonStore.Write(path, loaded);
            Throws(() => JsonStore.LoadSession(path));
        }
        finally { File.Delete(path); }
    }

    [Fact(DisplayName = "Original slash commands, comma separator, suffix and ambiguity")]
    public void Original_slash_commands_comma_separator_suffix_and_ambiguity()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "AAL1716");
        Assert(sim.Execute("716/T18").Success);
        Assert(a.Phase == FlightPhase.TaxiOut);
        Assert(sim.Execute("AAL1716,HY").Success && a.Holding);
        Assert(sim.Execute("AAL1716 TU").Success && !a.Holding);
        sim.AddAircraft(FlightDirection.Arrival, "UAL1716");
        Assert(!sim.Execute("716/CL").Success);
        Assert(!sim.Execute("AAL/SS").Success);
        Assert(sim.Execute("AAL1716/SS").Success);
    }

    [Fact(DisplayName = "Selected aircraft cannot intercept an explicitly addressed command")]
    public void Selected_aircraft_cannot_intercept_an_explicitly_addressed_command()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE1");
        var b = sim.AddAircraft(FlightDirection.Departure, "TWO2");
        Assert(sim.Execute("ONE1/CL", b.CallSign).Success && a.LandingCleared);
        Assert(sim.Execute("/T18", b.CallSign).Success && b.Phase == FlightPhase.TaxiOut);
        Assert(sim.Execute("hold position", b.CallSign).Success && b.Holding);
        Assert(!sim.Execute("MISSING/CL", b.CallSign).Success);
    }

    [Fact(DisplayName = "Chained commands stop on failure and keep prior accepted commands")]
    public void Chained_commands_stop_on_failure_and_keep_prior_accepted_commands()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "ONE");
        Assert(!sim.Execute("ONE/T18/CT/HY").Success);
        Assert(a.Phase == FlightPhase.TaxiOut && !a.Holding);
        Assert(sim.Execute("ONE/HY/SS").Success && a.Holding);
    }

    [Fact(DisplayName = "Say commands report live state and repeat automatic transmissions")]
    public void Say_commands_report_live_state_and_repeat_automatic_transmissions()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        var initial = a.LastTransmission;
        Assert(sim.Execute("ONE/SG").Message == initial);
        foreach (var code in new[] { "SA", "SH", "SR", "SP", "SS" })
        {
            Assert(sim.Execute("ONE/" + code).Success);
        }

        var previous = a.LastTransmission;
        Assert(sim.Execute("ONE/SG").Message == previous);
        sim.Advance(1);
        Assert(a.VerticalSpeedFpm < 0);
    }

    [Fact(DisplayName = "Report base follows circuit and emits one original base report")]
    public void Report_base_follows_circuit_and_emits_one_established_report()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        var reports = 0;
        sim.RadioMessage += message => { if (message.IntentKey == "flight.base.fullstop") { reports++; } };
        Assert(sim.Execute("ONE/PRB18/CL").Success);
        Until(sim, () => a.Phase == FlightPhase.ArrivalHold);
        Assert(reports == 1);
    }

    [Fact(DisplayName = "Enter base does not request a pilot report")]
    public void Enter_base_does_not_request_a_pilot_report()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        var reports = 0;
        sim.Message += m => { if (m.Contains("Established")) { reports++; } };
        Assert(sim.Execute("ONE/ELB18/CL").Success);
        Until(sim, () => a.Phase == FlightPhase.ArrivalHold);
        Assert(reports == 0);
    }

    [Fact(DisplayName = "Extended downwind awaits a new base command")]
    public void Extended_downwind_awaits_a_new_base_command()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        Assert(sim.Execute("ONE/ELD18/X").Success);
        sim.Advance(600);
        Assert(a.Phase == FlightPhase.Circuit && a.CircuitLeg == "downwind");
        Assert(sim.Execute("ONE/ELB18/CL").Success);
        Until(sim, () => a.Phase == FlightPhase.ArrivalHold);
    }

    [Fact(DisplayName = "Final report fires once and rejects invalid distances")]
    public void Final_report_fires_once_and_rejects_invalid_distances()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        var reports = 0;
        sim.RadioMessage += message => { if (message.IntentKey == "flight.final.distance" && message.Value == 5) { reports++; } };
        Assert(sim.Execute("ONE/PF5/CL").Success);
        Assert(!sim.Execute("ONE/PFNaN").Success);
        Until(sim, () => a.Phase == FlightPhase.ArrivalHold);
        Assert(reports == 1);
    }

    [Fact(DisplayName = "Directed heading turns respect turn side and 360 resumes approach")]
    public void Directed_heading_turns_respect_turn_side_and_360_resumes_approach()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        Assert(sim.Execute("ONE/URH90").Success);
        sim.Advance(30);
        Near(a.Heading, 270, 0.01);
        sim.Advance(60);
        Near(a.Heading, 90, 0.01);
        Assert(a.Phase == FlightPhase.Vectoring);
        Assert(!sim.Execute("ONE/ULH361").Success);
        var other = New();
        var b = other.AddAircraft(FlightDirection.Arrival, "TWO");
        Assert(other.Execute("TWO/ML3").Success);
        other.Advance(120);
        Assert(b.Phase == FlightPhase.Approach);
    }

    [Fact(DisplayName = "Touch and go returns to circuit without parking or departure score")]
    public void Touch_and_go_returns_to_circuit_without_parking_or_departure_score()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        Assert(sim.Execute("ONE/CG").Success);
        Until(sim, () => a.Phase == FlightPhase.TakeoffRoll);
        Near(a.AltitudeFeet, 0);
        Until(sim, () => a.Phase == FlightPhase.Circuit);
        Assert(sim.State.Performance.Arrivals == 0 && sim.State.Performance.Departures == 0);
    }

    [Fact(DisplayName = "Low approach remains airborne and option chooses a full stop")]
    public void Low_approach_remains_airborne_and_option_chooses_a_full_stop()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        Assert(sim.Execute("ONE/CA").Success);
        Until(sim, () => a.Phase == FlightPhase.LowApproach);
        Assert(a.IsAirborne && a.IsOnRunway && a.AltitudeFeet >= 50);
        Until(sim, () => a.Phase == FlightPhase.Circuit);
        Assert(sim.State.Performance.GoArounds == 0);
        var other = New();
        var b = other.AddAircraft(FlightDirection.Arrival, "TWO");
        Assert(other.Execute("TWO/CO").Success);
        Until(other, () => b.Phase == FlightPhase.ArrivalHold);
    }

    [Fact(DisplayName = "Cancelled takeoff brakes and reserves runway until resumed")]
    public void Cancelled_takeoff_brakes_and_reserves_runway_until_resumed()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Departure, "ONE");
        sim.Execute("ONE/T18");
        Until(sim, () => a.Phase == FlightPhase.HoldingShort);
        Assert(sim.Execute("ONE/CT").Success);
        Until(sim, () => a.SpeedKnots > 50);
        Assert(sim.Execute("ONE/CC").Success);
        var speed = a.SpeedKnots;
        sim.Advance(1);
        Assert(a.SpeedKnots < speed);
        Assert(!sim.Execute("ONE/TP").Success);
        Assert(!sim.Execute("ONE/CT").Success);
        Until(sim, () => a.SpeedKnots == 0);
        var b = sim.AddAircraft(FlightDirection.Arrival, "TWO");
        Assert(sim.Execute("TWO/CL").Success);
        Assert(b.LandingCleared);
        Assert(sim.Execute("TWO/G").Success);
        Assert(sim.Execute("ONE/CT").Success);
        Until(sim, () => a.Phase == FlightPhase.Departing);
    }

    [Fact(DisplayName = "Breakout handoff works for the original B/FD example without departure credit")]
    public void Breakout_handoff_works_for_the_original_B_FD_example_without_departure_credit()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "AAL265");
        Assert(sim.Execute("AAL265/B/FD").Success);
        Assert(a.Phase == FlightPhase.Completed && sim.State.Performance.Departures == 0);
    }

    [Fact(DisplayName = "Waiting pilots report once without timed reminders")]
    public void Waiting_pilots_report_once_without_timed_reminders()
    {
        var sim = New();
        var reports = 0;
        sim.RadioMessage += m => { if (m.IntentKey == "departure.ready") { reports++; } };
        sim.AddAircraft(FlightDirection.Departure, "ONE");
        sim.Advance(361);
        Assert(reports == 1);
        sim.Paused = true;
        sim.Advance(121);
        Assert(reports == 1);
    }

    [Fact(DisplayName = "Crossing runway clearances do not reserve each other's full length")]
    public void Crossing_runway_clearances_do_not_reserve_each_others_full_length()
    {
        var sim = New();
        var airport = sim.State.Airport;
        airport.Nodes.Add("C09", new("C09", "RUNWAY", new(-1500, 0)));
        airport.Nodes.Add("C27", new("C27", "RUNWAY", new(1500, 0)));
        airport.Runways.Add(new()
        {
            Name = "09",
            PhysicalId = "09/27",
            Threshold = "C09",
            End = "C27"
        });
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        Assert(sim.Execute("ONE/CL").Success);
        var b = sim.AddAircraft(FlightDirection.Arrival, "TWO");
        Assert(sim.Execute("TWO/ERB09").Success);
        Assert(sim.Execute("TWO/CL").Success);
        airport.Nodes["C09"] = new("C09", "RUNWAY", new(5000, 1500));
        airport.Nodes["C27"] = new("C27", "RUNWAY", new(5000, -1500));
        Assert(sim.Execute("TWO/CL").Success);
    }

    [Fact(DisplayName = "Maneuver and radio state survives save and resume")]
    public void Maneuver_and_radio_state_survives_save_and_resume()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "ONE");
        sim.Execute("ONE/ML3");
        sim.Advance(20);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, sim.State);
            var copy = new Simulation(JsonStore.LoadSession(path));
            sim.Advance(50);
            copy.Advance(50);
            Near(a.Position.Distance(copy.State.Aircraft[0].Position), 0);
            Near(a.TurnRemaining, copy.State.Aircraft[0].TurnRemaining);
            Assert(a.LastTransmission == copy.State.Aircraft[0].LastTransmission);
        }
        finally { File.Delete(path); }
    }

    [Fact(DisplayName = "Multiple configured arrival runways alternate independently of departure spawning")]
    public void Multiple_configured_arrival_runways_alternate_independently_of_departure_spawning()
    {
        var sim = New();
        var airport = sim.State.Airport;
        airport.Runways.Add(new()
        {
            Name = "36",
            Threshold = "R36",
            End = "R18"
        });
        sim.State.Options.ArrivalRunways = ["18", "36"];
        Assert(sim.AddAircraft(FlightDirection.Arrival, "A").Runway == "18");
        sim.AddAircraft(FlightDirection.Departure, "D");
        Assert(sim.AddAircraft(FlightDirection.Arrival, "B").Runway == "36");
        Assert(sim.AddAircraft(FlightDirection.Arrival, "C").Runway == "18");
    }

    [Fact(DisplayName = "Performance counts addressed commands and rejections separately")]
    public void Performance_counts_addressed_commands_and_rejections_separately()
    {
        var sim = New();
        sim.AddAircraft(FlightDirection.Departure, "ONE");
        sim.Execute("ONE/SS/CT");
        Assert(sim.State.Performance.CommandsIssued == 2 && sim.State.Performance.CommandErrors == 1);
    }

    [Fact(DisplayName = "Malformed internal command text is rejected without crashing")]
    public void Malformed_internal_command_text_is_rejected_without_crashing()
    {
        var sim = New();
        var a = sim.AddAircraft(FlightDirection.Arrival, "LAND");
        foreach (var command in new[] { "heading L", "heading Q 90", "pattern nonsense", "ULHNaN", "URH-1", "PF5,18,36" })
        {
            Assert(!sim.Execute("LAND " + command).Success);
        }

        Assert(a.Phase == FlightPhase.Approach);
        sim.AddAircraft(FlightDirection.Departure, "OTHER");
        Assert(sim.Execute("LAND/CL", "OTHER").Success && a.LandingCleared);
    }

    [Fact(DisplayName = "Original Canyon layout imports four directions, gates and connected taxi routes")]
    public void Original_Canyon_layout_imports_four_directions_gates_and_connected_taxi_routes()
    {
        var airport = OriginalData.LoadCanyon(CanyonFixtures());
        Assert(airport.Runways.Select(r => r.Name).Order().SequenceEqual(new[] { "14", "27", "32", "9" }));
        Assert(airport.AircraftTypes.Count == 37 && airport.Outlines.ContainsKey("B737"));
        Near(airport.AircraftTypes["C182"].ApproachSpeed, 70);
        Assert(airport.AircraftTypes["C182"].Name == "Skylane");
        foreach (var gate in airport.Nodes.Values.Where(n => n.Kind == "GATE"))
        {
            foreach (var runway in airport.Runways)
            {
                Assert(Routing.FindTaxiPath(airport, gate.Id, runway.Hold).Count > 1);
                Assert(Routing.FindTaxiPath(airport, runway.Exit, gate.Id).Count > 1);
            }
        }
    }

    [Fact(DisplayName = "Original Canyon arrival and departure work on every runway")]
    public void Original_Canyon_arrival_and_departure_work_on_every_runway()
    {
        foreach (var name in new[] { "9", "27", "14", "32" })
        {
            var airport = OriginalData.LoadCanyon(CanyonFixtures());
            var sim = new Simulation(new Session { Airport = airport, Options = new() { Runway = name, AircraftPerMinute = 0 } });
            var a = sim.AddAircraft(FlightDirection.Arrival, "ARR", "B727", useTrafficFlow: false);
            Near(a.SpeedKnots, 136);
            Assert(sim.Execute("ARR/CL").Success);
            Until(sim, () => a.Phase == FlightPhase.ArrivalHold);
            Assert(sim.Execute("ARR/TP").Success);
            Until(sim, () => a.Phase == FlightPhase.Completed);
            var b = sim.AddAircraft(FlightDirection.Departure, "DEP", "C182", useTrafficFlow: false);
            if (b.Pushback is not null)
            {
                Assert(sim.Execute("DEP/PB").Success);
                Until(sim, () => b.Phase == FlightPhase.ReadyForTaxi);
            }
            Assert(sim.Execute("DEP/T" + name).Success);
            Until(sim, () => b.Phase == FlightPhase.HoldingShort);
            Assert(sim.Execute("DEP/CT").Success);
            Until(sim, () => b.Phase == FlightPhase.Departing);
            Assert(b.SpeedKnots < 100);
        }
    }

    [Fact(DisplayName = "Original MIX timetable preserves callsigns, types, times, gates and runways")]
    public void Original_MIX_timetable_preserves_callsigns_types_times_gates_and_runways()
    {
        var airport = OriginalData.LoadCanyon(CanyonFixtures());
        var flights = OriginalData.ReadSchedule(Path.Combine(CanyonFixtures(), "CYN_MIX.SIM"), airport);
        Assert(flights.Count == 6 && flights[0].CallSign == "NWA135" && flights[1].Gate == "G8" && flights[^1].TimeSeconds == 270);
        var sim = new Simulation(new Session { Airport = airport, Schedule = flights, Options = new() { Runway = "9", AircraftPerMinute = 0 } });
        sim.Advance(1);
        Assert(sim.State.Aircraft.Count == flights.Count(flight => (flight.Timing?.SpawnSeconds ?? flight.TimeSeconds) <= 1));
        sim.Advance(10);
        Assert(sim.State.Aircraft.Count == flights.Count(flight => (flight.Timing?.SpawnSeconds ?? flight.TimeSeconds) <= 11));
        sim.Advance(270);
        Assert(sim.State.Aircraft.Count == 6 && flights.All(f => f.Spawned));
        foreach (var file in new[] { "CYN_ARR.SIM", "CYN_DEP.SIM" })
        {
            Assert(OriginalData.ReadSchedule(Path.Combine(CanyonFixtures(), file), airport).Count > 0);
        }
    }

    [Fact(DisplayName = "Intermediate hold short stops taxi before runway and TU releases")]
    public void Intermediate_hold_short_stops_taxi_before_runway_and_TU_releases()
    {
        var sim = New();
        var ac = sim.AddAircraft(FlightDirection.Departure, "DEP");
        ac.Phase = FlightPhase.TaxiOut;
        ac.Position = new(-200, 0);
        ac.Path = [new(200, 0)];
        ac.PathIndex = 0;
        Assert(sim.Execute("DEP/HS18").Success);
        Until(sim, () => ac.Holding, 120);
        Assert(ac.Position.X < -35);
        Assert(sim.Execute("DEP/TU").Success);
        sim.Advance(50);
        Assert(ac.Position.X > 35);
    }

    [Fact(DisplayName = "Taxi crosses a reserved runway without HS; crossing traffic does not reject landing clearance")]
    public void Taxi_crosses_reserved_runway_without_HS_and_actual_crossing_traffic_blocks_new_clearance()
    {
        var sim = New();
        var taxi = sim.AddAircraft(FlightDirection.Departure, "DEP");
        var arrival = sim.AddAircraft(FlightDirection.Arrival, "ARR");
        Assert(sim.Execute("ARR/CL").Success);
        taxi.Phase = FlightPhase.TaxiOut;
        taxi.Position = new(-200, 0);
        taxi.Path = [new(200, 0)];
        taxi.PathIndex = 0;
        sim.Advance(60);
        Assert(taxi.Position.X > 35);
        Assert(arrival.LandingCleared);
        taxi.Position = new(0, 0);
        taxi.Phase = FlightPhase.TaxiOut;
        arrival.Phase = FlightPhase.Approach;
        Assert(sim.Execute("ARR/CL").Success);
    }

    [Fact(DisplayName = "Structured site VOC reads every record, validates offsets and rejects truncation")]
    public void Structured_site_VOC_reads_every_record_validates_offsets_and_rejects_truncation()
    {
        byte[] data = [1, 5, 0, 0, 145, 0, 128, 129, 130, 1, 4, 0, 0, 145, 0, 131, 132, 0, 0, 0, 0, 9, 0, 0, 0, 17, 0, 0, 0, 3, 0, 1, 0];
        var bank = new VoiceBank();
        bank.LoadRecords(4, data);
        Assert(bank.Fragments.Count == 2);
        var wave = bank.BuildWave("(4:1) + (4:2)");
        Assert(wave.Skip(44).Take(5).SequenceEqual(new byte[] { 128, 129, 130, 131, 132 }));
        Throws(() => bank.LoadRecords(4, data[..^1]));
        var bad = data.ToArray();
        bad[21] = 8;
        Throws(() => bank.LoadRecords(4, bad));
        Assert(bank.Fragments.Count == 2);
        bank.Clear();
        Assert(bank.Fragments.Count == 0);
    }

    [Fact(DisplayName = "Zip importer rejects path traversal")]
    public void Zip_importer_rejects_path_traversal()
    {
        var zip = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open());
                writer.Write("bad");
            }
            Throws(() => OriginalData.Extract(zip, dir));
        }
        finally
        {
            File.Delete(zip);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact(DisplayName = "Original-format importer splits holds and creates gate nodes using synthetic data")]
    public void Original_format_importer_splits_holds_and_creates_gate_nodes_using_synthetic_data()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "CYN.TWR"), """
            [Points]
            A RwyEnd 0 0 36
            B RwyEnd 0 3000 18
            H1 Holdshort 100 0 T1 36
            H2 Holdshort 100 3000 T2 18
            X1 RwyInt 0 800 RW
            X2 RwyInt 0 2200 RW
            N1 Taxiway 200 0
            N2 Taxiway 200 3000
            E1 Taxiway 200 800
            E2 Taxiway 200 2200
            G Taxiway 500 800
            [Segments]
            RW Runway A B
            T1 Taxiway A N1
            T2 Taxiway B N2
            T3 Taxiway N1 E1
            T4 Taxiway E1 E2
            T5 Taxiway E2 N2
            EXIT1 Taxiway X1 E1
            EXIT2 Taxiway X2 E2
            RAMP1 Ramp E1 G
            [Routes]
            A N1 E1 E2 N2 end
            [Gates]
            G1 Left 1 1 1 RAMP1 ALLEY
            """);
            var airport = OriginalData.LoadCanyon(dir);
            Assert(airport.Runways.Count == 2 && airport.Nodes["G1"].Kind == "GATE");
            foreach (var runway in airport.Runways)
            {
                Assert(Routing.FindTaxiPath(airport, "G1", runway.Hold).Count > 1);
                Assert(Routing.FindTaxiPath(airport, runway.Exit, "G1").Count > 1);
            }
            File.WriteAllText(Path.Combine(dir, "sample.prf"), "Type: TEST Approach_Speed: 75\nTakeoff_Speed: 65 Wing_Span: 40\nMax_Taxi_Speed: 12 Turn_Rate: 2\n");
            OriginalData.ReadPerformance(Path.Combine(dir, "sample.prf"), airport);
            Near(airport.AircraftTypes["TEST"].WingSpanMetres, 12.192);
            File.WriteAllText(Path.Combine(dir, "sample.sim"), "[Aircraft]\nDEMO TEST VFR -1 d 01:15 0 0 0 0 18\n CYN OUT Ctr @GG1\n");
            var flight = OriginalData.ReadSchedule(Path.Combine(dir, "sample.sim"), airport).Single();
            Assert(flight.Gate == "G1" && flight.TimeSeconds == 75);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Runway leading-zero aliases resolve consistently")]
    public void Runway_leading_zero_aliases_resolve_consistently()
    {
        var airport = Airport.Training();
        airport.Runways[0].Name = "9";
        Assert(airport.GetRunway("09").Name == "9");
    }

    [Fact(DisplayName = "One-way taxi edges are directional and vehicle routes are excluded")]
    public void One_way_taxi_edges_are_directional_and_vehicle_routes_are_excluded()
    {
        var airport = Airport.Training();
        airport.Segments = [new("A", "TAXIWAY", "G1", "A2") { OneWay = true }, new("B", "VROUTE", "A2", "G1")];
        Assert(Routing.FindTaxiPath(airport, "G1", "A2").Count == 2);
        Throws(() => Routing.FindTaxiPath(airport, "A2", "G1"));
        airport.Routes["IN"] = ["E18", "A2"];
        airport.Routes["ONE"] = ["G1", "A2"];
        airport.OneWayRoutes.Add("ONE");
        Throws(() => Routing.NamedRoute(airport, "IN-ONE"));
    }

    [Fact(DisplayName = "Multiple-airport imports sort shared holds and choose the nearest departure hold")]
    public void Multiple_airport_imports_sort_shared_holds_and_choose_the_nearest_departure_hold()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            var text = """
            [Tower]
            Location 40d30m00.00sN/080d15m00.00sW
            [Points]
            A RwyEnd 0 0 36
            B RwyEnd 0 3000 18
            H2 Holdshort 160 0 T1 36
            H1 Holdshort 80 0 T1 36
            H3 Holdshort 100 3000 T2 18
            X1 RwyInt 0 800 RW
            X2 RwyInt 0 2200 RW
            N1 Taxiway 200 0
            N2 Taxiway 200 3000
            E1 Taxiway 200 800
            E2 Taxiway 200 2200
            G Taxiway 500 800
            [Segments]
            RW Runway A B
            T1 Taxiway A N1
            T2 Taxiway B N2
            T3 Taxiway N1 E1
            T4 Taxiway E1 E2
            T5 Taxiway E2 N2
            EXIT1 Taxiway X1 E1
            EXIT2 Taxiway X2 E2
            RAMP1 Ramp E1 G
            [Routes]
            OUT ONEWAY N1 E1 E2 N2 end
            [Gates]
            G1 Left 1 1 1 RAMP1 ALLEY
            """;
            foreach (var code in new[] { "CYN", "DCA", "ORD" })
            {
                File.WriteAllText(Path.Combine(directory, code + ".TWR"), text);
            }

            Assert(OriginalData.InstalledAirports(directory).Count == 3);
            foreach (var code in OriginalData.InstalledAirports(directory))
            {
                var airport = OriginalData.LoadAirport(directory, code);
                Assert(airport.Code == code && airport.GetRunway("36").Hold == "H1");
                Near(airport.Latitude, 40.5);
                Near(airport.Longitude, -80.25);
                Assert(airport.Segments.Any(e => e.From == "A" && e.To == "H1"));
                Assert(airport.Segments.Any(e => e.From == "H1" && e.To == "H2"));
                Assert(airport.Segments.Any(e => e.From == "H2" && e.To == "N1"));
                Assert(airport.OneWayRoutes.Contains("OUT"));
                Assert(!airport.Routes["OUT"].Contains("ONEWAY"));
                Assert(airport.Segments.Single(e => e.Id == "T4").OneWay);
            }
            File.WriteAllText(Path.Combine(directory, "wrong.sim"), "[SimInfo]\nSector DCA.SEC\n[Aircraft]\n");
            Throws(() => OriginalData.ReadSchedule(Path.Combine(directory, "wrong.sim"), OriginalData.LoadAirport(directory, "ORD")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact(DisplayName = "User-provided airport data integrates all runways, gates and schedules")]
    [Trait("Category", "Integration")]
    public void User_provided_airport_data_integrates_all_runways_gates_and_schedules()
    {
        var originalDirectory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(originalDirectory))
        {
            Xunit.Assert.Skip("Set TOWER_ORIGINAL_DATA to enable all-airport integration.");
            return;
        }

        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(originalDirectory, code);
            foreach (var runway in airport.Runways)
            {
                foreach (var gate in airport.Nodes.Values.Where(n => n.Kind == "GATE"))
                {
                    Assert(Routing.FindTaxiPath(airport, gate.Id, runway.Hold).Count > 1);
                    Assert(Routing.FindTaxiPath(airport, runway.Exit, gate.Id).Count > 1);
                }
            }

            foreach (var suffix in new[] { "ARR", "DEP", "MIX" })
            {
                var file = OriginalData.Find(originalDirectory, code + "_" + suffix + ".SIM") ?? throw new Exception("Missing scenario");
                Assert(OriginalData.ReadSchedule(file, airport).Count > 0);
            }
        }
    }

    [Fact(DisplayName = "Direct phrase concatenation preserves PCM order and format")]
    public void Direct_phrase_concatenation_preserves_PCM_order_and_format()
    {
        var bank = new VoiceBank();
        bank.Load(2, [128, 145, 0, 128, 128, 128, 128, 145, 0, 128, 128, 128, 128, 145, 0]);
        Assert(bank.BuildWave("(2:2)(2:3)").SequenceEqual(bank.BuildWave(" (2:2) + (2:3) ")));
        var ids = VoiceBank.ParseExpression(Speak.Tower + Speak.WithYou);
        Assert(ids.Select(x => x.Index).SequenceEqual(new[] { 98, 53, 151, 56, 44 }));
        var wave = bank.BuildWave("(2:2)");
        Assert(BitConverter.ToInt32(wave, 28) == 8900 && BitConverter.ToInt16(wave, 22) == 1);
    }

    [Fact(DisplayName = "Phrase parser rejects broken separators and oversized IDs")]
    public void Phrase_parser_rejects_broken_separators_and_oversized_IDs()
    {
        foreach (var bad in new[] { "", " ", "+ (2:2)", "(2:2) + ", "(2:2)++(2:3)", "(0:1)", "(2:0)", "(2:99999999999999999)", "(2:2) junk", "(2:2), (2:3)" })
        {
            Throws(() => VoiceBank.ParseExpression(bad));
        }
    }

    [Fact(DisplayName = "Text catalog composes known phrases without silently dropping unknown words")]
    public void Text_catalog_composes_known_phrases_without_silently_dropping_unknown_words()
    {
        var catalog = new VoicePhraseCatalog();
        Assert(catalog.TryCompose("Tower, WITH YOU!", "CYN", out var expression));
        Assert(expression == Speak.Tower + Speak.WithYou);
        Assert(!catalog.TryCompose("Tower with you 12 miles west", "CYN", out expression) && expression == "");
        // Artificial IDs exercise composition; these are not claims about original recordings.
        catalog.Set("12", "(2:2)");
        catalog.Set("miles west", "(2:3)");
        Assert(catalog.TryCompose("Tower with you 12 miles west", "CYN", out expression));
        Assert(expression == Speak.Tower + Speak.WithYou + "(2:2)(2:3)");
        Assert(!catalog.TryCompose("Tower with you 12.5 miles west", "CYN", out _));
        Assert(!catalog.TryCompose("Tower ÃŽÂ©", "CYN", out _), "Unknown Unicode words must not disappear.");
    }

    [Fact(DisplayName = "Phrase matching finds complete alternatives instead of greedy partial matches")]
    public void Phrase_matching_finds_complete_alternatives_instead_of_greedy_partial_matches()
    {
        var catalog = new VoicePhraseCatalog(false);
        catalog.Set("a b", "(2:2)");
        catalog.Set("a", "(2:3)");
        catalog.Set("b c", "(2:4)");
        Assert(catalog.TryCompose("a b c", "CYN", out var expression) && expression == "(2:3)(2:4)");
    }

    [Fact(DisplayName = "Airport phrase scopes override globals without leaking across sites")]
    public void Airport_phrase_scopes_override_globals_without_leaking_across_sites()
    {
        var catalog = new VoicePhraseCatalog();
        Throws(() => catalog.Set("runway", "(4:1)"));
        catalog.Set("runway", "(2:2)");
        catalog.Set("runway", "(4:1)", "CYN");
        catalog.Set("taxiway", "(5:1)", "ORD");
        Assert(catalog.TryCompose("runway", "CYN", out var c) && c == "(4:1)");
        Assert(catalog.TryCompose("runway", "DCA", out var d) && d == "(2:2)");
        Assert(!catalog.TryCompose("taxiway", "CYN", out _));
        Throws(() => catalog.Set("test", "(6:1)", "CYN"));
    }

    [Fact(DisplayName = "Phrase settings round trip mappings and disabled fallback")]
    public void Phrase_settings_round_trip_mappings_and_disabled_fallback()
    {
        var settings = new VoiceSettings { ReadbacksEnabled = false, WindowsFallback = false, Phrases = [new("west", "(2:2)"), new("nine", "(4:1)", "CYN")] };
        var json = System.Text.Json.JsonSerializer.Serialize(settings);
        var restored = System.Text.Json.JsonSerializer.Deserialize<VoiceSettings>(json)!;
        Assert(!restored.ReadbacksEnabled && !restored.WindowsFallback && restored.Phrases.SequenceEqual(settings.Phrases));
        var catalog = new VoicePhraseCatalog(false);
        foreach (var p in restored.Phrases)
        {
            catalog.Set(p.Text, p.Expression, p.AirportCode);
        }

        Assert(catalog.TryCompose("nine west", "CYN", out var expression) && expression == "(4:1)(2:2)");
        catalog.Remove(catalog.Entries.Single(p => p.Text == "west"));
        Assert(!catalog.TryCompose("nine west", "CYN", out _));
    }

    [Fact(DisplayName = "Radio events include callsign, replies, rejections and say again exactly once")]
    public void Radio_events_include_callsign_replies_rejections_and_say_again_exactly_once()
    {
        var sim = New();
        var radio = new List<(string Id, string Text)>();
        sim.RadioTransmission += (id, text) => radio.Add((id, text));
        var aircraft = sim.AddAircraft(FlightDirection.Arrival, "TEST1");
        Assert(radio.Count == 1 && radio[0].Id == "TEST1");
        var result = sim.Execute("TEST1/CL");
        Assert(result.Success && radio.Count == 2 && radio[^1].Text == result.Message);
        var previous = radio[^1];
        sim.Execute("TEST1/SG");
        Assert(radio.Count == 3 && radio[^1] == previous);
        result = sim.Execute("TEST1/TU");
        Assert(!result.Success && radio.Count == 4 && radio[^1].Text == "Unable.");
        sim.Execute("UNKNOWN/CL");
        Assert(radio.Count == 4);
        Assert(aircraft.LastTransmission == radio[^1].Text);
    }

    [Fact(DisplayName = "Automatic flight state reports publish radio events")]
    public void Automatic_flight_state_reports_publish_radio_events()
    {
        var sim = New();
        var messages = new List<string>();
        var log = new List<string>();
        sim.Message += log.Add;
        sim.RadioTransmission += (_, text) => messages.Add(text);
        var aircraft = sim.AddAircraft(FlightDirection.Arrival, "TEST1");
        sim.Execute("TEST1/CL");
        Until(sim, () => aircraft.Phase == FlightPhase.ArrivalHold);
        Assert(log.Any(t => t.Contains("landed;")) && messages.Any(t => t.Contains("up your frequency request taxi to parking")));
        Assert(!messages.Any(t => t.Contains("landed;")));
    }

    [Fact]
    public void Typed_radio_preserves_intent_on_repeat_and_requests_parking_only_after_vacating()
    {
        var sim = New();
        var messages = new List<AircraftRadioMessage>();
        sim.RadioMessage += messages.Add;
        var aircraft = sim.AddAircraft(FlightDirection.Arrival, "TEST1");
        Assert(messages.Count == 1 && messages[0].IntentKey == "arrival.initial" && messages[0].DistanceNm > 0);
        sim.Execute("TEST1/CL");
        Assert(messages[^1].IntentKey == "command.CL.pilot");
        Assert(!messages.Any(m => m.IntentKey == "ground.parking.request"));
        var previous = messages[^1];
        sim.Execute("TEST1/SG");
        Assert(messages[^1] == previous);
        var json = System.Text.Json.JsonSerializer.Serialize(sim.State, JsonStore.Options);
        var restored = System.Text.Json.JsonSerializer.Deserialize<Session>(json, JsonStore.Options)!;
        Assert(restored.Aircraft.Single().LastRadioMessage == previous);
        Until(sim, () => aircraft.Phase == FlightPhase.ArrivalHold);
        Assert(messages.Count(m => m.IntentKey == "ground.parking.request") == 1);
    }
}
