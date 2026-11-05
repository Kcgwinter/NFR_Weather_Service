namespace NFR_Weather_Service.Models;

public record WeatherReport(
    string Address,
    string StationId,
    double? TempFahrenheit,
    double RainInches,
    double SnowInches,
    double IceInches,
    bool FreezingRainActive
);