# Configuration IIS automatique pour ExConnector (mode silencieux)
# Exécuté automatiquement par l'installateur MSI

$ErrorActionPreference = "SilentlyContinue"
$LogFile = "$env:TEMP\ExConnector-IIS-Setup.log"

function Write-Log {
    param($Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $Message" | Out-File -FilePath $LogFile -Append
}

Write-Log "=== DEBUT CONFIGURATION IIS ==="

# Vérification des droits admin
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Log "ERREUR: Pas de droits administrateur"
    exit 1
}

Write-Log "Installation des fonctionnalites IIS..."

# Installation IIS + modules nécessaires
$features = @(
    "IIS-WebServerRole",
    "IIS-WebServer",
    "IIS-CommonHttpFeatures",
    "IIS-HttpErrors",
    "IIS-ApplicationDevelopment",
    "IIS-NetFxExtensibility45",
    "IIS-HealthAndDiagnostics",
    "IIS-HttpLogging",
    "IIS-Security",
    "IIS-RequestFiltering",
    "IIS-Performance",
    "IIS-WebServerManagementTools",
    "IIS-ManagementConsole",
    "IIS-HttpRedirect"
)

foreach ($feature in $features) {
    try {
        $result = Enable-WindowsOptionalFeature -Online -FeatureName $feature -NoRestart -All
        Write-Log "  Feature $feature : OK"
    } catch {
        Write-Log "  Feature $feature : Deja installe ou erreur"
    }
}

Write-Log "Installation URL Rewrite..."

# URL Rewrite Module
$urlRewriteUrl = "https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi"
$urlRewritePath = "$env:TEMP\urlrewrite.msi"

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $urlRewriteUrl -OutFile $urlRewritePath -UseBasicParsing -TimeoutSec 60
    Start-Process msiexec.exe -ArgumentList "/i `"$urlRewritePath`" /quiet /norestart" -Wait -NoNewWindow
    Remove-Item $urlRewritePath -Force -ErrorAction SilentlyContinue
    Write-Log "  URL Rewrite : OK"
} catch {
    Write-Log "  URL Rewrite : Deja installe ou erreur ($($_.Exception.Message))"
}

Write-Log "Installation ARR..."

# ARR (Application Request Routing)
$arrUrl = "https://download.microsoft.com/download/E/9/8/E9849D6A-020E-47E4-9FD0-A023E99B54EB/requestRouter_amd64.msi"
$arrPath = "$env:TEMP\arr.msi"

try {
    Invoke-WebRequest -Uri $arrUrl -OutFile $arrPath -UseBasicParsing -TimeoutSec 60
    Start-Process msiexec.exe -ArgumentList "/i `"$arrPath`" /quiet /norestart" -Wait -NoNewWindow
    Remove-Item $arrPath -Force -ErrorAction SilentlyContinue
    Write-Log "  ARR : OK"
} catch {
    Write-Log "  ARR : Deja installe ou erreur ($($_.Exception.Message))"
}

# Attendre que IIS soit complètement démarré
Start-Sleep -Seconds 5

Write-Log "Configuration du proxy ARR..."

# Activer le proxy ARR
try {
    Import-Module WebAdministration
    Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter "system.webServer/proxy" -name "enabled" -value "True"
    Write-Log "  Proxy ARR : OK"
} catch {
    Write-Log "  Proxy ARR : Erreur ($($_.Exception.Message))"
}

Write-Log "Creation du site IIS..."

# Variables
$siteName = "ExConnector"
$appPoolName = "ExConnectorPool"
$sitePort = 80
$sitePath = "C:\inetpub\ExConnectorProxy"

# Créer le dossier du site
try {
    if (-not (Test-Path $sitePath)) {
        New-Item -ItemType Directory -Path $sitePath -Force | Out-Null
        Write-Log "  Dossier site cree : $sitePath"
    }
} catch {
    Write-Log "  Erreur creation dossier : $($_.Exception.Message)"
}

