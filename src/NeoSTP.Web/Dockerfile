# Imagen de NeoSTP.Web (M8.1). Build context = raíz del repo.
#   docker build -f src/NeoSTP.Web/Dockerfile -t neostp-web .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/NeoSTP.Web/NeoSTP.Web.csproj
RUN dotnet publish src/NeoSTP.Web/NeoSTP.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NeoSTP.Web.dll"]
