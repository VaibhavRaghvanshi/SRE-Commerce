FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src
COPY Backend/Directory.Build.props Backend/global.json Backend/
WORKDIR /src/Backend
COPY Backend/Services/Identity/SRECommerce.Identity.Domain/*.csproj Services/Identity/SRECommerce.Identity.Domain/
COPY Backend/Services/Identity/SRECommerce.Identity.Application/*.csproj Services/Identity/SRECommerce.Identity.Application/
COPY Backend/Services/Identity/SRECommerce.Identity.Infrastructure/*.csproj Services/Identity/SRECommerce.Identity.Infrastructure/
COPY Backend/Services/Identity/SRECommerce.Identity.API/*.csproj Services/Identity/SRECommerce.Identity.API/
RUN dotnet restore Services/Identity/SRECommerce.Identity.API --nologo
COPY Backend/Services/Identity/SRECommerce.Identity.Domain/ Services/Identity/SRECommerce.Identity.Domain/
COPY Backend/Services/Identity/SRECommerce.Identity.Application/ Services/Identity/SRECommerce.Identity.Application/
COPY Backend/Services/Identity/SRECommerce.Identity.Infrastructure/ Services/Identity/SRECommerce.Identity.Infrastructure/
COPY Backend/Services/Identity/SRECommerce.Identity.API/ Services/Identity/SRECommerce.Identity.API/
RUN dotnet publish Services/Identity/SRECommerce.Identity.API -c Release -o /app --no-restore --nologo -m:1 /p:UseSharedCompilation=false

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