# Créer l'Application Pool
try {
    if (Get-WebAppPoolState -Name $appPoolName -ErrorAction SilentlyContinue) {
        Remove-WebAppPool -Name $appPoolName
        Write-Log "  Application Pool existant supprime"
    }
    
    New-WebAppPool -Name $appPoolName -Force | Out-Null
    Set-ItemProperty IIS:\AppPools\$appPoolName -Name managedRuntimeVersion -Value ""
    Write-Log "  Application Pool cree : $appPoolName"
} catch {
    Write-Log "  Erreur Application Pool : $($_.Exception.Message)"
}

# Supprimer le site s'il existe
try {
    if (Get-Website -Name $siteName -ErrorAction SilentlyContinue) {
        Remove-Website -Name $siteName
        Write-Log "  Site existant supprime"
    }
} catch {
    Write-Log "  Erreur suppression site : $($_.Exception.Message)"
}

# Arrêter le Default Web Site s'il utilise le port 80
try {
    $defaultSite = Get-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
    if ($defaultSite -and $defaultSite.State -eq "Started") {
        $defaultBindings = $defaultSite.bindings.Collection | Where-Object { $_.bindingInformation -like "*:80:*" }
        if ($defaultBindings) {
            Stop-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
            Write-Log "  Default Web Site arrete (liberait le port 80)"
        }
    }
} catch {
    Write-Log "  Verification Default Web Site : $($_.Exception.Message)"
}

# Créer le site
try {
    New-Website -Name $siteName -PhysicalPath $sitePath -ApplicationPool $appPoolName -Port $sitePort -Force | Out-Null
    Write-Log "  Site IIS cree : $siteName (port $sitePort)"
} catch {
    Write-Log "  Erreur creation site : $($_.Exception.Message)"
    # Essayer avec une méthode alternative
    try {
        $site = Get-IISSite -Name $siteName -ErrorAction SilentlyContinue
        if (-not $site) {
            New-IISSite -Name $siteName -PhysicalPath $sitePath -BindingInformation "*:${sitePort}:" -Force
            Write-Log "  Site IIS cree (methode alternative)"
        }
    } catch {
        Write-Log "  Erreur methode alternative : $($_.Exception.Message)"
    }
}

Write-Log "Creation du web.config..."

# Créer le web.config de proxy
$webConfigContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="ReverseProxyToExConnector" stopProcessing="true">
          <match url="(.*)" />
          <action type="Rewrite" url="http://127.0.0.1:14330/{R:1}" />
          <serverVariables>
            <set name="HTTP_X_FORWARDED_PROTO" value="http" />
            <set name="HTTP_X_REAL_IP" value="{REMOTE_ADDR}" />
          </serverVariables>
        </rule>
      </rules>
    </rewrite>
    <httpProtocol>
      <customHeaders>
        <remove name="X-Powered-By" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
</configuration>
"@

try {
    $webConfigPath = Join-Path $sitePath "web.config"
    $webConfigContent | Out-File -FilePath $webConfigPath -Encoding UTF8 -Force
    Write-Log "  web.config cree : $webConfigPath"
} catch {
    Write-Log "  Erreur web.config : $($_.Exception.Message)"
}

Write-Log "Configuration des regles pare-feu..."

# Ouvrir les ports HTTP/HTTPS
try {
    New-NetFirewallRule -DisplayName "ExConnector HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow -ErrorAction SilentlyContinue | Out-Null
    Write-Log "  Pare-feu HTTP (80) : OK"
} catch {
    Write-Log "  Pare-feu HTTP : Regle deja existante"
}

try {
    New-NetFirewallRule -DisplayName "ExConnector HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow -ErrorAction SilentlyContinue | Out-Null
    Write-Log "  Pare-feu HTTPS (443) : OK"
} catch {
    Write-Log "  Pare-feu HTTPS : Regle deja existante"
}

Write-Log "=== CONFIGURATION IIS TERMINEE ==="
Write-Log "Log complet : $LogFile"

exit 0

