# Antennas Calculator (WPF, .NET 8, Mapsui 5)

## Build & Run
1. .NET 8 SDK
2. Restore & run:
   ```bash
   dotnet restore
   dotnet build
   dotnet run --project <path-to-wpf-project>
   ```

## NuGet (WPF)
- Mapsui.Wpf 5.0.0-beta.20
- Mapsui.Nts 5.0.0-beta.20
- NetTopologySuite 2.6.0

## Notes
- Для OSM потрібен валідний User-Agent при створенні tile-layer.
- Демо-код поки без DEM; гачки додані (IDemProvider).

## Next
- DEM.Net/GDAL інтеграція
- UI з двома панелями (верх — профіль траси/зона Френеля, низ — параметри лінка)