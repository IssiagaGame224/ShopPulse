# ---- Étape 1 : build (SDK) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauration par couches : les fichiers de solution d'abord
COPY Directory.Build.props Directory.Packages.props ShopPulse.slnx ./
COPY src/ShopPulse.Domain/ShopPulse.Domain.csproj src/ShopPulse.Domain/
COPY src/ShopPulse.SharedKernel/ShopPulse.SharedKernel.csproj src/ShopPulse.SharedKernel/
COPY src/ShopPulse.Application/ShopPulse.Application.csproj src/ShopPulse.Application/
COPY src/ShopPulse.Infrastructure/ShopPulse.Infrastructure.csproj src/ShopPulse.Infrastructure/
COPY src/ShopPulse.Contracts/ShopPulse.Contracts.csproj src/ShopPulse.Contracts/
COPY src/ShopPulse.Api/ShopPulse.Api.csproj src/ShopPulse.Api/
RUN dotnet restore src/ShopPulse.Api/ShopPulse.Api.csproj

# Puis tout le code
COPY src/ src/
RUN dotnet publish src/ShopPulse.Api/ShopPulse.Api.csproj -c Release -o /app

# ---- Étape 2 : runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# ICU requis par les cultures fr/en/ar (module 8, concept Formats) :
# l'image runtime l'inclut ; ne JAMAIS activer InvariantGlobalization en production ici.
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "ShopPulse.Api.dll"]
