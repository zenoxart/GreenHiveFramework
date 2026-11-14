# =============================================================================
# Green Hive - workflow-service
# Build context: repository root (see Deployment/Docker/docker-compose.yml).
# Multi-stage: build with the .NET 10 SDK, run on the ASP.NET 10 runtime as non-root user, health check on /health/live.
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Shared/ Shared/
COPY Backend/GreenHive.NET.Backend.Core/ Backend/GreenHive.NET.Backend.Core/
COPY Backend/GreenHive.NET.Backend.WorkflowService/ Backend/GreenHive.NET.Backend.WorkflowService/
RUN dotnet publish "Backend/GreenHive.NET.Backend.WorkflowService/GreenHive.NET.Backend.WorkflowService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    GreenHive__ServiceName=workflow-service
EXPOSE 8080
USER $APP_UID

HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=5 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && head -n1 <&3 | grep -q ' 200 '"]

ENTRYPOINT ["dotnet", "GreenHive.NET.Backend.WorkflowService.dll"]
