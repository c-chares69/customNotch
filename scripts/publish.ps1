# Publie customNotch en un exécutable unique : dist\customNotch\customNotch.exe (dépend du runtime .NET 10 Desktop,
# déjà présent avec le SDK ; sinon : winget install Microsoft.DotNet.DesktopRuntime.10).
# Utilisation :  powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 [-Shortcut] [-Autostart] [-Run]
#   -Shortcut  : crée « customNotch » dans le menu Démarrer (raccourci vers l'exe publié)
#   -Autostart : lance customNotch à l'ouverture de session (clé Run de l'utilisateur ; l'installateur du plan 2
#                la remplacera par une tâche planifiée avec surveillance)
#   -Run       : lance l'exe publié à la fin (en arrêtant l'instance en cours)
# L'installateur Inno Setup (désinstallation propre, mises à jour) arrive avec le plan 2.
param(
    [switch]$Shortcut,
    [switch]$Autostart,
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $projectDir
$out = Join-Path $projectDir "dist\customNotch"
$exe = Join-Path $out "customNotch.exe"

# Une instance qui tourne verrouille l'exe : on l'arrête avant de le remplacer.
Get-Process customNotch -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $projectDir "src\CustomNotch.App\CustomNotch.App.csproj") -c Release -o $out --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "publication en echec (dotnet publish $LASTEXITCODE)" }
Get-ChildItem $out -Filter "*.pdb" | Remove-Item -Force
if (-not (Test-Path $exe)) { throw "$exe introuvable apres publication" }
$version = (Get-Item $exe).VersionInfo.ProductVersion
if ($version -match '^(\d+\.\d+\.\d+)') { $version = $Matches[1] }
$size = [math]::Round((Get-ChildItem $out -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Publie : $exe ($size MB) - version $version"

if ($Shortcut) {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath("Programs")) "customNotch.lnk"))
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $out
    $lnk.Description = "customNotch - strip de statut et de lancement"
    $lnk.Save()
    Write-Host "Raccourci : menu Demarrer > customNotch"
}

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if ($Autostart) {
    New-ItemProperty -Path $runKey -Name "customNotch" -Value "`"$exe`"" -PropertyType String -Force | Out-Null
    Write-Host "Demarrage automatique : active (cle Run)"
}

if ($Run) {
    Start-Process $exe
    Write-Host "Lance : $exe"
}
