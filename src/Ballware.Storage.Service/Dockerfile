FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY ./Ballware.Storage.Azure ./Ballware.Storage.Azure
COPY ./Ballware.Storage.Provider ./Ballware.Storage.Provider
COPY ./Ballware.Storage.Service ./Ballware.Storage.Service

WORKDIR "/src/Ballware.Storage.Service"
RUN dotnet build "Ballware.Storage.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ballware.Storage.Service.csproj" -c Release -o /app/publish

FROM base AS final
USER app
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Ballware.Storage.Service.dll"]