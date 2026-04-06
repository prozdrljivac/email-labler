FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/EmailLabeler/EmailLabeler.csproj src/EmailLabeler/
RUN dotnet restore src/EmailLabeler/EmailLabeler.csproj
COPY src/ src/
RUN dotnet publish src/EmailLabeler/EmailLabeler.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "EmailLabeler.dll"]
