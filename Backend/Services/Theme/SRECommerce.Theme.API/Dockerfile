FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Backend/Directory.Build.props Backend/global.json Backend/
WORKDIR /src/Backend
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/
COPY Backend/Services/Theme/SRECommerce.Theme.Domain/*.csproj Services/Theme/SRECommerce.Theme.Domain/
COPY Backend/Services/Theme/SRECommerce.Theme.Application/*.csproj Services/Theme/SRECommerce.Theme.Application/
COPY Backend/Services/Theme/SRECommerce.Theme.Infrastructure/*.csproj Services/Theme/SRECommerce.Theme.Infrastructure/
COPY Backend/Services/Theme/SRECommerce.Theme.API/*.csproj Services/Theme/SRECommerce.Theme.API/
RUN dotnet restore Services/Theme/SRECommerce.Theme.API --nologo
COPY Backend/BuildingBlocks/ BuildingBlocks/
COPY Backend/Services/Theme/ Services/Theme/
RUN dotnet publish Services/Theme/SRECommerce.Theme.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Theme.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Theme.API.dll"]
