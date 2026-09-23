# Retire les tâches planifiées et l'entrée de démarrage de customNotch.
# Appelé par l'installateur (démarrage automatique décoché) et par la désinstallation.
# Utilisation :  powershell -ExecutionPolicy Bypass -File remove_tasks.ps1 [-KeepRegistry]
param(
    [switch]$KeepRegistry
)

$ErrorActionPreference = "Continue"
$app = "customNotch"
foreach ($task in @($app, "$app Watchdog")) {
    Unregister-ScheduledTask -TaskName $task -Confirm:$false -ErrorAction SilentlyContinue
}
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name $app -ErrorAction SilentlyContinue
if (-not $KeepRegistry) {
    Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CustomNotch" -Recurse -Force -ErrorAction SilentlyContinue
}
