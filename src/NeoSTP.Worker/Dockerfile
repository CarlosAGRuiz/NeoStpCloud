# Imagen de NeoSTP.Worker (M8.1). Build context = raíz del repo.
#   docker build -f src/NeoSTP.Worker/Dockerfile -t neostp-worker .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/NeoSTP.Worker/NeoSTP.Worker.csproj
RUN dotnet publish src/NeoSTP.Worker/NeoSTP.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NeoSTP.Worker.dll"]
