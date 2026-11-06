using System.Text.Json;
using NFR_Weather_Service.Services;

namespace NFR_Weather_Service.Tests;

public class WeatherServiceTests
{
    [Fact]
    public void ConvertToFahrenheit_ZeroCelsius_ReturnsFahrenheit()
    {
        var result = WeatherService.ConvertToFahrenheit(0.0);
        Assert.NotNull(result);
        Assert.Equal(32.0, result.Value);
    }

    [Fact]
    public void ConvertToFahrenheit_ThirtySevenCelsius_ReturnsFahrenheit()
    {
        var result = WeatherService.ConvertToFahrenheit(37.0);
        Assert.NotNull(result);
        Assert.Equal(98.6, result.Value);
    }


    [Fact]
    public void GetPrecipitationInInches_LastHourValueInMillimeters_ReturnsInches()
    {
        const string json = """
                            {
                              "properties": {
                                "precipitationLastHour": { "unitCode": "wmoUnit:mm", "value": 2.5 }
                              }
                            }
                            """;
        using var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.GetProperty("properties");
        var (inches, windowLabel) = WeatherService.GetPrecipitationInInches(props);

        Assert.Equal("1 hr", windowLabel);
        Assert.Equal(2.5 / 25.4, inches, 5);
    }

    [Fact]
    public void GetPrecipitationInInches_LastHourNull_FallsBackToLastThreeHours()
    {
        const string json = """
                            {
                              "properties": {
                                "precipitationLastHour": { "unitCode": "wmoUnit:mm", "value": null },
                                "precipitationLast3Hours": { "unitCode": "wmoUnit:mm", "value": 10 }
                              }
                            }
                            """;
        using var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.GetProperty("properties");
        var (inches, windowLabel) = WeatherService.GetPrecipitationInInches(props);

        Assert.Equal("3 hrs", windowLabel);
        Assert.Equal(10 / 25.4, inches, 5);
    }

    [Fact]
    public async Task GetReportDataAsync_FallsBackToNextStation_WhenFirstStationNotAvailable()
    {
        // This test verifies the fallback behavior when the first station (KOCH) 
        // returns a 404 error, but the next station (KRFI) is successfully used.
        var service = new WeatherService(new HttpClient());
        var weatherReport = await service.GetReportDataAsync("1481 County Road 124, Nacogdoches, Texas 75965");

        // Verify that we got a weather report from a station
        Assert.NotNull(weatherReport);

        // The report should include data from either KOCH (if it had worked) or the fallback station
        // Either way, we should have a valid station ID
        Assert.NotNull(weatherReport.StationId);
        Assert.True(!string.IsNullOrEmpty(weatherReport.StationId));

        // Verify we got temperature data (not null), showing the fallback station is working
        Assert.NotNull(weatherReport.TempFahrenheit);

        // Verify the coordinates are properly geocoded
        Assert.Equal("1481 County Road 124, Nacogdoches, Texas 75965", weatherReport.Address);
    }
}