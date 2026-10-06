# Development

## Local verification

Run these commands from the repository root:

dotnet restore Musiq.sln

dotnet build Musiq.sln --configuration Release

dotnet test Musiq.sln --configuration Release --no-build

The same build/test path runs on GitHub Actions using Windows and .NET 10.

## Dependency rule

Domain remains independent from infrastructure and UI. Application owns abstractions. Infrastructure owns SQLite and file-system implementation details. Presentation never opens the database directly.

## Phase rule

Do not move to the next phase while the current phase does not build and test successfully.
