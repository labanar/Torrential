#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0.3-jammy-chiseled-extra AS base
USER app
WORKDIR /app
EXPOSE 8080 
EXPOSE 53123/tcp

# Ensure the directory exists and set correct permissions
FROM ubuntu:jammy AS setup
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN mkdir -p /app/data && chmod -R 777 /app/data

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:8.0.203-jammy AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
WORKDIR /src
COPY ["Torrential.Web/Torrential.Web.csproj", "Torrential.Web/"]
COPY ["Torrential.Extensions.SignalR/Torrential.Extensions.SignalR.csproj", "Torrential.Extensions.SignalR/"]
COPY ["Torrential/Torrential.csproj", "Torrential/"]
RUN dotnet restore -a $TARGETARCH "./Torrential.Web/Torrential.Web.csproj"
COPY . .
WORKDIR "/src/Torrential.Web"
RUN dotnet build "./Torrential.Web.csproj" -a $TARGETARCH -c $BUILD_CONFIGURATION -o /app/build

FROM node:latest as nextjs-build
WORKDIR /app
COPY ["torrential-ui/package.json", "torrential-ui/package-lock.json*", "./"]
RUN npm install
COPY torrential-ui/ .
RUN npm run build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
RUN dotnet publish "./Torrential.Web.csproj" -c $BUILD_CONFIGURATION -a $TARGETARCH -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=nextjs-build /app/out ./wwwroot
ENV APP_DATA_PATH="/app/data"
ENV DOTNET_gcServer="0"
ENTRYPOINT ["dotnet", "Torrential.Web.dll"]