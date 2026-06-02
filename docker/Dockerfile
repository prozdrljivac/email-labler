FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/EmailLabeler/EmailLabeler.csproj src/EmailLabeler/
RUN dotnet restore src/EmailLabeler/EmailLabeler.csproj
COPY src/ src/
RUN dotnet publish src/EmailLabeler/EmailLabeler.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
# Liveness probe: hits the dependency-free /health/live so the container is marked
# unhealthy only when it stops serving HTTP, not when an upstream (Gmail) is down.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:5000/health/live || exit 1
ENTRYPOINT ["dotnet", "EmailLabeler.dll"]
