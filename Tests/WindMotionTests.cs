using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class WindMotionTests
{
    private static (Simulation Simulation, Aircraft Aircraft) Create(double from, double knots)
    {
        var airport = Airport.Training();
        airport.AircraftTypes["TEST"] = new()
        {
            Type = "TEST", ApproachSpeed = 100, PatternSpeed = 100, TakeoffSpeed = 60,
            TakeoffDistanceFeet = 1000 / Coordinates.MetresPerFoot
        };
        var simulation = new Simulation(new() { Airport = airport,
            Options = new() { WindFromDegrees = from, WindKnots = knots, AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Arrival, "WIND", "TEST", useTrafficFlow: false);
        aircraft.SpeedKnots = 100;
        aircraft.TargetSpeedKnots = 100;
        return (simulation, aircraft);
    }

    [Theory]
    [InlineData(90, 0, 100)]
    [InlineData(90, 20, 80)]
    [InlineData(270, 20, 120)]
    public void Wind_from_direction_sets_the_correct_headwind_or_tailwind(double from, double knots, double speed)
    {
        var options = new ScenarioOptions { WindFromDegrees = from, WindKnots = knots };
        var result = WindMotion.Track(100, 90, options);
        Assert.True(result.CanHoldTrack);
        Assert.Equal(90, result.Heading, 8);
        Assert.Equal(speed, result.GroundSpeedKnots, 8);
    }

    [Theory]
    [InlineData(0, 78.463040967)]
    [InlineData(180, 101.536959033)]
    public void Crosswind_crab_cancels_sideways_velocity_on_a_tracked_route(double from, double heading)
    {
        var options = new ScenarioOptions { WindFromDegrees = from, WindKnots = 20 };
        var result = WindMotion.Track(100, 90, options);
        Assert.True(result.CanHoldTrack);
        Assert.Equal(heading, result.Heading, 7);
        var velocity = WindMotion.GroundVelocity(100, result.Heading, options);
        Assert.Equal(0, velocity.Y, 8);
        Assert.Equal(Math.Sqrt(9600), velocity.X, 8);
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(100, 150)]
    public void Impossible_track_has_no_fake_forward_speed_or_invalid_numbers(double airspeed, double wind)
    {
        var result = WindMotion.Track(airspeed, 90, new() { WindFromDegrees = 0, WindKnots = wind });
        Assert.False(result.CanHoldTrack);
        Assert.True(double.IsFinite(result.Heading));
        Assert.True(double.IsFinite(result.GroundSpeedKnots));
    }

    [Fact]
    public void Heading_instruction_holds_the_nose_heading_and_allows_wind_drift()
    {
        var (simulation, aircraft) = Create(0, 20);
        aircraft.Phase = FlightPhase.Vectoring;
        aircraft.Heading = 90;
        aircraft.Position = new(5000, 5000);
        simulation.Advance(10);
        Assert.Equal(90, aircraft.Heading, 8);
        Assert.Equal(5000 + 100 * Simulation.MetresPerKnotSecond * 10, aircraft.Position.X, 6);
        Assert.Equal(5000 - 20 * Simulation.MetresPerKnotSecond * 10, aircraft.Position.Y, 6);
        Assert.InRange(aircraft.GroundTrackDegrees!.Value, 101, 102);
        Assert.Equal(Math.Sqrt(10400), aircraft.GroundSpeedKnots, 6);
    }

    [Fact]
    public void Circuit_uses_ground_track_and_wind_correction_survives_save_and_resume()
    {
        var (simulation, aircraft) = Create(0, 20);
        aircraft.Phase = FlightPhase.Circuit;
        aircraft.Position = new(5000, 5000);
        aircraft.Heading = 90;
        aircraft.Path = [new(7000, 5000)];
        aircraft.PathIndex = 0;
        simulation.Advance(3);
        var path = Path.GetTempFileName();
        try
        {
            JsonStore.Write(path, simulation.State);
            var restored = new Simulation(JsonStore.LoadSession(path));
            simulation.Advance(7);
            restored.Advance(7);
            var resumed = Assert.Single(restored.State.Aircraft);
            Assert.Equal(5000, aircraft.Position.Y, 6);
            Assert.Equal(5000 + Math.Sqrt(9600) * Simulation.MetresPerKnotSecond * 10, aircraft.Position.X, 5);
            Assert.Equal(78.463040967, aircraft.Heading, 7);
            Assert.Equal(aircraft.Position, resumed.Position);
            Assert.Equal(aircraft.Heading, resumed.Heading);
            Assert.Equal(aircraft.GroundTrackDegrees, resumed.GroundTrackDegrees);
            Assert.Equal(aircraft.GroundSpeedKnots, resumed.GroundSpeedKnots);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(180, 80)]
    [InlineData(0, 120)]
    public void Approach_distance_uses_ground_speed_and_does_not_change_the_runway(double from, double expected)
    {
        var (simulation, aircraft) = Create(from, 20);
        var threshold = simulation.State.Airport.Point("R18");
        aircraft.Position = threshold.Offset(0, 8 * 1852);
        aircraft.Phase = FlightPhase.Approach;
        aircraft.Heading = 180;
        Command(simulation, "WIND/CL");
        var distance = aircraft.Position.Distance(threshold);
        simulation.Advance(10);
        Assert.Equal(expected * Simulation.MetresPerKnotSecond * 10, distance - aircraft.Position.Distance(threshold), 6);
        Assert.Equal("18", aircraft.Runway);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(180, 20)]
    [InlineData(0, 20)]
    public void Takeoff_roll_uses_wind_adjusted_ground_distance_and_reaches_rotation_airspeed(double from, double knots)
    {
        var (simulation, aircraft) = Create(from, knots);
        aircraft.Direction = FlightDirection.Departure;
        aircraft.Phase = FlightPhase.LinedUp;
        aircraft.Position = simulation.State.Airport.Point("R18");
        aircraft.Heading = 180;
        aircraft.SpeedKnots = 0;
        var start = aircraft.Position;
        var expectedDistance = WindMotion.TakeoffDistance(aircraft, 1000, 180, simulation.State.Options);
        Command(simulation, "WIND/CT");
        for (var step = 0; step < 3000 && aircraft.Phase == FlightPhase.TakeoffRoll; step++)
        {
            simulation.Advance(0.1);
            Assert.Equal(0, aircraft.Position.X, 6);
        }
        Assert.Equal(FlightPhase.Departing, aircraft.Phase);
        Assert.Equal(expectedDistance, start.Distance(aircraft.Position), 5);
        Assert.InRange(aircraft.SpeedKnots, 59.5, 60.5);
    }

    [Fact]
    public void Corrupt_saved_ground_track_is_rejected()
    {
        var (simulation, aircraft) = Create(0, 20);
        aircraft.GroundTrackDegrees = 400;
        var path = Path.GetTempFileName();
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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(90, 15)]
    [InlineData(0, 15)]
    [Trait("Category", "Integration")]
    public void Canyon_arrival_and_departure_complete_in_calm_headwind_and_crosswind(double from, double knots)
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA") ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");
        var airport = OriginalData.LoadAirport(data, "CYN");
        var simulation = new Simulation(new() { Airport = airport, Options = new() { Runway = "9", WindFromDegrees = from, WindKnots = knots, AircraftPerMinute = 0 } });
        var arrival = simulation.AddAircraft(FlightDirection.Arrival, "ARRIVE", "C182", "9");
        for (var second = 0; second < 2400 && arrival.Phase != FlightPhase.Completed; second++)
        {
            if (arrival.IsOnFrequency && arrival.Phase is FlightPhase.Circuit or FlightPhase.Approach && !arrival.LandingCleared)
            {
                Command(simulation, "ARRIVE/CL");
            }
            if (arrival.Phase == FlightPhase.ArrivalHold)
            {
                Command(simulation, "ARRIVE/TP");
            }
            if (arrival.Phase == FlightPhase.TaxiIn && arrival.Holding)
            {
                _ = simulation.Execute("ARRIVE/TU");
            }
            simulation.Advance(1);
        }
        Assert.Equal(FlightPhase.Completed, arrival.Phase);
        var departure = simulation.AddAircraft(FlightDirection.Departure, "DEPART", "C182", "9", "G3");
        Command(simulation, "DEPART/PB");
        for (var second = 0; second < 3000 && departure.Phase != FlightPhase.Completed; second++)
        {
            if (departure.Phase == FlightPhase.ReadyForTaxi)
            {
                Command(simulation, "DEPART/T9");
            }
            if (departure.Phase == FlightPhase.HoldingShort)
            {
                Command(simulation, "DEPART/CT");
            }
            if (departure.Phase == FlightPhase.TaxiOut && departure.Holding)
            {
                _ = simulation.Execute("DEPART/TU");
            }
            if (departure.Phase == FlightPhase.Departing && departure.GeneratedDeparture is { Stage: GeneratedDepartureStage.ToDestination } && departure.Movement is not null)
            {
                Command(simulation, "DEPART/FD");
            }
            simulation.Advance(1);
        }
        Assert.True(departure.Phase == FlightPhase.Completed,
            $"{departure.Phase}, stage {departure.GeneratedDeparture?.Stage}, altitude {departure.AltitudeFeet}, target {departure.GeneratedDeparture?.FirstAltitudeFeetAgl}, speed {departure.SpeedKnots}, distance {departure.GeneratedDeparture?.Destination.Position.Distance(departure.Position)}");
        Assert.Equal(1, simulation.State.Performance.Arrivals);
        Assert.Equal(1, simulation.State.Performance.Departures);
    }

    private static void Command(Simulation simulation, string command)
    {
        var result = simulation.Execute(command);
        Assert.True(result.Success, command + ": " + result.Message);
    }
}
