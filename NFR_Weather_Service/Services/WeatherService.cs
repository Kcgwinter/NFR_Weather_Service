using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Localization;
using NFR_Weather_Service.Models;

namespace NFR_Weather_Service.Services;

public class WeatherService
{
    private readonly ILogger<WeatherService> _logger;
    private readonly HttpClient _http;
    private string noaaUrl = "https://api.weather.gov";

    public WeatherService(ILogger<WeatherService> logger, HttpClient http)
    {
        _logger = logger;
        _http = http;

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NFR Weather Service Demo (kcgwinter@gmail.com)");
    }

    public async Task<WeatherReport> GetReportDataAsync(string address)
    {
        //Step 1: Convert Address to Long/Lat Coordinates (Geocoding.geo.census.gov)
        var coordinates = await ConvertAddressAsync(address);

        //Step 2: Lookup Station / Grid Url from (NOAA API) with Long/Lat Coordinates
        var (stationId, gridUrl) = await GetNOAADataAsync(coordinates);
        //Step 3: Fetch Weather Stats from the closest station
        var (tempF, rainInches, isFrozenPrecipitation) = await GetLatestWeatherDataAsync(stationId);
        //Step 4: Fetch Grid Data (Snow & Ice Depth) // if possible
        var (snowInches, iceInches) = await GetGridAccuAsync(gridUrl);

        //Todo: remove throw
        return new WeatherReport(
            address,
            stationId,
            tempF,
            rainInches,
            snowInches,
            iceInches,
            isFrozenPrecipitation
        );
    }

    private async Task<(double snowInches, double iceInches)> GetGridAccuAsync(string gridUrl)
    {
        using var doc = await GetJsonAsync(gridUrl, "application/geo+json");
        var props = doc.RootElement.GetProperty("properties");
        double snowMeters = GetMaxFromGridArray(props, "snowfallAmount");
        double iceMeters = GetMaxFromGridArray(props, "iceAccumulation");
        
        return (snowMeters * 39.3701, iceMeters * 39.3701);
    }

    private async Task<(double? tempF, double rainInches, bool isFreezingRain)> GetLatestWeatherDataAsync(
        string stationId)
    {
        string url = $"{noaaUrl}/stations/{stationId}/observations/latest";

        using var doc = await GetJsonAsync(url, "application/geo+json");

        //temperature of last hour
        var props = doc.RootElement.GetProperty("properties");
        double? tempC = GetValueFromJson(props, "temperature");
        double? tempF = tempC.HasValue ? (tempC.Value * 9.0 / 5.0) + 32.0 : null;

        //rainfall of last hour or if 0 check if last 3 hours
        //double? rainInMeter = GetValueFromJson(props, "precipitationLastHour");        // refactored to LastHours not always has value and gives different units
        //double rainInches = rainInMeter.HasValue ? rainInMeter.Value * 39.3701 : 0.0;
        var (rainInches, _) = GetPrecipitationInInches(props);

        bool frozenPrecipitation = false;
        //todo: get Freezing Rain
        string desc = props.TryGetProperty("textDescription", out var d) ? d.GetString() ?? "" : "";
        frozenPrecipitation = desc.Contains("Freezing", StringComparison.OrdinalIgnoreCase) ||
                                   desc.Contains("Snow", StringComparison.OrdinalIgnoreCase) ||
                                   desc.Contains("Sleet", StringComparison.OrdinalIgnoreCase) ||
                                   desc.Contains("Ice Pellets", StringComparison.OrdinalIgnoreCase) ||
                                   desc.Contains("Blizzard", StringComparison.OrdinalIgnoreCase);

        return (tempF, rainInches, frozenPrecipitation);
    }

