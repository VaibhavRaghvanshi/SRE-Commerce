FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Kernel/
COPY SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/*.csproj SRECommerce.BuildingBlocks/SRECommerce.BuildingBlocks.Contracts/
COPY SRECommerce.Identity.API/SRECommerce.Identity.Domain/*.csproj SRECommerce.Identity.API/SRECommerce.Identity.Domain/
COPY SRECommerce.Identity.API/SRECommerce.Identity.Application/*.csproj SRECommerce.Identity.API/SRECommerce.Identity.Application/
COPY SRECommerce.Identity.API/SRECommerce.Identity.Infrastructure/*.csproj SRECommerce.Identity.API/SRECommerce.Identity.Infrastructure/
COPY SRECommerce.Identity.API/SRECommerce.Identity.API/*.csproj SRECommerce.Identity.API/SRECommerce.Identity.API/
RUN dotnet restore SRECommerce.Identity.API/SRECommerce.Identity.API --nologo
COPY SRECommerce.BuildingBlocks/ SRECommerce.BuildingBlocks/
COPY SRECommerce.Identity.API/ SRECommerce.Identity.API/
RUN dotnet publish SRECommerce.Identity.API/SRECommerce.Identity.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /var/identity/keys /home/app/.dotnet/corefx/cryptography/x509stores && chown -R $APP_UID:$APP_UID /var/identity /home/app/.dotnet
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Identity__DataProtectionPath=/var/identity/keys
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=7s --start-period=20s --retries=3 CMD ["dotnet", "SRECommerce.Identity.API.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "SRECommerce.Identity.API.dll"]
