using System.Text.Json;
using Tower.Core;
using Tower.Infrastructure;
using Xunit;

namespace Tower.Tests;

public sealed class AircraftAnimationTests
{
    [Fact]
    public void Takeoff_climb_retracts_and_final_extends_over_simulation_time()
    {
        var aircraft = new Aircraft { Phase = FlightPhase.TakeoffRoll };
        AircraftAnimation.Update(aircraft, .05);
        Assert.Equal(1, aircraft.Animation!.GearExtension);
        aircraft.Phase = FlightPhase.Departing;
        aircraft.AltitudeFeet = 199;
        AircraftAnimation.Update(aircraft, 3);
        Assert.Equal(1, aircraft.Animation.GearExtension);
        aircraft.AltitudeFeet = 200;
        AircraftAnimation.Update(aircraft, 3);
        Assert.Equal(.5, aircraft.Animation.GearExtension, 6);
        AircraftAnimation.Update(aircraft, 3);
        Assert.Equal(0, aircraft.Animation.GearExtension);
        aircraft.Phase = FlightPhase.Approach;
        aircraft.AltitudeFeet = 1400;
        AircraftAnimation.Update(aircraft, 4);
        Assert.Equal(.5, aircraft.Animation.GearExtension, 6);
        AircraftAnimation.Update(aircraft, 4);
        Assert.Equal(1, aircraft.Animation.GearExtension);
    }

    [Theory]
    [InlineData(FlightPhase.GoAround)]
    [InlineData(FlightPhase.Breakout)]
    public void Missed_approach_retains_gear_near_ground_then_retracts(FlightPhase phase)
    {
        var aircraft = new Aircraft { Phase = phase, AltitudeFeet = 100, Animation = new() };
        AircraftAnimation.Update(aircraft, 3);
        Assert.Equal(1, aircraft.Animation.GearExtension);
        aircraft.AltitudeFeet = 500;
        AircraftAnimation.Update(aircraft, 6);
        Assert.Equal(0, aircraft.Animation.GearExtension);
    }

    [Fact]
    public void Base_and_low_approach_deploy_while_vectoring_retains_the_command()
    {
        var aircraft = new Aircraft { Phase = FlightPhase.Circuit, CircuitLeg = "base", AltitudeFeet = 1800,
            Animation = new() { GearExtension = 0, GearCommandedDown = false } };
        AircraftAnimation.Update(aircraft, 4);
        Assert.Equal(.5, aircraft.Animation.GearExtension, 6);
        aircraft.Phase = FlightPhase.Vectoring;
        AircraftAnimation.Update(aircraft, 4);
        Assert.Equal(1, aircraft.Animation.GearExtension);
        aircraft.Animation = new() { GearExtension = 0, GearCommandedDown = false };
        aircraft.Phase = FlightPhase.LowApproach;
        AircraftAnimation.Update(aircraft, 8);
        Assert.Equal(1, aircraft.Animation.GearExtension);
    }

    [Fact]
    public void Fixed_gear_and_ground_contact_never_retract()
    {
        var aircraft = new Aircraft { Phase = FlightPhase.Departing, AltitudeFeet = 6000,
            Specification = new() { OriginalFixedGear = true } };
        AircraftAnimation.Update(aircraft, 60);
        Assert.Equal(1, aircraft.Animation!.GearExtension);
        aircraft.Specification.OriginalFixedGear = false;
        aircraft.Phase = FlightPhase.LandingRoll;
        aircraft.Animation.GearExtension = .2;
        AircraftAnimation.Update(aircraft, .05);
        Assert.Equal(1, aircraft.Animation.GearExtension);
    }

    [Theory]
    [InlineData(FlightPhase.AtGate, 0, false)]
    [InlineData(FlightPhase.Pushback, 0, false)]
    [InlineData(FlightPhase.TaxiOut, 0, false)]
    [InlineData(FlightPhase.TakeoffRoll, 0, true)]
    [InlineData(FlightPhase.Approach, 1000, true)]
    [InlineData(FlightPhase.Departing, 10000, false)]
    public void Landing_lights_follow_ground_operation_and_airborne_height(FlightPhase phase, double height, bool lit)
    {
        var aircraft = new Aircraft { Phase = phase, AltitudeFeet = height };
        Assert.Equal(lit, AircraftAnimation.Current(aircraft).LandingLights);
    }

