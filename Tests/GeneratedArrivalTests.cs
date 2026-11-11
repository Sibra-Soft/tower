using Tower.Core;
using Tower.Infrastructure;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class GeneratedArrivalTests
{
    private static string Data => Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA")
        ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");

    private static Simulation Create(bool enabled = true)
    {
        var airport = Airport.Training();
        airport.Original.UseOriginalGeneratedPerformance = true;
        airport.Original.UseGeneratedArrivalRoutes = enabled;
        airport.Original.FieldElevationFeetMsl = 500;
        airport.Original.PatternCategoryByType["TEST"] = 1;
        airport.Original.PatternValues["cat1_altit"] = 1000;
        airport.Original.PatternValues["cat1_final"] = 3000;
        airport.Original.PatternMetresPerUnit = 1;
        airport.Original.Fixes.Add("ENTRY", new("ENTRY", airport.Latitude + 1, airport.Longitude + 0.1));
        airport.Original.ArrivalFlow = [new("ENTRY", 100)];
        airport.AircraftTypes["TEST"] = new()
        {
            Type = "TEST", ApproachSpeed = 100, PatternSpeed = 140,
            OriginalPerformance = new() { ["Cruise_Speed"] = 200 },
            DescentRatesFpm = Enum.GetValues<AltitudeBand>().ToDictionary(b => b, _ => 1200d),
            Acceleration = new() { AirborneDeceleration = 7, AirborneAcceleration = 7 }
        };
        return new(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0 } });
    }

    private static void Until(Simulation simulation, Func<bool> done, int seconds = 1800, double step = 1)
    {
        for (var elapsed = 0d; elapsed < seconds && !done(); elapsed += step)
        {
            simulation.Advance(step);
        }
        Assert.True(done(), "Generated arrival did not reach the expected stage.");
    }

    [Theory]
    [InlineData("CYN", "9", "G1", "C130")]
    [Trait("Category", "Integration")]
    public void Original_airport_arrival_runs_from_entry_through_final_to_parking(string code, string runway, string gate, string type)
    {
        var airport = OriginalData.LoadAirport(Data, code);
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { Runway = runway, AircraftPerMinute = 0 } });
        var radio = new List<AircraftRadioMessage>();
        var added = 0;
        simulation.RadioMessage += radio.Add;
        simulation.AircraftAdded += _ => added++;
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "AAL1535", type, runway, gate);
        var route = Assert.IsType<GeneratedArrivalProgress>(aircraft.GeneratedArrival);
        Assert.Null(aircraft.OriginalPlan);
        Assert.Equal(aircraft.GeneratedPerformance!.Altitude1FeetMsl - airport.Original.FieldElevationFeetMsl!.Value, aircraft.AltitudeFeet, 6);
        Assert.Equal(aircraft.GeneratedPerformance.InitialSpeedKnots, aircraft.SpeedKnots);
        Assert.Equal(airport.Original.Fixes[aircraft.GeneratedRoute!.Origin].ProjectFrom(airport.Latitude, airport.Longitude, airport.Observer), route.Origin.Position);
        Assert.False(aircraft.IsOnFrequency);
        Assert.Empty(radio);
        Until(simulation, () => aircraft.IsOnFrequency);
        Assert.Equal(1, added);
        var first = Assert.Single(radio, m => m.IntentKey == "arrival.initial");
        Assert.NotNull(first.DistanceNm);
        Assert.True(route.Contact.AnnouncedSeconds >= route.Contact.EntrySeconds + 1 - 1e-7);
        var speechRuntime = new WorkbenchSpeechRuntime(Data);
        speechRuntime.Preferences.Engine = SpeechEngineKind.BaoOriginal;
        var speech = speechRuntime.Render(first);
        Assert.Null(speech.Diagnostic);
        Assert.True(speech.Wave is { Length: > 44 });
        Assert.True(simulation.Execute("AAL1535/CL").Success);
        Until(simulation, () => route.Stage == GeneratedArrivalStage.Final, step: 0.05);
        Assert.True(aircraft.LandingCleared);
        Assert.InRange(Math.Abs(aircraft.AltitudeFeet - route.PatternAltitudeFeetAgl), 0, 1);
        Assert.InRange(aircraft.SpeedKnots, route.ApproachSpeedKnots - 1, route.ApproachSpeedKnots + 1);
        Until(simulation, () => aircraft.Phase is FlightPhase.ArrivalHold or FlightPhase.GoAround);
        Assert.Equal(FlightPhase.ArrivalHold, aircraft.Phase);
        Assert.True(simulation.Execute("AAL1535/TP").Success);
        Until(simulation, () => aircraft.Phase == FlightPhase.Completed);
        Assert.Equal(1, simulation.State.Performance.Arrivals);
        Assert.Equal(1, added);
        Assert.Single(radio, m => m.IntentKey == "arrival.initial");
    }

    [Fact]
    public void Geographic_contact_is_delayed_once_and_not_granted_by_radar_visibility()
    {
        var simulation = Create();
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "DEP", "TEST");
        Assert.True(aircraft.GeneratedArrival!.Contact.EntryReached);
        Assert.False(simulation.Execute("DEP/CL").Success);
        simulation.Advance(0.5);
        Assert.False(aircraft.IsOnFrequency);
        simulation.Advance(0.5);
        Assert.True(aircraft.IsOnFrequency);
        Assert.Equal("arrival.initial", Assert.Single(messages).IntentKey);
        simulation.Advance(5);
        Assert.Single(messages, m => m.IntentKey == "arrival.initial");
    }

    [Fact]
    public void Prf_descent_and_deceleration_are_gradual_and_prepare_a_slow_profile()
    {
        var simulation = Create();
        var specification = simulation.State.Airport.AircraftTypes["TEST"];
        specification.DescentRatesFpm = Enum.GetValues<AltitudeBand>().ToDictionary(b => b, _ => 300d);
        specification.Acceleration.AirborneDeceleration = 1;
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "SLOW", "TEST");
        var height = aircraft.AltitudeFeet;
        var speed = aircraft.SpeedKnots;
        var position = aircraft.Position;
        simulation.Advance(1);
        Assert.InRange(height - aircraft.AltitudeFeet, 0, 5.001);
        Assert.InRange(speed - aircraft.SpeedKnots, 0, SpeedPerformance.KnotsPerSecond(1) + 0.001);
        Assert.InRange(position.Distance(aircraft.Position), 0, speed * Simulation.MetresPerKnotSecond + 0.01);
        Until(simulation, () => aircraft.IsOnFrequency);
        Assert.True(simulation.Execute("SLOW/CL").Success);
        Until(simulation, () => aircraft.GeneratedArrival!.Stage == GeneratedArrivalStage.Final);
        Assert.InRange(Math.Abs(aircraft.AltitudeFeet - aircraft.GeneratedArrival!.PatternAltitudeFeetAgl), 0, 0.1);
    }

    [Fact]
    public void Speed_override_survives_final_join_and_heading_override_cancels_route()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST", "TEST");
        Until(simulation, () => aircraft.IsOnFrequency);
        Assert.True(simulation.Execute("TEST/speed 90/CL").Success);
        Until(simulation, () => aircraft.GeneratedArrival!.Stage == GeneratedArrivalStage.Final);
        Assert.Equal(90, aircraft.TargetSpeedKnots);
        Assert.True(aircraft.LandingCleared);
        Assert.True(simulation.Execute("TEST/URH090").Success);
        Assert.Equal(GeneratedArrivalStage.Cancelled, aircraft.GeneratedArrival!.Stage);
        Assert.False(aircraft.LandingCleared);
        simulation.Advance(5);
        Assert.Equal(GeneratedArrivalStage.Cancelled, aircraft.GeneratedArrival!.Stage);
    }

    [Fact]
    public void Orbit_preserves_route_and_missing_clearance_causes_a_real_missed_approach()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "TEST", "TEST");
        Until(simulation, () => aircraft.IsOnFrequency);
        Assert.True(simulation.Execute("TEST/ML3").Success);
        Assert.Equal(GeneratedArrivalStage.Inbound, aircraft.GeneratedArrival!.Stage);
        Until(simulation, () => aircraft.Phase == FlightPhase.Circuit, 150);
        Until(simulation, () => aircraft.Phase == FlightPhase.GoAround);
        Assert.Equal(GeneratedArrivalStage.Cancelled, aircraft.GeneratedArrival.Stage);
        Assert.Equal("flight.goaround", aircraft.LastRadioMessage!.IntentKey);
        Assert.Equal(0, simulation.State.Performance.Arrivals);
    }

    [Fact]
    public void Entry_spacing_and_saved_pending_radio_are_preserved()
    {
        var simulation = Create();
        var first = simulation.AddAircraft(FlightDirection.Arrival, "FIRST", "TEST");
        var second = simulation.AddAircraft(FlightDirection.Arrival, "SECOND", "TEST");
        Assert.True(first.Position.Distance(second.Position) >= 3 * 1852);
        Assert.False(second.GeneratedArrival!.Contact.EntryReached);
        var path = Path.Combine(Path.GetTempPath(), "Arrival-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var state = JsonStore.LoadSession(path);
            state.Airport.Original.Fixes["ENTRY"] = new("ENTRY", 0, 0);
            var resumed = new Simulation(state);
            var added = 0;
            resumed.AircraftAdded += _ => added++;
            resumed.Paused = true;
            resumed.Advance(10);
            Assert.Equal(0, added);
            resumed.Paused = false;
            simulation.Advance(5);
            resumed.Advance(5);
            for (var i = 0; i < 2; i++)
            {
                Assert.Equal(simulation.State.Aircraft[i].Position, state.Aircraft[i].Position);
                Assert.Equal(simulation.State.Aircraft[i].SpeedKnots, state.Aircraft[i].SpeedKnots);
                Assert.Equal(simulation.State.Aircraft[i].AltitudeFeet, state.Aircraft[i].AltitudeFeet);
            }
            Assert.Equal(1, added);
            Assert.Equal(second.GeneratedArrival.Origin, state.Aircraft[1].GeneratedArrival!.Origin);
            state.Aircraft[1].GeneratedArrival!.ContactPointIndex = 99;
            JsonStore.Write(path, state);
            Assert.Throws<InvalidDataException>(() => JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Legacy_and_authored_arrivals_keep_their_previous_model()
    {
        var legacy = Create(false).AddAircraft(FlightDirection.Arrival, "OLD", "TEST");
        Assert.Null(legacy.GeneratedArrival);
        Assert.True(legacy.IsOnFrequency);
        var plan = new OriginalFlightPlan { AltitudeReference = OriginalAltitudeReference.Msl, Altitude1Feet = 4000 };
        var authored = Create().AddAircraft(FlightDirection.Arrival, "SIM", "TEST", originalPlan: plan);
        Assert.Null(authored.GeneratedArrival);
        Assert.Same(plan, authored.OriginalPlan);
    }

    [Fact]
    public void Unusable_final_geometry_keeps_the_existing_entry_model()
    {
        var simulation = Create();
        simulation.State.Airport.Original.PatternValues["cat1_final"] = 100000;
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "FALLBACK", "TEST",
            freeTrafficFlight: new() { Task = FreeTrafficTask.Arrival });
        Assert.Null(aircraft.GeneratedArrival);
        Assert.True(aircraft.IsOnFrequency);
        Assert.NotNull(aircraft.GeneratedPerformance);
    }
}
