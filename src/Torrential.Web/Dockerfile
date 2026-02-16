# ---------- 1. Build the Vite frontend ----------
FROM node:20-slim AS frontend
WORKDIR /app
COPY src/torrential-ui-vite/package.json src/torrential-ui-vite/package-lock.json ./
RUN npm ci
COPY src/torrential-ui-vite/ .
ENV VITE_API_BASE_URL=""
RUN npm run build -- --outDir /app/wwwroot

# ---------- 2. Build the .NET backend ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Torrential.Web/Torrential.Web.csproj", "src/Torrential.Web/"]
COPY ["src/Torrential.Extensions.SignalR/Torrential.Extensions.SignalR.csproj", "src/Torrential.Extensions.SignalR/"]
COPY ["src/Torrential/Torrential.csproj", "src/Torrential/"]
RUN dotnet restore "./src/Torrential.Web/Torrential.Web.csproj"
COPY . .
# Copy pre-built frontend into wwwroot so the MSBuild ViteBuild target is skipped
COPY --from=frontend /app/wwwroot /src/src/Torrential.Web/wwwroot
WORKDIR "/src/src/Torrential.Web"
RUN dotnet publish "./Torrential.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:SKIP_VITE_BUILD=true

# ---------- 3. Final runtime image ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
RUN mkdir -p /app/data
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Torrential.Web.dll"]