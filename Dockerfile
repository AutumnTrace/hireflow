# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the dependency layer is cached between code changes.
COPY global.json Directory.Build.props HireFlow.slnx ./
COPY src/HireFlow.Domain/HireFlow.Domain.csproj src/HireFlow.Domain/
COPY src/HireFlow.Infrastructure/HireFlow.Infrastructure.csproj src/HireFlow.Infrastructure/
COPY src/HireFlow.Api/HireFlow.Api.csproj src/HireFlow.Api/
RUN dotnet restore src/HireFlow.Api/HireFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/HireFlow.Api/HireFlow.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "HireFlow.Api.dll"]
