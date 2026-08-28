FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/WaterFlow.Api/WaterFlow.Api.csproj", "WaterFlow.Api/"]
RUN dotnet restore "WaterFlow.Api/WaterFlow.Api.csproj"

COPY src/ .
WORKDIR /src/WaterFlow.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WaterFlow.Api.dll"]
