# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY Torrential.Core/Torrential.Core.csproj Torrential.Core/
COPY Torrential/Torrential.csproj Torrential/
COPY Torrential.Application/Torrential.Application.csproj Torrential.Application/
COPY Torrential.Api/Torrential.Api.csproj Torrential.Api/
RUN dotnet restore Torrential.Api/Torrential.Api.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish Torrential.Api/Torrential.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Torrential.Api.dll"]
