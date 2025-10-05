; ExConnector - Installeur Windows Simple
; Version minimaliste et fonctionnelle

#define AppName "ExConnector"
#define AppVersion "0.1.2"
#define AppPublisher "EX'SIE SARL"
#define AppExeName "ExConnector.exe"

[Setup]
AppId={{ExConnector-Sage100-Connector}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=ExConnector-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Dirs]
Name: "{commonappdata}\ExConnector"; Permissions: users-full

[Files]
; Application complète depuis publish
Source: "..\bin\Release\net9.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Scripts d'installation
Source: "install-hosting-bundle.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "configure-iis.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall
; Configuration initiale
Source: "..\exconnector.settings.json"; DestDir: "{commonappdata}\ExConnector"; Flags: onlyifdoesntexist

[Run]
; Étape 1 : Installer ASP.NET Core Hosting Bundle si nécessaire
Filename: "powershell.exe"; \
    Parameters: "-ExecutionPolicy Bypass -File ""{tmp}\install-hosting-bundle.ps1"" -Install"; \
    StatusMsg: "Installation ASP.NET Core Hosting Bundle (peut prendre 2-3 min)..."; \
    Flags: runhidden waituntilterminated

; Étape 2 : Configurer IIS
Filename: "powershell.exe"; \
    Parameters: "-ExecutionPolicy Bypass -File ""{tmp}\configure-iis.ps1"" -InstallPath ""{app}"" -Port 14330"; \
    StatusMsg: "Configuration de IIS..."; \
    Flags: runhidden waituntilterminated

; Étape 3 : Ouvrir le navigateur automatiquement
Filename: "http://localhost:14330/admin/connexion.html"; \
    Flags: shellexec skipifsilent nowait

[UninstallRun]
; Supprimer le site IIS
Filename: "powershell.exe"; \
    Parameters: "-ExecutionPolicy Bypass -Command ""Import-Module WebAdministration; Stop-Website -Name '{#AppName}' -ErrorAction SilentlyContinue; Stop-WebAppPool -Name '{#AppName}' -ErrorAction SilentlyContinue; Remove-Website -Name '{#AppName}' -ErrorAction SilentlyContinue; Remove-WebAppPool -Name '{#AppName}' -ErrorAction SilentlyContinue"""; \
    RunOnceId: "RemoveIIS"; \
    Flags: runhidden waituntilterminated

[UninstallDelete]
Type: files; Name: "{app}\web.config"
Type: filesandordirs; Name: "{commonappdata}\ExConnector"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsAdmin then
  begin
    MsgBox('Cette installation nécessite les droits administrateur.', mbError, MB_OK);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  SourceFile, DestFile: string;
begin
  if CurStep = ssPostInstall then
  begin
    SourceFile := ExpandConstant('{commonappdata}\ExConnector\exconnector.settings.json');
    DestFile := ExpandConstant('{app}\exconnector.settings.json');
    
    if FileExists(SourceFile) and not FileExists(DestFile) then
    begin
      CopyFile(SourceFile, DestFile, False);
    end;
  end;
end;

