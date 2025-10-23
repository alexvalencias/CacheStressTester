# =========================
# 1. Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything and restore dependencies
COPY . .
RUN dotnet restore "CacheStressTester.csproj"

# Build in Release mode
RUN dotnet publish "CacheStressTester.csproj" -c Release -o /app/publish /p:UseAppHost=false


# =========================
# 2. Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Copy build artifacts from previous stage
COPY --from=build /app/publish .

# Optional: Set non-root user (good practice)
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Set environment variable defaults (override via -e)
ENV STRESS_ENVIRONMENT="AWS"     STRESS_THREADS=50     STRESS_REQUESTSPERTHREAD=500     STRESS_DURATIONSECONDS=30     STRESS_AGGRESSIVEMODE=true     STRESS_PUBLISHMETRICS=false

# Default command
ENTRYPOINT ["dotnet", "CacheStressTester.dll"]
