# Installe customNotch pour l'utilisateur courant (aucun droit administrateur requis).
#   - copie l'application dans %LOCALAPPDATA%\Programs\customNotch
#   - raccourci dans le menu Démarrer (c'est aussi ce qui donne son nom aux notifications)
#   - entrée « Applications installées » (désinstallation propre)
#   - tâche planifiée « customNotch » à l'ouverture de session, relancée en cas d'échec
#   - tâche planifiée « customNotch Watchdog » toutes les 15 min : relance l'application
#     si elle est tombée (sauf si vous l'avez quittée vous-même depuis le menu de l'icône)
#   - retire l'ancienne entrée de démarrage (clé Run) : la tâche planifiée la remplace
# Utilisation :  powershell -ExecutionPolicy Bypass -File scripts\install.ps1 [-Source <dossier>] [-NoStart]
param(
    [string]$Source = "",
    [switch]$NoStart
)

$ErrorActionPreference = "Stop"
$app = "customNotch"
$exeName = "$app.exe"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Source) {
    $candidates = @((Join-Path $scriptDir $app), (Join-Path (Split-Path -Parent $scriptDir) "dist\$app"))
    $Source = $candidates | Where-Object { Test-Path (Join-Path $_ $exeName) } | Select-Object -First 1
}
if (-not $Source -or -not (Test-Path (Join-Path $Source $exeName))) {
    throw "$exeName introuvable. Publiez-le (scripts\publish.ps1) ou indiquez -Source."
}
$Source = (Resolve-Path $Source).Path

$installDir = Join-Path $env:LOCALAPPDATA "Programs\$app"
$exe = Join-Path $installDir $exeName
$version = (Get-Item (Join-Path $Source $exeName)).VersionInfo.ProductVersion
if (-not $version) { $version = "1.0.0" }

Write-Host "Installation de $app $version dans $installDir"

# 1. Arrêter l'instance en cours
Get-Process -Name $app -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Sleep -Milliseconds 800

# 2. Copier les fichiers (miroir : les anciens fichiers sont retirés)
New-Item -ItemType Directory -Force $installDir | Out-Null
$null = robocopy $Source $installDir /MIR /NFL /NDL /NJH /NJS /NP /R:3 /W:1
if ($LASTEXITCODE -ge 8) { throw "copie des fichiers en échec (robocopy $LASTEXITCODE)" }
Copy-Item (Join-Path $scriptDir "uninstall.ps1") (Join-Path $installDir "uninstall.ps1") -Force
# L'icone a part : un raccourci qui pointe sur l'executable garderait l'ancien dessin dans
# le cache d'icones de Windows jusqu'au redemarrage d'Explorer.
$icon = Join-Path $installDir "customnotch.ico"
if (-not (Test-Path $icon)) { $icon = $exe }

# 3. Raccourci du menu Démarrer
$shell = New-Object -ComObject WScript.Shell
$programs = [Environment]::GetFolderPath("Programs")
$lnk = $shell.CreateShortcut((Join-Path $programs "$app.lnk"))
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $installDir
$lnk.Description = "customNotch - strip de statut et de lancement"
$lnk.IconLocation = $icon
$lnk.Save()
$oldStartup = Join-Path ([Environment]::GetFolderPath("Startup")) "$app.lnk"
if (Test-Path $oldStartup) { Remove-Item $oldStartup -Force }

# 4. L'ancienne clé Run (posée par une copie lancée depuis les sources ou un autre dossier)
#    lancerait une seconde copie : la tâche planifiée la remplace.
$run = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
foreach ($name in @($app)) {
    Remove-ItemProperty -Path $run -Name $name -ErrorAction SilentlyContinue
}

# 5. Tâches planifiées
$action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $installDir
$logon = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew -StartWhenAvailable
Register-ScheduledTask -TaskName $app -Action $action -Trigger $logon -Settings $settings `
    -Description "$app : pilules de statut et de lancement sur un bord de l'écran (démarre avec la session)" -Force | Out-Null

$watchAction = New-ScheduledTaskAction -Execute $exe -Argument "--auto" -WorkingDirectory $installDir
# Répétition sans durée limite ; -RepetitionDuration [TimeSpan]::MaxValue est refusé sur Windows 11
$watchTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 15)
$watchSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -MultipleInstances IgnoreNew -StartWhenAvailable -Hidden
Register-ScheduledTask -TaskName "$app Watchdog" -Action $watchAction -Trigger $watchTrigger `
    -Settings $watchSettings -Description "Relance $app s'il s'est arrêté de façon inattendue" -Force | Out-Null

# 6. Entrée « Applications installées »
$reg = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CustomNotch"
New-Item -Path $reg -Force | Out-Null
$size = [int]((Get-ChildItem $installDir -Recurse | Measure-Object Length -Sum).Sum / 1KB)
Set-ItemProperty $reg "DisplayName" $app
Set-ItemProperty $reg "DisplayVersion" $version
Set-ItemProperty $reg "Publisher" "DevPilot"
Set-ItemProperty $reg "InstallLocation" $installDir
Set-ItemProperty $reg "DisplayIcon" $exe
Set-ItemProperty $reg "UninstallString" "powershell.exe -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""
Set-ItemProperty $reg "QuietUninstallString" "powershell.exe -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`" -Quiet"
Set-ItemProperty $reg "InstallDate" (Get-Date -Format "yyyyMMdd")
Set-ItemProperty $reg "EstimatedSize" $size -Type DWord
Set-ItemProperty $reg "NoModify" 1 -Type DWord
Set-ItemProperty $reg "NoRepair" 1 -Type DWord

Write-Host "  Application    : $exe"
Write-Host "  Menu Demarrer  : $app"
Write-Host "  Taches         : $app (ouverture de session), $app Watchdog (toutes les 15 min)"
Write-Host "  Donnees        : $env:APPDATA\$app (config.json, cells.json et sa surcharge locale, secrets.json chiffré, journaux)"
Write-Host "  Desinstallation: Parametres > Applications > $app, ou $installDir\uninstall.ps1"

# 7. Démarrer
if (-not $NoStart) {
    Start-Process $exe -WorkingDirectory $installDir
    Write-Host "$app demarre."
}
