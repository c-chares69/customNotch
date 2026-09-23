# Désinstalle customNotch (utilisateur courant) : application, tâches planifiées, raccourcis,
# entrée « Applications installées », fichiers du programme. Les données (%APPDATA%\customNotch :
# config.json, cells.json et sa surcharge locale, secrets.json chiffré, journaux) sont conservées sauf -RemoveData.
# Utilisation :  powershell -ExecutionPolicy Bypass -File uninstall.ps1 [-RemoveData] [-Quiet]
param(
    [switch]$RemoveData,
    [switch]$Quiet
)

$ErrorActionPreference = "Continue"
$app = "customNotch"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$app"

if (-not $Quiet) {
    $answer = Read-Host "Desinstaller $app ? (o/N)"
    if ($answer -notmatch '^[oOyY]') { Write-Host "Annule."; exit 0 }
}

Get-Process -Name $app -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Milliseconds 800

foreach ($task in @($app, "$app Watchdog")) {
    Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
}
$programs = [Environment]::GetFolderPath("Programs")
Remove-Item (Join-Path $programs "$app.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath("Startup")) "$app.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath("Desktop")) "$app.lnk") -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name $app -ErrorAction SilentlyContinue
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CustomNotch" -Recurse -Force -ErrorAction SilentlyContinue

if ($RemoveData) {
    Remove-Item (Join-Path $env:APPDATA $app) -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Donnees supprimees."
} else {
    Write-Host "Donnees conservees dans $env:APPDATA\$app (config.json, cells.json et sa surcharge locale, secrets.json chiffré, journaux)."
}

# Les fichiers du programme sont supprimés en dernier, après la fin de ce script (il s'exécute depuis ce dossier).
if (Test-Path $installDir) {
    Start-Process cmd.exe -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$installDir`"" -WindowStyle Hidden
}
Write-Host "$app desinstalle."
