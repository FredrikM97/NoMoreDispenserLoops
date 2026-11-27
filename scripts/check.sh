#!/usr/bin/env bash
set -euo pipefail

dotnet format src/NoMoreDispenserLoops/NoMoreDispenserLoops.csproj --verify-no-changes
dotnet format scripts/OniBuildInfo/OniBuildInfo.csproj --verify-no-changes
dotnet format tests/NoMoreDispenserLoops.Tests/NoMoreDispenserLoops.Tests.csproj --verify-no-changes
dotnet test tests/NoMoreDispenserLoops.Tests/NoMoreDispenserLoops.Tests.csproj -c Release
