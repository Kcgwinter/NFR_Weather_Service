# NFR Weather Service

A Blazor Web App that provides current weather information for any given address.

## Overview

The NFR Weather Service is a demo web application built with Blazor that lets users search for weather on a specific address. 
Once an address is submitted, the app:

1. Converts the address to geographic coordinates (lat/long) via the https://geocoding.geo.census.gov/.
2. Uses the resulting coordinates with the NOAA Weather API to find the closest weather station and forecast grid.
3. Retrieves the latest weather observation from that station (temperature, precipitation, and freezing-precipitation status).
4. Fetches accumulated snow and ice data from the NOAA forecast grid.

All of this is rendered client-side through an interactive Blazor Server UI.

## Features

- Address-based weather search
- Current temperature (F)
- Rainfall over the last hour (in)
- Snow accumulation (in)
- Ice accumulation (in)
- Freezing rain / frozen precipitation indicator

## Tech Stack

- **.NET 10** (ASP.NET Core Web SDK, `net10.0`)
- **Blazor Web App**
- **Bootstrap 5** for styling
- **NOAA Weather API** https://api.weather.gov
- **Census Geocoding API** https://geocoding.geo.census.gov

## Getting Started
### Prerequisites

- .NET 10 SDK

### Run the app
- dotnet build
- dotnet run

Or run the solution NFR_Weather_Service.sln

## Project Structure

```
NFR_Weather_Service/
├── NFR_Weather_Service/
│   ├── Components/
│   │   ├── Layout/            # MainLayout, NavMenu, ReconnectModal, styles
│   │   ├── Pages/             # Error, NotFound
│   │   ├── Weather/           # Weather page + WeatherReportCard
│   │   ├── App.razor         # Root component
│   │   └── Routes.razor      # Router
│   ├── Models/                # Coordinates, WeatherReport
│   ├── Services/              # WeatherService
│   ├── Properties/            # launchSettings.json
│   ├── wwwroot/               # app.css, favicon, static assets (Bootstrap)
│   ├── appsettings.json
│   ├── Program.cs             # App entry point & DI setup
│   └── NFR_Weather_Service.csproj
├── NFR_Weather_Service.sln
└── .gitignore
```

## Usage

1. Navigate to the weather page (`/` or `/weather`).
2. Enter an address into the search input.
3. Click the **Search** button.
4. The current weather report for the address is displayed, showing:
   - Temperature, rain, snow, ice totals.
   - An indicator for active freezing precipitation.

## Notes & Limitations

- NOAA returns nearby stations ordered by distance; if the closest station is offline, the service automatically falls back to the next nearest station that is reporting.
- Freezing‑rain detection uses simple text‑matching – it's not a fully comprehensive algorithm.
- HTTPS redirection is disabled for local development


## API References

- [NOAA Weather API](https://www.weather.gov/documentation/services-web-api)
- [US Census Geocoding API](https://www.census.gov/geographies/reference-files/time-series/geo/geocoding.html)

## Future Improvement
* Address search with example searches (dropdown of possible matching addresses)
* Optimization of search and text matching search
* Extend basic testing
