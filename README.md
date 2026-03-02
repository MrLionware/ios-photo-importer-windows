# iOS Photo Importer (Windows USB)

A WinUI 3 desktop app that imports iPhone photos/videos to local storage on Windows 11.

## Implemented v1 behavior

- Manual USB import only.
- New-items-only import tracking with SQLite.
- Single destination folder import.
- HEIC kept as original.
- Live Photo motion component skipped.
- Filename collision policy: skip incoming file.
- Resume support using persisted import job state.
- Local file logging only.

## Solution layout

- `src/IosPhotoImporter.App` - WinUI 3 app and 4-screen flow.
- `src/IosPhotoImporter.Core` - domain contracts, policies, import orchestration.
- `src/IosPhotoImporter.Infrastructure` - SQLite repository and WPD service adapters.
- `tests/IosPhotoImporter.Core.Tests` - unit tests for policies/import service.
- `tests/IosPhotoImporter.Infrastructure.Tests` - SQLite and mocked-WPD integration tests.

## Runtime prerequisites

- Windows 11.
- .NET 8 SDK.
- Apple Mobile Device Support (from Apple Devices app or iTunes).

## Build and test (Windows)

```powershell
dotnet restore IosPhotoImporter.sln
dotnet test IosPhotoImporter.sln
dotnet build src\IosPhotoImporter.App\IosPhotoImporter.App.csproj -c Release
```

## MSIX packaging (Windows)

```powershell
dotnet publish src\IosPhotoImporter.App\IosPhotoImporter.App.csproj -c Release -r win-x64 -p:PublishProfile=win-x64.pubxml
```

Output package artifacts are generated under `src/IosPhotoImporter.App/bin/x64/Release/AppPackages/`.

## Notes on USB transport

The infrastructure includes a transport seam (`IWpdTransport`) with service adapters wired for device/media operations. The default transport in this repo is `UnsupportedWpdTransport`; replace it with a concrete Windows WPD implementation in the app composition root for hardware access.
