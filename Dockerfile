# syntax=docker/dockerfile:1
# ---------------------------
# Etapa 1: Build
# ---------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia solo los archivos de proyecto primero para cachear la restauración de NuGet.
COPY ["SmartLogAnalyzer.slnx", "./"]
COPY ["src/SmartLogAnalyzer.Api/SmartLogAnalyzer.Api.csproj", "src/SmartLogAnalyzer.Api/"]
COPY ["src/SmartLogAnalyzer.Core/SmartLogAnalyzer.Core.csproj", "src/SmartLogAnalyzer.Core/"]
COPY ["src/SmartLogAnalyzer.Infrastructure/SmartLogAnalyzer.Infrastructure.csproj", "src/SmartLogAnalyzer.Infrastructure/"]

RUN dotnet restore "SmartLogAnalyzer.slnx"

# Copia el resto del código y publica la API.
COPY . .
WORKDIR "/src/src/SmartLogAnalyzer.Api"
RUN dotnet publish "SmartLogAnalyzer.Api.csproj" -c Release -o /app/publish --no-restore

# ---------------------------
# Etapa 2: Runtime
# ---------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

# La app escucha en el puerto 8080 (configurado vía ASPNETCORE_URLS en docker-compose).
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SmartLogAnalyzer.Api.dll"]