    [Fact]
    public void Original_conditional_flags_combine_without_hiding_fixed_gear_or_wings()
    {
        var state = new AircraftAnimationState { GearExtension = 0, LandingLights = false };
        Assert.True(new ModelFace([0], 7, "FIXEDGEAR", 0).Visible(state));
        Assert.True(new ModelFace([0], 7, "WING", 0x1000).Visible(state));
        Assert.False(new ModelFace([0], 7, "GEAR", 0x1001).Visible(state));
        Assert.False(new ModelFace([0], 7, "LIGHT", 0x200).Visible(state));
        state.LandingLights = true;
        Assert.False(new ModelFace([0], 7, "GEARLIGHT", 0x201).Visible(state));
        state.GearExtension = .5;
        Assert.True(new ModelFace([0], 7, "GEARLIGHT", 0x201).Visible(state));
    }

    [Fact]
    public void Partial_gear_moves_towards_its_source_attachment_without_changing_the_mesh()
    {
        var face = new ModelFace([0, 1, 2], 7, "GEAR", 1);
        var mesh = new AircraftMesh("SYNTHETIC", [new(-5, 0, 0), new(5, 0, 0), new(0, 4, 0)], [face]);
        var aircraft = new Aircraft { Heading = 0, Specification = new() { WingSpanMetres = 10 } };
        Assert.Equal(2, mesh.GearOffset(face, 0, aircraft, .5).Altitude, 6);
        Assert.Equal(0, mesh.GearOffset(face, 2, aircraft, .5).Altitude, 6);
        Assert.Equal(0, mesh.GearOffset(face, 0, aircraft, 1).Altitude, 6);
        Assert.Equal(0, mesh.Vertices[0].Y);
        aircraft.PitchDegrees = 10;
        aircraft.BankDegrees = 20;
        var tilted = mesh.GearOffset(face, 0, aircraft, .5);
        Assert.True(tilted.Position.Distance(new()) > 0);
        Assert.InRange(tilted.Altitude, 0, 2);
    }

    [Fact]
    public void Mid_transition_save_resume_and_pause_preserve_animation()
    {
        var simulation = new Simulation(new() { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "ANIM");
        aircraft.Phase = FlightPhase.Vectoring;
        aircraft.AltitudeFeet = 3000;
        aircraft.Animation = new() { GearExtension = 1, GearCommandedDown = false };
        simulation.Advance(2);
        var restored = new Simulation(JsonSerializer.Deserialize<Session>(JsonSerializer.Serialize(simulation.State, JsonStore.Options), JsonStore.Options)!);
        var savedExtension = aircraft.Animation.GearExtension;
        simulation.Paused = true;
        simulation.Advance(3);
        Assert.Equal(savedExtension, aircraft.Animation.GearExtension);
        simulation.Paused = false;
        simulation.Advance(2);
        restored.Advance(2);
        Assert.Equal(aircraft.Animation.GearExtension, restored.State.Aircraft.Single().Animation!.GearExtension, 8);
        Assert.False(new AircraftAnimationState { GearExtension = double.NaN }.IsValid());
        Assert.False(new AircraftAnimationState { GearExtension = 1.1 }.IsValid());
    }

    [Fact]
    public void Legacy_save_initializes_from_phase_and_camera_reads_do_not_advance_animation()
    {
        var aircraft = new Aircraft { Phase = FlightPhase.Vectoring, AltitudeFeet = 4000 };
        Assert.Equal(0, AircraftAnimation.Current(aircraft).GearExtension);
        Assert.Null(aircraft.Animation);
        aircraft.Animation = new() { GearExtension = .4, GearCommandedDown = false };
        for (var index = 0; index < 100; index++)
        {
            Assert.Equal(.4, AircraftAnimation.Current(aircraft).GearExtension);
        }
    }

    [Fact]
    public void Original_fixed_gear_metadata_is_preserved_without_reclassifying_source_types()
    {
        var data = Environment.GetEnvironmentVariable("TOWER_ORIGINAL_DATA") ?? throw new InvalidOperationException("Set TOWER_ORIGINAL_DATA.");
        var airport = OriginalData.LoadAirport(data, "CYN");
        Assert.True(airport.AircraftTypes["C182"].OriginalFixedGear);
        // The supplied TYP says FALSE even for this physically fixed-gear type.
        // Its unflagged source mesh keeps its wheels visible independently.
        Assert.False(airport.AircraftTypes["C172"].OriginalFixedGear);
        var catalog = new AircraftModelCatalog(data);
        Assert.DoesNotContain(catalog.Get("C172")!.Faces, face => (face.Flags & ModelFace.RetractableGear) != 0);
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(1.1)]
    public void Session_loader_rejects_invalid_animation_progress(double extension)
    {
        var simulation = new Simulation(new() { Options = new() { AircraftPerMinute = 0 } });
        var aircraft = simulation.AddAircraft(FlightDirection.Departure, "INVALID");
        aircraft.Animation = new() { GearExtension = extension };
        var path = Path.Combine(Path.GetTempPath(), "Tower-Animation-" + Guid.NewGuid().ToString("N") + ".json");
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
}
