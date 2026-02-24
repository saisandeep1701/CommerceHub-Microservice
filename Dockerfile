# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore
COPY src/CommerceHub.Api/CommerceHub.Api.csproj src/CommerceHub.Api/
RUN dotnet restore src/CommerceHub.Api/CommerceHub.Api.csproj

# Copy source and build
COPY src/ src/
RUN dotnet publish src/CommerceHub.Api/CommerceHub.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CommerceHub.Api.dll"]
