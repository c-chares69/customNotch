; Installateur de customNotch (Inno Setup 6, application .NET auto-contenue), compilé par scripts\package.ps1 :
;   ISCC.exe /DAppVersion=1.3.0 "/DSourceDir=..\dist\customNotch" scripts\installer.iss
; Produit dist\customNotch-<version>-setup.exe : installation par utilisateur, sans droit
; administrateur, dans %LOCALAPPDATA%\Programs\customNotch. Menu Démarrer, entrée
; « Applications installées », tâches planifiées (démarrage de session + surveillance).
; Silencieux : /VERYSILENT /SUPPRESSMSGBOXES /NORESTART - c'est ce que fait la mise à jour.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\customNotch"
#endif
#define AppName "customNotch"
#define AppExe "customNotch.exe"
#define Publisher "DevPilot"

[Setup]
AppId={{4A7D2E61-9B3C-4F58-8E12-6C0B5A9D3F21}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/c-chares69/customNotch
AppSupportURL=https://github.com/c-chares69/customNotch
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=customNotch-{#AppVersion}-setup
SetupIconFile=..\assets\customnotch.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#Publisher}
VersionInfoDescription=Installateur de {#AppName}
VersionInfoProductName={#AppName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "autostart"; Description: "Lancer {#AppName} à l'ouverture de session (tâche planifiée, relancée en cas d'échec, surveillée toutes les 15 min)"; GroupDescription: "Démarrage :"
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "install_tasks.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "remove_tasks.ps1"; DestDir: "{app}"; Flags: ignoreversion
; L'icône à part : les raccourcis pointent dessus plutôt que sur l'exécutable, dont le
; cache d'icônes de Windows garde l'ancien dessin tant qu'Explorer n'est pas relancé.
Source: "..\assets\customnotch.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; AppUserModelID : sans lui, les notifications Windows affichent l'identifiant technique au lieu du nom.
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Comment: "Strip de statut et de lancement"; AppUserModelID: "DevPilot.CustomNotch"; IconFilename: "{app}\customnotch.ico"
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon; IconFilename: "{app}\customnotch.ico"

[Run]
; Tâches planifiées (ou leur retrait si l'utilisateur a décoché le démarrage automatique).
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\install_tasks.ps1"" -Exe ""{app}\{#AppExe}"""; Flags: runhidden waituntilterminated; Tasks: autostart; StatusMsg: "Enregistrement du démarrage automatique…"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\remove_tasks.ps1"" -KeepRegistry"; Flags: runhidden waituntilterminated; Tasks: not autostart
; Lancement : proposé à la fin de l'assistant, automatique en mode silencieux (mise à jour).
Filename: "{app}\{#AppExe}"; Description: "Lancer {#AppName}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExe}"; Flags: nowait skipifnotsilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\remove_tasks.ps1"""; Flags: runhidden waituntilterminated; RunOnceId: "tasks"

[InstallDelete]
; Le raccourci à la racine du menu Démarrer, posé par l'ancien scripts\publish.ps1 -Shortcut.
Type: files; Name: "{userprograms}\{#AppName}.lnk"

[Code]
// Le runtime .NET 10 Desktop : présent sur beaucoup de postes, sinon téléchargé chez
// Microsoft (≈ 55 Mo) et posé en silence. C'est ce qui garde l'application à quelques Mo.
const
  RuntimeUrl = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe';

function RuntimePresent(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  Found: TFindRec;
begin
  Result := False;
  // Le registre (vue 32 bits de l'installateur = WOW6432Node, là où dotnet s'inscrit)...
  if RegGetValueNames(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 3) = '10.' then
        Result := True;
  // ... puis le dossier partagé, au cas où l'inscription manque.
  if (not Result) and FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Found) then
  begin
    Result := True;
    FindClose(Found);
  end;
end;

var
  DownloadPage: TDownloadWizardPage;

procedure InitializeWizard();
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  R: Integer;
begin
  Result := True;
  if (CurPageID = wpReady) and (not RuntimePresent()) then
  begin
    DownloadPage.Clear;
    DownloadPage.Add(RuntimeUrl, 'windowsdesktop-runtime.exe', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        if not ShellExec('runas', ExpandConstant('{tmp}\windowsdesktop-runtime.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, R) then
          MsgBox('Le runtime .NET 10 Desktop n''a pas pu être installé (' + IntToStr(R) + '). '
                 + 'Installe-le depuis https://dotnet.microsoft.com puis relance {#AppName}.', mbInformation, MB_OK);
      except
        MsgBox('Le runtime .NET 10 Desktop n''a pas pu être téléchargé : ' + AddPeriod(GetExceptionMessage)
               + ' Installe-le depuis https://dotnet.microsoft.com puis relance {#AppName}.', mbInformation, MB_OK);
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

// L'application tourne en permanence : on l'arrête avant de remplacer ses fichiers.
procedure StopApp();
var
  R: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM "{#AppExe}" /F', '', SW_HIDE, ewWaitUntilTerminated, R);
  Sleep(800);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopApp();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Data: String;
begin
  if CurUninstallStep = usUninstall then
    StopApp();
  if CurUninstallStep = usPostUninstall then
  begin
    Data := ExpandConstant('{userappdata}\{#AppName}');
    if DirExists(Data) and (not UninstallSilent) then
      if MsgBox('Supprimer aussi les données (config.json, cells.json et sa surcharge locale, secrets.json chiffré, journaux) ?'
                + #13#10 + Data, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(Data, True, True, True);
  end;
end;
