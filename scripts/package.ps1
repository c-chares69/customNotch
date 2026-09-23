# Publie l'application .NET (scripts\publish.ps1) puis produit :
#   - dist\customNotch-<version>-setup.exe : l'installateur (Inno Setup), à partager ;
#   - dist\customNotch-<version>-win64.zip : l'archive portable (application + install.ps1 + Install.cmd) ;
#   - dist\latest.json : le manifeste des mises à jour, pointant sur l'installateur.
# Aucun droit administrateur, ni pour construire ni pour installer.
# Utilisation :  powershell -ExecutionPolicy Bypass -File scripts\package.ps1 [-SkipBuild]
param(
    [switch]$SkipBuild,
    # Adresse où le zip et latest.json seront déposés (ex. https://exemple.fr/clickup-extended/).
    # Vide : l'URL du manifeste est relative, résolue par l'application contre l'adresse du manifeste.
    [string]$BaseUrl = "",
    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"
$app = "customNotch"
$projectDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $projectDir

if (-not $SkipBuild) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $projectDir "scripts\publish.ps1")
    if (-not $?) { throw "publication en echec" }
}

$exe = Join-Path $projectDir "dist\$app\$app.exe"
if (-not (Test-Path $exe)) { throw "$exe introuvable : publiez d'abord (scripts\publish.ps1)." }
$version = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $version) { $version = "0.0.0" }
# .NET ajoute parfois un « +hash » a la version de produit.
$version = ($version -split "\+")[0]

$stage = Join-Path $projectDir "dist\package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
$null = robocopy (Join-Path $projectDir "dist\$app") (Join-Path $stage $app) /MIR /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw "copie des fichiers en echec (robocopy $LASTEXITCODE)" }
# L'icone a part, pour les raccourcis (voir install.ps1 et installer.iss).
Copy-Item (Join-Path $projectDir "assets\customnotch.ico") (Join-Path $stage "$app\customnotch.ico") -Force
foreach ($f in @("install.ps1", "uninstall.ps1", "Install.cmd")) {
    Copy-Item (Join-Path $projectDir "scripts\$f") (Join-Path $stage $f) -Force
}
Copy-Item (Join-Path $projectDir "README.md") (Join-Path $stage "LISEZMOI.md") -Force

$zip = Join-Path $projectDir "dist\customNotch-$version-win64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
$size = [math]::Round((Get-Item $zip).Length / 1MB, 0)

# Installateur Inno Setup : le vrai setup.exe, celui que l'on partage.
$setup = $null
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if ($iscc) {
    & $iscc /Qp "/DAppVersion=$version" "/DSourceDir=$projectDir\dist\$app" (Join-Path $projectDir "scripts\installer.iss")
    if ($LASTEXITCODE -ne 0) { throw "compilation de l'installateur en echec (ISCC $LASTEXITCODE)" }
    $setup = Join-Path $projectDir "dist\customNotch-$version-setup.exe"
    if (-not (Test-Path $setup)) { throw "installateur attendu introuvable : $setup" }
} else {
    Write-Warning "Inno Setup 6 (ISCC.exe) introuvable : pas d'installateur, seulement le zip. winget install JRSoftware.InnoSetup"
}

# Manifeste de mise à jour : l'application le lit pour annoncer une version plus récente
# et vérifie l'empreinte du fichier avant toute installation. Il pointe sur l'installateur
# (installation silencieuse), à défaut sur le zip.
$payload = if ($setup) { $setup } else { $zip }
$hash = (Get-FileHash $payload -Algorithm SHA256).Hash.ToLower()
$payloadName = Split-Path $payload -Leaf
$url = if ($BaseUrl) { ($BaseUrl.TrimEnd("/") + "/" + $payloadName) } else { $payloadName }
$manifest = [ordered]@{
    version    = $version
    url        = $url
    sha256     = $hash
    zip        = (Split-Path $zip -Leaf)
    zip_sha256 = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    notes      = $Notes
    published  = (Get-Date -Format "yyyy-MM-dd")
}
$manifestPath = Join-Path $projectDir "dist\latest.json"
($manifest | ConvertTo-Json) | Set-Content -Path $manifestPath -Encoding utf8
Write-Host ""
if ($setup) {
    $setupSize = [math]::Round((Get-Item $setup).Length / 1MB, 0)
    Write-Host "Installateur : $setup ($setupSize MB) - a partager tel quel"
}
Write-Host "Archive portable : $zip ($size MB)"
Write-Host "Manifeste : $manifestPath (a deposer a cote de $payloadName ; SHA-256 $hash)"
Write-Host "Installation sur un poste : dezipper puis Install.cmd (aucun droit administrateur requis)."
