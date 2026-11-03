FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/
COPY SRECommerce.Discovery.API/SRECommerce.Discovery.Domain/*.csproj SRECommerce.Discovery.API/SRECommerce.Discovery.Domain/
COPY SRECommerce.Discovery.API/SRECommerce.Discovery.Application/*.csproj SRECommerce.Discovery.API/SRECommerce.Discovery.Application/
COPY SRECommerce.Discovery.API/SRECommerce.Discovery.Infrastructure/*.csproj SRECommerce.Discovery.API/SRECommerce.Discovery.Infrastructure/
COPY SRECommerce.Discovery.API/SRECommerce.Discovery.API/*.csproj SRECommerce.Discovery.API/SRECommerce.Discovery.API/
RUN dotnet restore SRECommerce.Discovery.API/SRECommerce.Discovery.API --nologo
COPY SRECommerce.BuildingBlocks/ SRECommerce.BuildingBlocks/
COPY SRECommerce.Discovery.API/ SRECommerce.Discovery.API/
RUN dotnet publish SRECommerce.Discovery.API/SRECommerce.Discovery.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Discovery.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Discovery.API.dll"]
