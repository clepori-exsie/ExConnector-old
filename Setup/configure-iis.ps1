# Configuration IIS pour ExConnector
# Version simple et fonctionnelle

param(
    [string]$InstallPath = "C:\Program Files\ExConnector",
    [int]$Port = 14330
)

$ErrorActionPreference = "Stop"

Write-Host "Configuration IIS pour ExConnector..." -ForegroundColor Cyan
Write-Host ""

# Installer IIS si nécessaire
$iisFeature = Get-WindowsOptionalFeature -Online -FeatureName "IIS-WebServer" -ErrorAction SilentlyContinue

if ($null -eq $iisFeature -or $iisFeature.State -ne "Enabled") {
    Write-Host "Installation de IIS..." -ForegroundColor Yellow
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer -All -NoRestart | Out-Null
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45 -All -NoRestart | Out-Null
}

Import-Module WebAdministration -ErrorAction Stop

# Supprimer l'ancien site s'il existe
$existingSite = Get-Website -Name "ExConnector" -ErrorAction SilentlyContinue
if ($existingSite) {
    Stop-Website -Name "ExConnector" -ErrorAction SilentlyContinue
    Remove-Website -Name "ExConnector" -ErrorAction SilentlyContinue
}

$existingPool = Get-WebAppPoolState -Name "ExConnector" -ErrorAction SilentlyContinue
if ($existingPool) {
    Stop-WebAppPool -Name "ExConnector" -ErrorAction SilentlyContinue
    Remove-WebAppPool -Name "ExConnector" -ErrorAction SilentlyContinue
}

# Créer le pool d'applications
New-WebAppPool -Name "ExConnector" | Out-Null
Set-ItemProperty -Path "IIS:\AppPools\ExConnector" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty -Path "IIS:\AppPools\ExConnector" -Name "startMode" -Value "AlwaysRunning"

# Créer web.config pour ASP.NET Core
$webConfigPath = Join-Path $InstallPath "web.config"
$webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath=".\ExConnector.exe" 
                  stdoutLogEnabled="false" 
                  stdoutLogFile=".\logs\stdout" 
                  hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
"@

Set-Content -Path $webConfigPath -Value $webConfig -Force

# Créer le site
New-Website -Name "ExConnector" `
    -PhysicalPath $InstallPath `
    -ApplicationPool "ExConnector" `
    -Port $Port | Out-Null

# Démarrer
Start-WebAppPool -Name "ExConnector"
Start-Website -Name "ExConnector"

Write-Host "IIS configure avec succes !" -ForegroundColor Green
Write-Host "Site accessible sur : http://localhost:$Port" -ForegroundColor Cyan

exit 0

