FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /source

COPY Directory.Build.props global.json ./
COPY tools/CfoAgent.FinanceMcpServer/CfoAgent.FinanceMcpServer.csproj tools/CfoAgent.FinanceMcpServer/
RUN dotnet restore tools/CfoAgent.FinanceMcpServer/CfoAgent.FinanceMcpServer.csproj

COPY tools/CfoAgent.FinanceMcpServer/ tools/CfoAgent.FinanceMcpServer/
RUN dotnet publish tools/CfoAgent.FinanceMcpServer/CfoAgent.FinanceMcpServer.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=6 \
    CMD curl --fail --silent --show-error http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "CfoAgent.FinanceMcpServer.dll"]

