FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Backend/Directory.Build.props Backend/global.json Backend/
WORKDIR /src/Backend
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY Backend/Services/Tenant/SRECommerce.Tenant.Domain/*.csproj Services/Tenant/SRECommerce.Tenant.Domain/
COPY Backend/Services/Tenant/SRECommerce.Tenant.Application/*.csproj Services/Tenant/SRECommerce.Tenant.Application/
COPY Backend/Services/Tenant/SRECommerce.Tenant.Infrastructure/*.csproj Services/Tenant/SRECommerce.Tenant.Infrastructure/
COPY Backend/Services/Tenant/SRECommerce.Tenant.API/*.csproj Services/Tenant/SRECommerce.Tenant.API/
# Keep dependency restore cached when only application source changes.
RUN dotnet restore Services/Tenant/SRECommerce.Tenant.API --nologo
COPY Backend/Services/Tenant/SRECommerce.Tenant.Domain/ Services/Tenant/SRECommerce.Tenant.Domain/
COPY Backend/Services/Tenant/SRECommerce.Tenant.Application/ Services/Tenant/SRECommerce.Tenant.Application/
COPY Backend/Services/Tenant/SRECommerce.Tenant.Infrastructure/ Services/Tenant/SRECommerce.Tenant.Infrastructure/
COPY Backend/Services/Tenant/SRECommerce.Tenant.API/ Services/Tenant/SRECommerce.Tenant.API/
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/ BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY Backend/BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/ BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
RUN dotnet publish Services/Tenant/SRECommerce.Tenant.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Tenant.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Tenant.API.dll"]