    private async Task<(string stationId, string gridUrl)> GetNOAADataAsync(Coordinates coordinates)
    {
        string pointUrl =
            FormattableString.Invariant(
                $"{noaaUrl}/points/{coordinates.Latitude:F4},{coordinates.Longitude:F4}");
        using var pointDoc = await GetJsonAsync(pointUrl, "application/geo+json");

        var props = pointDoc.RootElement.GetProperty("properties");

        string stationsUrl = props.GetProperty("observationStations").GetString()!;
        string gridUrl = props.GetProperty("forecastGridData").GetString()!;

        using var stationDoc = await GetJsonAsync(stationsUrl, acceptHeader: "application/geo+json");
        var features = stationDoc.RootElement.GetProperty("features");

        //Todo: check if no station is nearby
        if (features.GetArrayLength() == 0)
            throw new Exception(
                $"No stations found near target coordinated {coordinates.Latitude}/{coordinates.Longitude}");

        string stationId = features[0].GetProperty("properties").GetProperty("stationIdentifier").GetString()!;

        return (stationId, gridUrl);
    }

    private async Task<Coordinates> ConvertAddressAsync(string address)
    {
        string encoded = HttpUtility.UrlEncode(address);
        // string url =
        //     $"https://geocoding.geo.census.gov/geocoder/locations/onelineaddress?address={encoded}&benchmark=Public_AR_Current&format=json";

        string url =
            $"https://geocoding.geo.census.gov/geocoder/locations/onelineaddress?address={encoded}&benchmark=Public_AR_Current&format=json";

        using var doc = await GetJsonAsync(url, acceptHeader: "application/json");

        var matches = doc.RootElement.GetProperty("result").GetProperty("addressMatches");

        if (matches.GetArrayLength() == 0)
            throw new Exception($"No matching address for {address} found in Geocoding.geo.census.gov");

        var coords = matches[0].GetProperty("coordinates");
        return new Coordinates(coords.GetProperty("y").GetDouble(), coords.GetProperty("x").GetDouble());
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string acceptHeader)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", acceptHeader);
        using var response = await _http.SendAsync(request);

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        JsonDocument document = JsonDocument.Parse(json);
        return document;
    }

    private static double? GetValueFromJson(JsonElement parent, string propName)
    {
        if (parent.TryGetProperty(propName, out var prop) &&
            prop.TryGetProperty("value", out var val) &&
            val.ValueKind == JsonValueKind.Number)
        {
            return val.GetDouble();
        }

        return null;
    }

    private static double GetMaxFromGridArray(JsonElement props, string key)
    {
        if (!props.TryGetProperty(key, out var layer) ||
            !layer.TryGetProperty("values", out var values) ||
            values.ValueKind != JsonValueKind.Array)
            return 0.0;

        double max = 0.0;
        foreach (var item in values.EnumerateArray())
        {
            if (item.TryGetProperty("value", out var v) &&
                v.ValueKind == JsonValueKind.Number)
            {
                double val = v.GetDouble();
                if (val > max)
                    max = val;
            }
        }

        return max;
    }

    //Todo:check function for functionality
    private static (double Inches, string WindowLabel) GetPrecipitationInInches(JsonElement props)
    {
        string[] propNames = ["precipitationLastHour", "precipitationLast3Hours"];
        string[] windowLabels = ["1 hr", "3 hrs"];

        for (int i = 0; i < propNames.Length; i++)
        {
            string propName = propNames[i];
            if(props.TryGetProperty(propName, out var prop) &&
               prop.TryGetProperty("value", out var val) &&
               val.ValueKind == JsonValueKind.Number)
            {
                double rawVal = val.GetDouble();
                string unitCode = prop.TryGetProperty("unitCode", out var unit) ? unit.GetString() ?? "" : "";

                double inches = unitCode switch
                {
                        "wmoUnit:m" => rawVal * 39.3701, //Meter to Inches
                        "wmoUnit:mm" => rawVal / 25.4,
                        _         => rawVal / 25.4            
                };
                return (inches, windowLabels[i]);
                                
            }
        }
        return (0.0, "None");
    }
}
