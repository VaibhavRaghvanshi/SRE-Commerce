# SRECommerce Storefront API

Open SRECommerce.Storefront.API.sln in this folder. Its five projects contain Controllers, Application DTOs/Interfaces, Domain types, Infrastructure implementations and Tests.

Build from this folder: ..\artifacts\dotnet10\dotnet.exe build SRECommerce.Storefront.API.sln -c Release -m:1 -nr:false

Existing SQL database SRECommerce_StorefrontDB and user-secrets IDs are preserved. Startup does not migrate or recreate databases. Shared contracts are in ../SRECommerce.BuildingBlocks. See the main README for current status.
