FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/
COPY SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.Domain/*.csproj SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.Domain/
COPY SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.Application/*.csproj SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.Application/
COPY SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.Infrastructure/*.csproj SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.Infrastructure/
COPY SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.API/*.csproj SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.API/
RUN dotnet restore SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.API --nologo
COPY SRECommerce.BuildingBlocks/ SRECommerce.BuildingBlocks/
COPY SRECommerce.Manufacturing.API/ SRECommerce.Manufacturing.API/
RUN dotnet publish SRECommerce.Manufacturing.API/SRECommerce.Manufacturing.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Manufacturing.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Manufacturing.API.dll"]
