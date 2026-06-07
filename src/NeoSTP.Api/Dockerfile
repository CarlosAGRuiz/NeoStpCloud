# Imagen de NeoSTP.Api (M8.1). Build context = raíz del repo.
#   docker build -f src/NeoSTP.Api/Dockerfile -t neostp-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/NeoSTP.Api/NeoSTP.Api.csproj
RUN dotnet publish src/NeoSTP.Api/NeoSTP.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NeoSTP.Api.dll"]
