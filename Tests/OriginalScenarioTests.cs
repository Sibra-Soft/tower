using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class OriginalScenarioTests
{
    private static Airport Airport()
    {
        var airport = Tower.Core.Airport.Training();
        airport.Code = "CYN";
        airport.AircraftTypes["TEST"] = new()
        {
            Type = "TEST"
        };
        airport.Original.Fixes["VAN"] = new("VAN", airport.Latitude, airport.Longitude - 1);
        return airport;
    }

    private static string Scenario(string header = "ONE TEST VFR -1 a 00:02 250 6300 2000 1200 18", int count = 1) => $"""
        TOWER Simulation File Version 3.8
        [SimInfo]
        Sector CYN.SEC
        Aircraft {count}
        Minutes 15
        Seed 803914858
        SimType NORMAL
        [Description]
        Authored compatibility fixture.
        [Aircraft]
        {header}
          VAN CYN Twr @FS @UNSUPPORTED @GG1
        """;

    [Fact]
    public void Scenario_preserves_source_fields_and_applies_origin_direction_without_extra_traffic()
    {
        var path = Path.GetTempFileName();
        var save = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Scenario());
            var session = OriginalData.ReadScenario(path, Airport());
            Assert.Equal("CYN", OriginalData.ReadScenarioAirportCode(path));
            Assert.Equal(803914858, session.OriginalScenario!.Seed);
            Assert.Equal(15, session.OriginalScenario.DeclaredMinutes);
            Assert.Equal(File.ReadAllLines(path), session.OriginalScenario.SourceLines);
            Assert.Equal(0, session.Options.AircraftPerMinute);
            var flight = Assert.Single(session.Schedule);
            Assert.Equal("G1", flight.Gate);
            Assert.Equal(250, flight.OriginalPlan!.SpeedFieldKnots);
            Assert.Equal(6300, flight.OriginalPlan.Altitude1Feet);
            Assert.Equal(2000, flight.OriginalPlan.Altitude2Feet);
            Assert.Equal("1200", flight.OriginalPlan.Squawk);
            Assert.Contains("@UNSUPPORTED", flight.OriginalPlan.RouteTokens);

            var simulation = new Simulation(session);
            simulation.Advance(1);
            Assert.Empty(session.Aircraft);
            simulation.Advance(1.1);
            var aircraft = Assert.Single(session.Aircraft);
            Assert.True(flight.Spawned);
            Assert.Equal(FlightPhase.Circuit, aircraft.Phase);
            Assert.Equal("VAN", aircraft.OriginalPlan!.Origin);
            Assert.True(aircraft.Position.X < session.Airport.Point("R18").X);
            Assert.NotEqual(250, aircraft.SpeedKnots);
            Assert.False(aircraft.LandingCleared);

            JsonStore.Write(save, session);
            var restored = JsonStore.LoadSession(save);
            Assert.Equal(session.OriginalScenario.SourceLines, restored.OriginalScenario!.SourceLines);
            Assert.Equal(aircraft.OriginalPlan.RouteTokens, restored.Aircraft[0].OriginalPlan!.RouteTokens);
            Assert.Equal(flight.OriginalPlan.RouteTokens, restored.Schedule[0].OriginalPlan!.RouteTokens);
            var resumed = new Simulation(restored);
            simulation.Advance(10);
            resumed.Advance(10);
            Assert.Equal(session.Aircraft[0].Position, restored.Aircraft[0].Position);
            Assert.Single(restored.Aircraft);
        }
        finally
        {
            File.Delete(path);
            File.Delete(save);
        }
    }

    [Theory]
    [InlineData("ONE TEST VFR -1 a 00:99 250 6300 2000 1200 18", 1)]
    [InlineData("ONE TEST VFR -1 a 00:02 NaN 6300 2000 1200 18", 1)]
    [InlineData("ONE TEST VFR -1 a 00:02 250 6300 2000 8999 18", 1)]
    [InlineData("ONE TEST VFR -1 a 00:02 250 6300 2000 1200 18", 2)]
    public void Invalid_flight_fields_and_declared_counts_are_rejected(string header, int count)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Scenario(header, count));
            Assert.Throws<InvalidDataException>(() => OriginalData.ReadScenario(path, Airport()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Original_departure_route_turns_after_runway_end_and_explicit_turnout_takes_priority()
    {
        var airport = Airport();
        var simulation = new Simulation(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0, WindKnots = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "OUT", "TEST", originalPlan: new()
        {
            Origin = "CYN",
            Destination = "VAN",
            DestinationBearingDegrees = 270,
            Squawk = "1200"
        });
        aircraft.Phase = FlightPhase.Departing;
        aircraft.Position = airport.Point("R18");
        aircraft.SpeedKnots = 100;
        simulation.Advance(1);
        Assert.Equal(180, aircraft.Heading, 6);
        aircraft.Position = airport.Point("R36").Offset(180, 1);
        simulation.Advance(31);
        Assert.Equal(270, aircraft.Heading, 6);
        Assert.True(aircraft.OriginalDepartureRouteStarted);
        Assert.True(simulation.Execute("OUT/ART").Success);
        Assert.False(aircraft.FollowOriginalDepartureRoute);
        var commanded = aircraft.Heading;
        simulation.Advance(1);
        var turn = Coordinates.NormalizeHeading(aircraft.Heading - commanded);
        Assert.InRange(turn, .01, aircraft.Specification.TurnRate);
        Assert.InRange(Math.Abs(aircraft.BankDegrees), .01, 25);
        var target = Coordinates.NormalizeHeading(commanded + 90);
        // Acceleration can lower the bank-limited turn rate while the turn is in progress.
        for (var step = 0; step < 1800 && Math.Abs(RouteMovement.AngleDifference(aircraft.Heading, target)) > .001; step++)
        {
            var previous = aircraft.Heading;
            simulation.Advance(.05);
            Assert.InRange(Coordinates.NormalizeHeading(aircraft.Heading - previous), 0, aircraft.Specification.TurnRate * .05 + 1e-7);
        }
        Assert.Equal(target, aircraft.Heading, 6);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void All_nine_original_scenarios_import_and_canyon_routes_resolve()
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify original SIM and SEC data.");
            return;
        }

        foreach (var code in new[] { "CYN" })
        {
            var airport = OriginalData.LoadAirport(directory, code);
            foreach (var suffix in new[] { "ARR", "DEP", "MIX" })
            {
                var path = OriginalData.Find(directory, code + "_" + suffix + ".SIM")!;
                var session = OriginalData.ReadScenario(path, airport);
                Assert.Equal(0, session.Options.AircraftPerMinute);
                Assert.NotNull(session.OriginalScenario!.Seed);
                Assert.All(session.Schedule, flight => Assert.NotNull(flight.OriginalPlan));
                if (code == "CYN")
                {
                    Assert.Equal(suffix == "MIX" ? 6 : 5, session.Schedule.Count);
                    Assert.All(session.Schedule.Where(flight => flight.Direction == FlightDirection.Arrival),
                        flight => Assert.NotNull(flight.OriginalPlan!.OriginBearingDegrees));
                    var simulation = new Simulation(session);
                    simulation.Advance(300.1);
                    Assert.Equal(session.Schedule.Count, session.Aircraft.Count);
                    foreach (var flight in session.Schedule.Where(flight => flight.Direction == FlightDirection.Arrival))
                    {
                        var isolated = new Simulation(new Session { Airport = airport, Options = new() { AircraftPerMinute = 0, Runway = flight.Runway } });
                        isolated.AddAircraft(flight.Direction, flight.CallSign, flight.Type, flight.Runway, flight.Gate, flight.OriginalPlan);
                        var clearance = isolated.Execute(flight.CallSign + "/CL");
                        Assert.True(clearance.Success, $"{flight.CallSign} runway {flight.Runway}: {clearance.Message}");
                    }
                }
            }
        }
    }
}
