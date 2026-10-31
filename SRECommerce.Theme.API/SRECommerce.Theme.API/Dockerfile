FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Infrastructure/
COPY SRECommerce.Theme.API/SRECommerce.Theme.Domain/*.csproj SRECommerce.Theme.API/SRECommerce.Theme.Domain/
COPY SRECommerce.Theme.API/SRECommerce.Theme.Application/*.csproj SRECommerce.Theme.API/SRECommerce.Theme.Application/
COPY SRECommerce.Theme.API/SRECommerce.Theme.Infrastructure/*.csproj SRECommerce.Theme.API/SRECommerce.Theme.Infrastructure/
COPY SRECommerce.Theme.API/SRECommerce.Theme.API/*.csproj SRECommerce.Theme.API/SRECommerce.Theme.API/
RUN dotnet restore SRECommerce.Theme.API/SRECommerce.Theme.API --nologo
COPY SRECommerce.BuildingBlocks/ SRECommerce.BuildingBlocks/
COPY SRECommerce.Theme.API/ SRECommerce.Theme.API/
RUN dotnet publish SRECommerce.Theme.API/SRECommerce.Theme.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Theme.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Theme.API.dll"]
