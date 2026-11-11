namespace Tower.Core;

public sealed record AircraftRadioMessage(string Callsign, string AirportCode, string Text,
    string Runway, string IntentKey = "", double? DistanceNm = null, double? Value = null, string Detail = "", bool IncludeRunway = true);
