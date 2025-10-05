# Installation du ASP.NET Core Hosting Bundle
# Nécessaire pour faire tourner ASP.NET Core dans IIS

param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"

Write-Host "Verification ASP.NET Core Hosting Bundle..." -ForegroundColor Cyan
Write-Host ""

# Vérifier si le Hosting Bundle est installé
$hostingBundleInstalled = $false

# Vérifier via le registre
$registryPaths = @(
    "HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost",
    "HKLM:\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedhost"
)

foreach ($path in $registryPaths) {
    if (Test-Path $path) {
        $version = (Get-ItemProperty -Path $path -ErrorAction SilentlyContinue).Version
        if ($version -like "9.*") {
            $hostingBundleInstalled = $true
            Write-Host "ASP.NET Core Hosting Bundle 9.x detecte : $version" -ForegroundColor Green
            break
        }
    }
}

# Vérifier via aspnetcore.dll
$aspNetCoreDll = "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
if (Test-Path $aspNetCoreDll) {
    $hostingBundleInstalled = $true
    Write-Host "ASP.NET Core Module V2 detecte" -ForegroundColor Green
}

if ($hostingBundleInstalled) {
    Write-Host ""
    Write-Host "ASP.NET Core Hosting Bundle est deja installe." -ForegroundColor Green
    exit 0
}

if (!$Install) {
    Write-Host ""
    Write-Host "ASP.NET Core Hosting Bundle NON installe !" -ForegroundColor Red
    Write-Host "Installation requise pour faire tourner l'application dans IIS." -ForegroundColor Yellow
    exit 1
}

# Télécharger et installer le Hosting Bundle
Write-Host ""
Write-Host "Telechargement du ASP.NET Core 9.0 Hosting Bundle..." -ForegroundColor Yellow

$downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/5e8bc3eb-86f9-4df2-86ee-5c5c11e92c4a/8484c475f1b1a0933fb333cb43bd5467/dotnet-hosting-9.0.0-win.exe"
$installerPath = "$env:TEMP\dotnet-hosting-9.0.0-win.exe"

try {
    # Télécharger
    Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath -UseBasicParsing
    
    Write-Host "Installation en cours (cela peut prendre 2-3 minutes)..." -ForegroundColor Yellow
    
    # Installer silencieusement
    $process = Start-Process -FilePath $installerPath -ArgumentList "/quiet", "/norestart" -Wait -PassThru
    
    if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
        Write-Host ""
        Write-Host "ASP.NET Core Hosting Bundle installe avec succes !" -ForegroundColor Green
        
        # Redémarrer IIS pour charger le module
        Write-Host "Redemarrage de IIS..." -ForegroundColor Yellow
        iisreset /restart | Out-Null
        
        Write-Host "Installation terminee !" -ForegroundColor Green
        exit 0
    } else {
        Write-Host ""
        Write-Host "Erreur lors de l'installation (Code: $($process.ExitCode))" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host ""
    Write-Host "Erreur : $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    # Nettoyer
    if (Test-Path $installerPath) {
        Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
    }
}

