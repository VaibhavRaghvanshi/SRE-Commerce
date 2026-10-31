FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Backend/Directory.Build.props Backend/global.json Backend/
WORKDIR /src/Backend
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/
COPY Backend/Services/Storefront/SRECommerce.Storefront.Domain/*.csproj Services/Storefront/SRECommerce.Storefront.Domain/
COPY Backend/Services/Storefront/SRECommerce.Storefront.Application/*.csproj Services/Storefront/SRECommerce.Storefront.Application/
COPY Backend/Services/Storefront/SRECommerce.Storefront.Infrastructure/*.csproj Services/Storefront/SRECommerce.Storefront.Infrastructure/
COPY Backend/Services/Storefront/SRECommerce.Storefront.API/*.csproj Services/Storefront/SRECommerce.Storefront.API/
RUN dotnet restore Services/Storefront/SRECommerce.Storefront.API --nologo
COPY Backend/BuildingBlocks/ BuildingBlocks/
COPY Backend/Services/Storefront/ Services/Storefront/
RUN dotnet publish Services/Storefront/SRECommerce.Storefront.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Storefront.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Storefront.API.dll"]
