# PlannerPro API image — pure .NET 10 API (the front end ships as plannerproweb).
# BUILD CONTEXT MUST BE THE REPO ROOT (the API references PlannerPro.ServiceDefaults):
#   docker build -t plannerproapi -f PlannerPro.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# ICU for globalization (dates / SQL client) + a non-root runtime user.
RUN apk add --no-cache icu-libs icu-data-full && adduser -D -u 1000 appuser

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Restore against just the project files first for better layer caching.
COPY PlannerPro.Api/PlannerPro.Api.csproj PlannerPro.Api/
COPY PlannerPro.ServiceDefaults/PlannerPro.ServiceDefaults.csproj PlannerPro.ServiceDefaults/
RUN dotnet restore PlannerPro.Api/PlannerPro.Api.csproj
COPY PlannerPro.Api/ PlannerPro.Api/
COPY PlannerPro.ServiceDefaults/ PlannerPro.ServiceDefaults/
# SkipSpaBuild=true → do NOT run npm/Angular here; keep this a lean API image.
RUN dotnet publish PlannerPro.Api/PlannerPro.Api.csproj \
    -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false /p:SkipSpaBuild=true

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "PlannerPro.Api.dll"]
