# Tâches planifiées de customNotch, posées par l'installateur (installer.iss) :
#   - « customNotch » à l'ouverture de session, relancée jusqu'à 3 fois en cas d'échec
#   - « customNotch Watchdog » toutes les 15 min : relance l'application si elle est tombée
#     (sauf si elle a été quittée volontairement depuis le menu de l'icône : --auto le sait)
# Retire aussi l'ancienne clé Run et l'entrée de désinstallation de l'ancien script install.ps1.
# Utilisation :  powershell -ExecutionPolicy Bypass -File install_tasks.ps1 -Exe "<chemin de l'exe>"
param(
    [Parameter(Mandatory = $true)][string]$Exe
)

$ErrorActionPreference = "Stop"
$app = "customNotch"
$dir = Split-Path -Parent $Exe

$run = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
foreach ($name in @($app)) {
    Remove-ItemProperty -Path $run -Name $name -ErrorAction SilentlyContinue
}
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CustomNotch" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dir "uninstall.ps1") -Force -ErrorAction SilentlyContinue

# --startup : comme un lancement manuel, mais une copie en trop s'efface sans ouvrir les réglages.
$action = New-ScheduledTaskAction -Execute $Exe -Argument "--startup" -WorkingDirectory $dir
$logon = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew -StartWhenAvailable
Register-ScheduledTask -TaskName $app -Action $action -Trigger $logon -Settings $settings `
    -Description "$app : pilules de statut et de lancement sur un bord de l'écran (démarre avec la session)" -Force | Out-Null

$watchAction = New-ScheduledTaskAction -Execute $Exe -Argument "--auto" -WorkingDirectory $dir
$watchTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 15)
$watchSettings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -MultipleInstances IgnoreNew -StartWhenAvailable -Hidden
Register-ScheduledTask -TaskName "$app Watchdog" -Action $watchAction -Trigger $watchTrigger `
    -Settings $watchSettings -Description "Relance $app s'il s'est arrêté de façon inattendue" -Force | Out-Null
