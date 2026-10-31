FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY SRECommerce.Tenant.API/SRECommerce.Tenant.Domain/*.csproj SRECommerce.Tenant.API/SRECommerce.Tenant.Domain/
COPY SRECommerce.Tenant.API/SRECommerce.Tenant.Application/*.csproj SRECommerce.Tenant.API/SRECommerce.Tenant.Application/
COPY SRECommerce.Tenant.API/SRECommerce.Tenant.Infrastructure/*.csproj SRECommerce.Tenant.API/SRECommerce.Tenant.Infrastructure/
COPY SRECommerce.Tenant.API/SRECommerce.Tenant.API/*.csproj SRECommerce.Tenant.API/SRECommerce.Tenant.API/
RUN dotnet restore SRECommerce.Tenant.API/SRECommerce.Tenant.API --nologo
COPY SRECommerce.BuildingBlocks/ SRECommerce.BuildingBlocks/
COPY SRECommerce.Tenant.API/ SRECommerce.Tenant.API/
RUN dotnet publish SRECommerce.Tenant.API/SRECommerce.Tenant.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Tenant.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Tenant.API.dll"]
