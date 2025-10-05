# Script de compilation tout-en-un pour ExConnector MSI
# Auteur: Assistant AI
# Date: 2025-10-01

Write-Host ""
Write-Host "=== BUILD EXCONNECTOR MSI ===" -ForegroundColor Cyan
Write-Host ""

# Étape 1: Nettoyage
Write-Host "Étape 1/4: Nettoyage..." -ForegroundColor Yellow
Set-Location ..
dotnet clean | Out-Null
Remove-Item -Path "bin","obj" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  OK" -ForegroundColor Green

# Étape 2: Publication
Write-Host "Étape 2/4: Publication (self-contained win-x86)..." -ForegroundColor Yellow
dotnet publish -c Release -o publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERREUR lors de la publication" -ForegroundColor Red
    exit 1
}
Write-Host "  OK" -ForegroundColor Green

# Étape 3: Génération du WXS
Write-Host "Étape 3/4: Génération du Package-Full.wxs..." -ForegroundColor Yellow
Set-Location WixInstaller
.\Generate-WXS.ps1 | Out-Null
if (-not (Test-Path "Package-Full.wxs")) {
    Write-Host "  ERREUR: Package-Full.wxs non créé" -ForegroundColor Red
    exit 1
}
Write-Host "  OK" -ForegroundColor Green

# Étape 4: Compilation MSI
Write-Host "Étape 4/4: Compilation du MSI..." -ForegroundColor Yellow
wix build Package-Full.wxs -o ExConnector.msi
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERREUR lors de la compilation MSI" -ForegroundColor Red
    exit 1
}

# Vérification finale
if (Test-Path "ExConnector.msi") {
    $size = (Get-Item "ExConnector.msi").Length / 1MB
    Write-Host "  OK" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== SUCCES ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Fichier créé: WixInstaller\ExConnector.msi" -ForegroundColor Cyan
    Write-Host "  Taille: $([math]::Round($size, 2)) MB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Vous pouvez maintenant distribuer ce MSI à vos consultants !" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "  ERREUR: MSI non créé" -ForegroundColor Red
    exit 1
}

Set-Location ..

