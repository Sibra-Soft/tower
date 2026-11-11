using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class TrafficFlowTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(59, "A")]
    [InlineData(60, "B")]
    [InlineData(79, "B")]
    [InlineData(80, "C")]
    [InlineData(99, "C")]
    public void Departure_percentages_use_original_boundaries(int roll, string expected)
    {
        Assert.Equal(expected, TrafficFlows.SelectDeparture([new("A", 60), new("B", 20), new("C", 20)], roll).Fix);
    }

    [Fact]
    public void Uncovered_percentages_fall_back_to_first_entry_without_normalizing()
    {
        Assert.Equal("A", TrafficFlows.SelectDeparture([new("A", 10), new("B", 10)], 99).Fix);
    }

    private static Session CreateSession()
    {
        var session = new Session();
        var airport = session.Airport;
        airport.Original.Fixes["NORTH"] = new("NORTH", airport.Latitude + 1, airport.Longitude);
        airport.Original.Fixes["EAST"] = new("EAST", airport.Latitude, airport.Longitude + 1);
        airport.Original.ArrivalFlow = [new("EAST", 100), new("NORTH", 0)];
        airport.Original.DepartureFlow = [new("EAST", 50), new("NORTH", 50)];
        return session;
    }

    [Fact]
    public void Arrival_selects_alignment_even_when_that_fix_has_zero_weight()
    {
        var session = CreateSession();
        var aircraft = new Simulation(session).AddAircraft(FlightDirection.Arrival);
        Assert.Equal("NORTH", aircraft.GeneratedRoute!.Origin);
        Assert.Null(aircraft.OriginalPlan);
        Assert.Equal(0x6D2B79F5u, session.TrafficFlowRandomState);
    }

    [Fact]
    public void Speech_filter_and_generated_destination_work_together()
    {
        var session = CreateSession();
        var aircraft = new Simulation(session, ["APPROVED"]).AddAircraft(FlightDirection.Departure);
        Assert.Equal("APPROVED", aircraft.CallSign);
        Assert.NotNull(aircraft.GeneratedRoute);
        Assert.True(aircraft.FollowOriginalDepartureRoute);
    }

    [Fact]
    public void Save_preserves_selected_routes_and_next_departure_draw()
    {
        var session = CreateSession();
        var simulation = new Simulation(session);
        simulation.AddAircraft(FlightDirection.Departure, "FIRST");
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            JsonStore.Write(path, session);
            var restored = JsonStore.LoadSession(path);
            Assert.Equal(session.Aircraft[0].GeneratedRoute, restored.Aircraft[0].GeneratedRoute);
            Assert.Equal(simulation.AddAircraft(FlightDirection.Departure, "SECOND").GeneratedRoute,
                new Simulation(restored).AddAircraft(FlightDirection.Departure, "SECOND").GeneratedRoute);
            Assert.Equal(session.TrafficFlowRandomState, restored.TrafficFlowRandomState);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Generated_arrivals_remain_spaced_and_obey_the_configured_rate()
    {
        var session = CreateSession();
        session.Options.AircraftPerMinute = 2;
        var simulation = new Simulation(session);
        simulation.Advance(29);
        Assert.Empty(session.Aircraft);
        simulation.Advance(2);
        Assert.Single(session.Aircraft);
        var first = session.Aircraft[0];
        var second = simulation.AddAircraft(FlightDirection.Arrival);
        Assert.True(first.Position.Distance(second.Position) > 3 * 1852);
    }

    [Fact]
    public void Departure_turns_towards_the_flow_only_after_the_runway_end()
    {
        var session = CreateSession();
        session.Options.AircraftPerMinute = 0;
        session.Airport.Original.DepartureFlow = [new("EAST", 100)];
        var simulation = new Simulation(session);
        var aircraft = simulation.AddAircraft(FlightDirection.Departure);
        aircraft.Phase = FlightPhase.Departing;
        aircraft.AltitudeFeet = 1000;
        aircraft.SpeedKnots = 180;
        aircraft.Position = session.Airport.Point("R18");
        simulation.Advance(1);
        Assert.Equal(180, aircraft.Heading);
        aircraft.Position = session.Airport.Point("R36").Offset(180, 100);
        simulation.Advance(1);
        Assert.True(aircraft.Heading < 180);
        Assert.True(aircraft.OriginalDepartureRouteStarted);
    }

    [Fact]
    public void Missing_flow_keeps_legacy_behavior_and_scheduled_traffic_is_not_overridden()
    {
        Assert.Null(new Simulation(new Session()).AddAircraft(FlightDirection.Arrival).GeneratedRoute);
        var session = CreateSession();
        session.Options.AircraftPerMinute = 0;
        session.Schedule.Add(new() { CallSign = "SCHEDULED", Type = "B738", Runway = "18", TimeSeconds = 0, Direction = FlightDirection.Arrival });
        new Simulation(session).Advance(1);
        Assert.Null(Assert.Single(session.Aircraft).GeneratedRoute);
    }

    [Theory]
    [InlineData("CYN", 6)]
    [Trait("Category", "Integration")]
    public void Source_tables_are_imported_without_losing_order_or_percentages(string code, int count)
    {
        var directory = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set TOWER_ORIGINAL_DATA to verify SEC traffic flows.");
            return;
        }
        var airport = OriginalData.LoadAirport(directory, code);
        Assert.Equal(count, airport.Original.ArrivalFlow.Count);
        Assert.Equal(count, airport.Original.DepartureFlow.Count);
        Assert.Equal(100, airport.Original.ArrivalFlow.Sum(entry => entry.Percent));
        Assert.Equal(100, airport.Original.DepartureFlow.Sum(entry => entry.Percent));
    }
}
