FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY SRECommerce.ApiGateway/SRECommerce.ApiGateway/*.csproj SRECommerce.ApiGateway/SRECommerce.ApiGateway/
RUN dotnet restore SRECommerce.ApiGateway/SRECommerce.ApiGateway --nologo
COPY SRECommerce.BuildingBlocks/ SRECommerce.BuildingBlocks/
COPY SRECommerce.ApiGateway/ SRECommerce.ApiGateway/
RUN dotnet publish SRECommerce.ApiGateway/SRECommerce.ApiGateway -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.ApiGateway.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.ApiGateway.dll"]
