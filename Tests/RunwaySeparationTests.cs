using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class RunwaySeparationTests
{
    private static Simulation Create()
    {
        var airport = Airport.Training();
        airport.Runways.Add(new() { Name = "36", Threshold = "R36", End = "R18" });
        return new(new() { Airport = airport, Options = new() { AircraftPerMinute = 0 }, ElapsedSeconds = 200 });
    }

    private static Aircraft Rolling(Simulation simulation, int category = 1)
    {
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "FOLLOW");
        aircraft.Specification = new() { WeightCategory = category };
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.Phase = FlightPhase.LinedUp;
        Assert.True(simulation.Execute("FOLLOW/CT").Success);
        return aircraft;
    }

    private static RunwayDeparture Previous(Simulation simulation, Aircraft aircraft, int category, double feet, string runway = "18", string? group = null, double age = 200)
    {
        var previous = new RunwayDeparture
        {
            CallSign = "PREVIOUS",
            Runway = runway,
            WeightCategory = category,
            OriginalTypeGroup = group,
            StartedSeconds = simulation.State.ElapsedSeconds - age,
            Airborne = true,
            Position = aircraft.Position.Offset(180, feet * Coordinates.MetresPerFoot)
        };
        simulation.State.RunwayDepartures.Add(previous);
        return previous;
    }

    [Theory]
    [InlineData(1, 1, 3000)]
    [InlineData(1, 2, 4500)]
    [InlineData(1, 3, 6000)]
    [InlineData(2, 1, 3000)]
    [InlineData(2, 2, 4500)]
    [InlineData(2, 3, 6000)]
    [InlineData(3, 1, 6000)]
    [InlineData(3, 2, 6000)]
    [InlineData(3, 3, 6000)]
    public void Actual_takeoff_checks_category_matrix_once(int leader, int follower, int feet)
    {
        foreach (var shortfall in new[] { 1, 0 })
        {
            var simulation = Create();
            var aircraft = Rolling(simulation, follower);
            Previous(simulation, aircraft, leader, feet - shortfall);
            var messages = new List<string>();
            simulation.Message += messages.Add;
            simulation.Advance(0.1);
            Assert.Equal(shortfall, simulation.State.Performance.Incidents);
            Assert.Equal(shortfall, messages.Count(message => message.Contains("Category separation alert")));
            Assert.Equal(aircraft.RunwayDepartureId, Assert.Single(simulation.State.RunwayDepartures).Id);
        }
    }

    [Theory]
    [InlineData("Heavy", 119, 1)]
    [InlineData("Heavy", 120, 0)]
    [InlineData("Airline", 119, 0)]
    [InlineData(null, 119, 0)]
    public void Large_is_not_automatically_heavy(string? group, double age, int incidents)
    {
        var simulation = Create();
        var aircraft = Rolling(simulation);
        Previous(simulation, aircraft, 3, 7000, group: group, age: age);
        simulation.Advance(0.05);
        Assert.Equal(incidents, simulation.State.Performance.Incidents);
    }

    [Theory]
    [InlineData(1, 3, null, 179, 1)]
    [InlineData(1, 3, null, 180, 0)]
    [InlineData(2, 3, null, 179, 0)]
    [InlineData(3, 3, "Heavy", 179, 1)]
    [InlineData(2, 1, null, 179, 0)]
    public void Opposite_takeoff_has_specific_category_and_time_rules(int follower, int leader, string? group, double age, int incidents)
    {
        var simulation = Create();
        var aircraft = Rolling(simulation, follower);
        Previous(simulation, aircraft, leader, 7000, runway: "36", group: group, age: age);
        simulation.Advance(0.05);
        Assert.Equal(incidents, simulation.State.Performance.Incidents);
    }

    [Theory]
    [InlineData("18", 2999, 200, 1)]
    [InlineData("18", 3000, 200, 0)]
    [InlineData("36", 7000, 179, 1)]
    [InlineData("36", 7000, 180, 0)]
    public void Touchdown_checks_distance_or_opposite_direction_timer(string runway, double feet, double age, int incidents)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "FOLLOW");
        aircraft.Specification = new() { WeightCategory = 1 };
        aircraft.Position = simulation.State.Airport.Point("R18");
        // Separation is evaluated at touchdown; flare geometry is tested separately.
        aircraft.TouchdownDistanceMetres = 0;
        aircraft.AltitudeFeet = 0;
        aircraft.Path = [aircraft.Position];
        aircraft.PathIndex = 0;
        Assert.True(simulation.Execute("FOLLOW/CL").Success);
        Previous(simulation, aircraft, 1, feet, runway, age: age);
        simulation.Advance(0.05);
        Assert.Equal(FlightPhase.LandingRoll, aircraft.Phase);
        Assert.Equal(incidents, simulation.State.Performance.Incidents);
    }

    [Fact]
    public void Clearance_and_threshold_access_do_not_start_the_timer_and_pause_does_not_advance_it()
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "FOLLOW");
        aircraft.Phase = FlightPhase.HoldingShort;
        aircraft.Position = simulation.State.Airport.Point("H18");
        Assert.True(simulation.Execute("FOLLOW/CT").Success);
        simulation.Advance(1);
        Assert.Empty(simulation.State.RunwayDepartures);
        for (var second = 0; second < 100 && simulation.State.RunwayDepartures.Count == 0; second++)
        {
            simulation.Advance(1);
        }
        var departure = Assert.Single(simulation.State.RunwayDepartures);
        Assert.True(departure.StartedSeconds > 201);
        var elapsed = simulation.State.ElapsedSeconds;
        simulation.Paused = true;
        simulation.Advance(30);
        Assert.Equal(elapsed, simulation.State.ElapsedSeconds);
    }

    [Fact]
    public void Saved_history_survives_removing_the_previous_aircraft_and_checks_the_next_departure()
    {
        var simulation = Create();
        var aircraft = Rolling(simulation);
        Previous(simulation, aircraft, 3, 7000, group: "Heavy", age: 100);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            var resumed = new Simulation(JsonStore.LoadSession(path));
            resumed.Advance(0.05);
            Assert.Equal(1, resumed.State.Performance.Incidents);
            JsonStore.Write(path, resumed.State);
            resumed = new Simulation(JsonStore.LoadSession(path));
            resumed.Advance(0.05);
            Assert.Equal(1, resumed.State.Performance.Incidents);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unknown_categories_do_not_invent_a_classification()
    {
        var simulation = Create();
        var aircraft = Rolling(simulation);
        aircraft.Specification.WeightCategory = null;
        Previous(simulation, aircraft, 1, 100);
        simulation.Advance(0.05);
        Assert.Equal(0, simulation.State.Performance.Incidents);
    }

    [Fact]
    public void Cancelled_takeoff_clears_its_history_and_a_resumed_roll_gets_a_new_time()
    {
        var simulation = Create();
        var aircraft = Rolling(simulation);
        simulation.Advance(1);
        var first = Assert.Single(simulation.State.RunwayDepartures);
        Assert.True(simulation.Execute("FOLLOW/CC").Success);
        Assert.Empty(simulation.State.RunwayDepartures);
        simulation.Advance(10);
        Assert.True(simulation.Execute("FOLLOW/CT").Success);
        simulation.Advance(0.05);
        var resumed = Assert.Single(simulation.State.RunwayDepartures);
        Assert.NotEqual(first.Id, resumed.Id);
        Assert.True(resumed.StartedSeconds > first.StartedSeconds + 10);
        Assert.Equal(aircraft.RunwayDepartureId, resumed.Id);
    }

    [Fact]
    public void Liftoff_updates_history_and_live_position_is_retained_after_aircraft_removal()
    {
        var simulation = Create();
        var aircraft = Rolling(simulation);
        aircraft.Specification.TakeoffDistanceFeet = 100;
        aircraft.Path = [aircraft.Position, aircraft.Position.Offset(180, 10)];
        aircraft.PathIndex = 1;
        for (var second = 0; second < 30 && aircraft.Phase != FlightPhase.Departing; second++)
        {
            simulation.Advance(1);
        }
        Assert.Equal(FlightPhase.Departing, aircraft.Phase);
        var history = Assert.Single(simulation.State.RunwayDepartures);
        Assert.True(history.Airborne);
        simulation.Advance(1);
        Assert.Equal(aircraft.Position, history.Position);
        var position = history.Position;
        simulation.State.Aircraft.Remove(aircraft);
        simulation.Advance(1);
        Assert.Equal(position, history.Position);
    }

    [Fact]
    public void Another_physical_runway_does_not_apply_the_history()
    {
        var simulation = Create();
        simulation.State.Airport.Runways.Add(new() { Name = "OTHER", PhysicalId = "parallel", Threshold = "H18", End = "E18" });
        var aircraft = Rolling(simulation);
        Previous(simulation, aircraft, 3, 100, "OTHER", "Heavy", 1);
        simulation.Advance(0.05);
        Assert.Equal(0, simulation.State.Performance.Incidents);
        Assert.Equal(2, simulation.State.RunwayDepartures.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invalid_saved_history_is_rejected(bool duplicate)
    {
        var simulation = Create();
        var aircraft = Rolling(simulation);
        var previous = Previous(simulation, aircraft, 1, 7000);
        if (duplicate)
        {
            simulation.State.RunwayDepartures.Add(new() { Runway = "36", CallSign = "OTHER" });
        }
        else
        {
            previous.StartedSeconds = simulation.State.ElapsedSeconds + 1;
        }
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, simulation.State);
            Assert.Throws<InvalidDataException>(() => JsonStore.LoadSession(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Original_type_groups_preserve_the_separate_heavy_classification()
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original type groups.");
            return;
        }
        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(directory, code);
            Assert.Equal("Airline", airport.AircraftTypes["B737"].OriginalTypeGroup);
            Assert.Equal(3, airport.AircraftTypes["B737"].WeightCategory);
            Assert.All(airport.AircraftTypes.Values, specification => Assert.False(string.IsNullOrWhiteSpace(specification.OriginalTypeGroup)));
        }
    }

    [Theory]
    [InlineData("CG", 2)]
    [InlineData("CA", 0)]
    public void Touch_and_go_checks_both_events_but_low_approach_checks_neither(string command, int incidents)
    {
        var simulation = Create();
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "FOLLOW");
        aircraft.Specification = new() { WeightCategory = 1 };
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.TouchdownDistanceMetres = 0;
        aircraft.AltitudeFeet = 0;
        aircraft.Path = [aircraft.Position];
        aircraft.PathIndex = 0;
        Assert.True(simulation.Execute("FOLLOW/" + command).Success);
        var previous = Previous(simulation, aircraft, 1, 2000);
        simulation.Advance(0.05);
        Assert.Equal(incidents, simulation.State.Performance.Incidents);
        Assert.Equal(command == "CA" ? previous.Id : aircraft.RunwayDepartureId, Assert.Single(simulation.State.RunwayDepartures).Id);
    }

    [Fact]
    public void Legacy_save_without_history_loads_without_inventing_previous_departures()
    {
        var simulation = Create();
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(simulation.State, JsonStore.Options))!;
        json.AsObject().Remove("RunwayDepartures");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, json.ToJsonString());
            var resumed = new Simulation(JsonStore.LoadSession(path));
            Assert.Empty(resumed.State.RunwayDepartures);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
