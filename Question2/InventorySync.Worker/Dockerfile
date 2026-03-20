# ──────────────────────────────────────────────────────────────
# Multi-stage build for minimal container image.
#
# Stage 1 (build): Full SDK for restore + publish
# Stage 2 (runtime): Slim ASP.NET runtime image only
#
# This produces a ~80MB image vs ~800MB if using the SDK image.
# ──────────────────────────────────────────────────────────────

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for layer-cached restore
COPY InventorySync.Worker.csproj .
RUN dotnet restore

# Copy everything else and publish
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage — no SDK, no source code
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as non-root for security
USER $APP_UID

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "InventorySync.Worker.dll"]
