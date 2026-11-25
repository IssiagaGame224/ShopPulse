# ---- Étape 1 : build (SDK) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ShopPulse.slnx ./
COPY src/ShopPulse.Domain/ShopPulse.Domain.csproj src/ShopPulse.Domain/
COPY src/ShopPulse.SharedKernel/ShopPulse.SharedKernel.csproj src/ShopPulse.SharedKernel/
COPY src/ShopPulse.Application/ShopPulse.Application.csproj src/ShopPulse.Application/
COPY src/ShopPulse.Infrastructure/ShopPulse.Infrastructure.csproj src/ShopPulse.Infrastructure/
COPY src/ShopPulse.Modularity/ShopPulse.Modularity.csproj src/ShopPulse.Modularity/
COPY src/ShopPulse.UI/ShopPulse.UI.csproj src/ShopPulse.UI/
COPY src/ShopPulse.Features.Catalog/ShopPulse.Features.Catalog.csproj src/ShopPulse.Features.Catalog/
COPY src/ShopPulse.Features.Cart/ShopPulse.Features.Cart.csproj src/ShopPulse.Features.Cart/
COPY src/ShopPulse.Features.Checkout/ShopPulse.Features.Checkout.csproj src/ShopPulse.Features.Checkout/
COPY src/ShopPulse.Web/ShopPulse.Web.csproj src/ShopPulse.Web/
RUN dotnet restore src/ShopPulse.Web/ShopPulse.Web.csproj

COPY src/ src/
RUN dotnet publish src/ShopPulse.Web/ShopPulse.Web.csproj -c Release -o /app

# ---- Étape 2 : runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "ShopPulse.Web.dll"]
