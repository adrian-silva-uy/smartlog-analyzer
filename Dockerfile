# syntax=docker/dockerfile:1
# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SmartLogAnalyzer.slnx", "./"]
COPY ["src/SmartLogAnalyzer.Api/SmartLogAnalyzer.Api.csproj", "src/SmartLogAnalyzer.Api/"]
COPY ["src/SmartLogAnalyzer.Core/SmartLogAnalyzer.Core.csproj", "src/SmartLogAnalyzer.Core/"]
COPY ["src/SmartLogAnalyzer.Infrastructure/SmartLogAnalyzer.Infrastructure.csproj", "src/SmartLogAnalyzer.Infrastructure/"]
RUN dotnet restore "src/SmartLogAnalyzer.Api/SmartLogAnalyzer.Api.csproj"
COPY . .
WORKDIR "/src/src/SmartLogAnalyzer.Api"
RUN dotnet publish "SmartLogAnalyzer.Api.csproj" -c Release -o /app/publish --no-restore

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SmartLogAnalyzer.Api.dll"]