using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class OriginalTimingTests
{
    private static Session Session(string time = "05:00", bool departure = false, double? cruise = 120, int altitude = 3000)
    {
        var airport = Airport.Training();
        airport.Code = "FIELD";
        airport.Latitude = 51;
        airport.Longitude = -96;
        airport.Original.FieldElevationFeetMsl = 500;
        airport.Original.Fixes["ORIGIN"] = new("ORIGIN", 51.8, -96);
        airport.AircraftTypes["PLANE"] = new() { Type = "PLANE" };
        if (cruise is { } speed)
        {
            airport.AircraftTypes["PLANE"].OriginalPerformance["Cruise_Speed"] = speed;
        }
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".SIM");
        try
        {
            File.WriteAllText(path, $"[SimInfo]\nSector FIELD.SEC\nAircraft 1\n[Aircraft]\nTEST PLANE VFR -1 {(departure ? "d" : "a")} {time} 200 {altitude} 6000 1200 18\n"
                + (departure ? "FIELD ORIGIN Ctr @TO @LP @GG1\n" : "ORIGIN FIELD Twr @FS\n"));
            var session = OriginalData.ReadScenario(path, airport);
            session.Options.WindKnots = 0; // Exact scheduled-entry geometry is a calm-air baseline.
            return session;
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(120, 879, 5996)]
    [InlineData(250, 1831, 2878)]
    [InlineData(500, 3661, 1439)]
    public void Original_cruise_rounding_and_spawn_lead_are_reproduced(double cruise, int units, int seconds)
    {
        Assert.Equal(units, OriginalTimingUnits.CruiseUnits(cruise));
        Assert.Equal(seconds, OriginalTimingUnits.LeadSeconds(units));
        var timing = Session("166:40", cruise: cruise).Schedule[0].Timing!;
        Assert.Equal(10000, timing.EntrySeconds);
        Assert.Equal(10000 - seconds, timing.SpawnSeconds);
        Assert.Equal(seconds, timing.LeadSeconds);
    }

    [Fact]
    public void Arrival_is_prepared_silently_and_reports_once_after_entry_at_actual_position()
    {
        var state = Session();
        var simulation = new Simulation(state);
        var messages = new List<AircraftRadioMessage>();
        var positions = new List<Position>();
        int added = 0;
        simulation.AircraftAdded += _ => added++;
        simulation.RadioMessage += message =>
        {
            messages.Add(message);
            positions.Add(state.Aircraft[0].Position);
        };
        simulation.Advance(0.1);
        var aircraft = Assert.Single(state.Aircraft);
        var start = aircraft.Position;
        Assert.True(state.Schedule[0].Spawned);
        Assert.False(aircraft.IsOnFrequency);
        Assert.Empty(messages);
        Assert.Equal(0, added);
        Assert.Equal(2500, aircraft.AltitudeFeet);
        Assert.False(simulation.Execute("TEST/SA").Success);
        simulation.Advance(10);
        Assert.NotEqual(start, aircraft.Position);
        Assert.Equal(2500, aircraft.AltitudeFeet);
        Assert.Empty(messages);
        simulation.Advance(290);
        Assert.True(aircraft.InitialContact!.EntryReached);
        Assert.False(aircraft.IsOnFrequency);
        Assert.Empty(messages);
        simulation.Advance(1.2);
        Assert.True(aircraft.IsOnFrequency);
        Assert.Equal(1, added);
        var message = Assert.Single(messages);
        Assert.Equal("arrival.initial", message.IntentKey);
        Assert.Equal(positions[0].Distance(state.Airport.Observer) / 1852, message.DistanceNm!.Value, 8);
        Assert.Contains(OriginalRadioDistance.SpokenMiles(message.DistanceNm.Value).ToString(), message.Text);
        Assert.InRange(aircraft.InitialContact.AnnouncedSeconds!.Value, 301, 301.1);
        Assert.True(simulation.Execute("TEST/SA").Success);
        simulation.Advance(1);
        Assert.Single(messages, value => value.IntentKey == "arrival.initial");
    }

    [Fact]
    public void Entry_at_zero_starts_at_the_contact_point_without_immediate_radio()
    {
        var state = Session("00:00");
        var simulation = new Simulation(state);
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(0.05);
        var aircraft = Assert.Single(state.Aircraft);
        Assert.Equal(state.Schedule[0].Timing!.ArrivalContactPoint, aircraft.Position);
        Assert.True(aircraft.InitialContact!.EntryReached);
        Assert.False(aircraft.IsOnFrequency);
        Assert.Empty(messages);
        simulation.Advance(1.1);
        Assert.True(aircraft.IsOnFrequency);
        Assert.Single(messages);
    }

    [Fact]
    public void Route_is_extended_backwards_when_entry_time_exceeds_the_named_route_length()
    {
        var state = Session("60:00");
        var simulation = new Simulation(state);
        simulation.Advance(0.05);
        var aircraft = Assert.Single(state.Aircraft);
        var origin = state.Schedule[0].OriginalPlan!.ResolvedRoute![0].Position;
        Assert.True(aircraft.Position.Y > origin.Y);
        Assert.Equal("PREENTRY", aircraft.OriginalRoute!.Points[0].Name);
        Assert.Equal(2500, aircraft.AltitudeFeet);
    }

    [Fact]
    public void Departure_exists_at_gate_before_planned_time_without_early_reminders()
    {
        var state = Session(departure: true);
        var simulation = new Simulation(state);
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(200);
        var aircraft = Assert.Single(state.Aircraft);
        Assert.Equal(FlightPhase.AtGate, aircraft.Phase);
        Assert.Equal(state.Airport.Point("G1"), aircraft.Position);
        Assert.False(aircraft.IsOnFrequency);
        Assert.Empty(messages);
        Assert.False(simulation.Execute("TEST/T").Success);
        simulation.Advance(100.5);
        Assert.Empty(messages);
        simulation.Advance(0.7);
        Assert.True(aircraft.IsOnFrequency);
        Assert.Equal("departure.ready", Assert.Single(messages).IntentKey);
    }

    [Fact]
    public void Delayed_gate_creation_reports_after_actual_entry_not_retroactively()
    {
        var state = Session("00:01", departure: true);
        var simulation = new Simulation(state);
        var blocker = simulation.AddAircraft(FlightDirection.Departure, "BLOCK", assignedGate: "G1");
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(5);
        Assert.False(state.Schedule[0].Spawned);
        Assert.Empty(messages);
        blocker.Phase = FlightPhase.Completed;
        simulation.Advance(0.1);
        var aircraft = state.Aircraft.Single(value => value.CallSign == "TEST");
        Assert.False(aircraft.IsOnFrequency);
        Assert.Empty(messages);
        simulation.Advance(1.1);
        Assert.True(aircraft.IsOnFrequency);
        Assert.Equal("TEST", Assert.Single(messages).Callsign);
    }

    [Fact]
    public void Pause_and_save_resume_preserve_pending_entry_and_do_not_repeat_first_contact()
    {
        var state = Session();
        var simulation = new Simulation(state);
        simulation.Advance(20);
        var position = state.Aircraft[0].Position;
        simulation.Paused = true;
        simulation.Advance(100);
        Assert.Equal(position, state.Aircraft[0].Position);
        Assert.Equal(20, state.ElapsedSeconds, 6);
        simulation.Paused = false;
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, state);
            var resumed = new Simulation(JsonStore.LoadSession(path));
            var messages = new List<AircraftRadioMessage>();
            resumed.RadioMessage += messages.Add;
            simulation.Advance(282);
            resumed.Advance(282);
            Assert.Equal(state.Aircraft[0].Position, resumed.State.Aircraft[0].Position);
            Assert.Equal(state.Aircraft[0].InitialContact!.AnnouncedSeconds, resumed.State.Aircraft[0].InitialContact!.AnnouncedSeconds);
            Assert.Single(messages);
            JsonStore.Write(path, resumed.State);
            resumed = new Simulation(JsonStore.LoadSession(path));
            resumed.RadioMessage += messages.Add;
            resumed.Advance(2);
            Assert.Single(messages);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Old_save_without_timing_keeps_spawn_and_immediate_contact_at_its_previous_time()
    {
        var state = Session("00:02");
        state.Schedule[0].Timing = null;
        var simulation = new Simulation(state);
        var messages = new List<AircraftRadioMessage>();
        simulation.RadioMessage += messages.Add;
        simulation.Advance(1);
        Assert.Empty(state.Aircraft);
        simulation.Advance(1.1);
        Assert.True(Assert.Single(state.Aircraft).IsOnFrequency);
        Assert.Null(state.Aircraft[0].InitialContact);
        Assert.Single(messages);
    }

    [Fact]
    public void Missing_cruise_metadata_keeps_legacy_arrival_timing()
    {
        Assert.Null(Session(cruise: null).Schedule[0].Timing);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    public void Wind_adjusts_prepared_distance_without_stationary_waiting_at_the_scheduled_entry(double from)
    {
        var state = Session();
        state.Options.WindFromDegrees = from;
        state.Options.WindKnots = 20;
        var simulation = new Simulation(state);
        simulation.Advance(299);
        var aircraft = Assert.Single(state.Aircraft);
        Assert.False(aircraft.InitialContact!.EntryReached);
        Assert.True(aircraft.GroundSpeedKnots > 1);
        simulation.Advance(1.1);
        Assert.True(aircraft.InitialContact.EntryReached);
        Assert.InRange(aircraft.InitialContact.RadioDueSeconds!.Value, 301, 301.1);
    }

    [Theory]
    [InlineData(10000, 1831)]
    [InlineData(10001, 3661)]
    public void Initial_cruise_cap_changes_above_ten_thousand_feet_msl(int altitude, int units)
    {
        Assert.Equal(OriginalTimingUnits.SpeedKnots(units), Session(cruise: 500, altitude: altitude).Schedule[0].Timing!.InitialSpeedKnots);
    }

    [Fact]
    public void Passing_many_short_route_legs_does_not_delay_scheduled_entry()
    {
        var state = Session();
        var contact = state.Schedule[0].Timing!.ArrivalContactPoint!.Value;
        state.Schedule[0].OriginalPlan!.ResolvedRoute = [new("ORIGIN", contact.Offset(0, 30000)),
            .. Enumerable.Range(1, 80).Select(index => new OriginalRoutePoint("FIX" + index, contact.Offset(0, 5000 - index * 50)))];
        var simulation = new Simulation(state);
        simulation.Advance(300.1);
        var aircraft = Assert.Single(state.Aircraft);
        Assert.True(aircraft.InitialContact!.EntryReached);
        Assert.InRange(aircraft.InitialContact.RadioDueSeconds!.Value, 301, 301.1);
    }

    [Fact]
    public void Hidden_precontact_aircraft_do_not_create_uncontrollable_conflict_penalties()
    {
        var state = Session();
        var first = state.Schedule[0];
        state.Schedule.Add(new()
        {
            CallSign = "OTHER", Type = first.Type, Direction = first.Direction, Runway = first.Runway,
            TimeSeconds = first.TimeSeconds, OriginalPlan = first.OriginalPlan, Timing = first.Timing
        });
        var simulation = new Simulation(state);
        simulation.Advance(10);
        Assert.Equal(2, state.Aircraft.Count);
        Assert.All(state.Aircraft, aircraft => Assert.False(aircraft.IsOnFrequency));
        Assert.Equal(0, state.Performance.Incidents);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_saved_schedule_or_pending_contact_is_rejected(bool actor)
    {
        var state = Session();
        var simulation = new Simulation(state);
        simulation.Advance(1);
        if (actor)
        {
            state.Aircraft[0].InitialContact!.EntryPointIndex = 999;
        }
        else
        {
            state.Schedule[0].Timing!.SpawnSeconds = 500;
        }
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, state);
            Assert.Throws<InvalidDataException>(() => JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("CYN")]
    [Trait("Category", "Integration")]
    public void All_supplied_schedules_have_explicit_original_timing(string code)
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original schedule timing.");
            return;
        }
        var airport = OriginalData.LoadAirport(directory, code);
        foreach (var suffix in new[] { "ARR", "DEP", "MIX" })
        {
            var state = OriginalData.ReadScenario(OriginalData.Find(directory, code + "_" + suffix + ".SIM")!, airport);
            Assert.All(state.Schedule, flight =>
            {
                Assert.NotNull(flight.Timing);
                Assert.True(flight.Timing!.IsValid(flight.Direction));
                Assert.Equal(flight.TimeSeconds, flight.Timing.EntrySeconds);
            });
        }
    }
}
