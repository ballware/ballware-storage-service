FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build

ARG GITHUB_PAT

RUN mkdir -p ~/.nuget && \
    echo '<configuration>\
            <packageSources>\
              <add key="github" value="https://nuget.pkg.github.com/ballware/index.json" />\
            </packageSources>\
            <packageSourceCredentials>\
              <github>\
                <add key="Username" value="frankball" />\
                <add key="ClearTextPassword" value="'$GITHUB_PAT'" />\
              </github>\
            </packageSourceCredentials>\
          </configuration>' > ~/.nuget/NuGet.Config

WORKDIR /src

COPY ./Ballware.Storage.Api ./Ballware.Storage.Api
COPY ./Ballware.Storage.Data ./Ballware.Storage.Data
COPY ./Ballware.Storage.Data.Ef ./Ballware.Storage.Data.Ef
COPY ./Ballware.Storage.Data.Ef.SqlServer ./Ballware.Storage.Data.Ef.SqlServer
COPY ./Ballware.Storage.Jobs ./Ballware.Storage.Jobs
COPY ./Ballware.Storage.Metadata ./Ballware.Storage.Metadata
COPY ./Ballware.Storage.Provider.Azure ./Ballware.Storage.Provider.Azure
COPY ./Ballware.Storage.Service ./Ballware.Storage.Service

WORKDIR "/src/Ballware.Storage.Service"
RUN dotnet restore "Ballware.Storage.Service.csproj"
RUN dotnet build "Ballware.Storage.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ballware.Storage.Service.csproj" -c Release -o /app/publish

FROM base AS final
USER app
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Ballware.Storage.Service.dll"]