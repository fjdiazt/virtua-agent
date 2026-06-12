FROM node:22-bookworm-slim AS ui-build
WORKDIR /src

COPY src/virtua-agent-ui/package*.json ./src/virtua-agent-ui/
RUN npm ci --prefix src/virtua-agent-ui

COPY assets ./assets
COPY src/virtua-agent-ui ./src/virtua-agent-ui
RUN npm run build --prefix src/virtua-agent-ui

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY src/virtua-agent-api/VirtuaAgent.Api/VirtuaAgent.Api.csproj src/virtua-agent-api/VirtuaAgent.Api/
RUN dotnet restore src/virtua-agent-api/VirtuaAgent.Api/VirtuaAgent.Api.csproj

COPY src/virtua-agent-api ./src/virtua-agent-api
COPY --from=ui-build /src/src/virtua-agent-api/VirtuaAgent.Api/wwwroot/ui ./src/virtua-agent-api/VirtuaAgent.Api/wwwroot/ui
WORKDIR /src/src/virtua-agent-api/VirtuaAgent.Api
RUN dotnet publish VirtuaAgent.Api.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=api-build /app/publish .

RUN mkdir -p /data
VOLUME ["/data"]

ENV ASPNETCORE_HTTP_PORTS=8080
ENV TraceStore__ConnectionString="Data Source=/data/virtua-agent.db"

ENTRYPOINT ["dotnet", "VirtuaAgent.Api.dll"]
