# ShopPulse — démarrage de l'environnement de développement (Windows)
#
#   powershell -ExecutionPolicy Bypass -File scripts/dev.ps1
#
# 1. Démarre les conteneurs (PostgreSQL, Keycloak, Mailpit)
# 2. Détecte le port API : 5299, ou 5298 si 5299 est occupé (processus zombie)
# 3. Construit en Release (le dossier Debug peut rester verrouillé par d'anciens
#    processus jusqu'au prochain redémarrage de Windows)
# 4. Ouvre deux fenêtres PowerShell : une pour l'API, une pour le Web
#
# Les serveurs tournent dans VOS fenêtres : fermez la fenêtre = arrêtez le serveur.

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host "1/4 Conteneurs (db, keycloak, mailpit)..." -ForegroundColor Cyan
docker compose -f "$root/docker-compose.yml" up -d db mailpit keycloak

$apiPort = 5299
if (Get-NetTCPConnection -LocalPort 5299 -State Listen -ErrorAction SilentlyContinue) {
    $apiPort = 5298
    Write-Host "      Port 5299 occupé (ancien processus ?) → API sur 5298." -ForegroundColor Yellow
}

Write-Host "2/4 Build Release..." -ForegroundColor Cyan
dotnet build "$root/src/ShopPulse.Api/ShopPulse.Api.csproj" -c Release --nologo -v q
dotnet build "$root/src/ShopPulse.Web/ShopPulse.Web.csproj" -c Release --nologo -v q

Write-Host "3/4 Fenêtre API (http://localhost:$apiPort)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "Set-Location '$root/src/ShopPulse.Api'; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run -c Release --no-build --no-launch-profile --urls http://localhost:$apiPort"
)

Write-Host "4/4 Fenêtre Web (http://localhost:5026)..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    '-NoExit', '-Command',
    "Set-Location '$root/src/ShopPulse.Web'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:Api__BaseUrl='http://localhost:$apiPort/'; dotnet run -c Release --no-build --no-launch-profile --urls http://localhost:5026"
)

Write-Host ""
Write-Host "Boutique : http://localhost:5026" -ForegroundColor Green
Write-Host "API      : http://localhost:$apiPort"
Write-Host "Mailpit  : http://localhost:8025   Keycloak : http://localhost:8080"
Write-Host "Comptes  : demo/demo1234 (back-office) — client/client1234"
