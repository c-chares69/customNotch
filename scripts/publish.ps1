# Publie l'application .NET en exécutable unique (win-x64), dans dist\customNotch :
# un seul .exe de quelques Mo qui s'appuie sur le runtime .NET 10 Desktop (posé par
# l'installateur s'il manque), ou auto-contenu avec -SelfContained. Même dossier que
# la version 1.x : installer.iss et package.ps1 n'ont rien à connaître de la technologie.
#
# Signature : si CUSTOMNOTCH_SIGN_THUMBPRINT ou CUSTOMNOTCH_SIGN_PFX
# (+ CUSTOMNOTCH_SIGN_PASSWORD) sont définis, l'exe est signé avec signtool.
# Utilisation :  powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release",
    # Auto-contenu : embarque le runtime .NET (≈ 64 Mo) ; sinon l'exe pèse quelques Mo et
    # l'installateur pose le runtime .NET 10 Desktop s'il manque (une fois par poste).
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$app = "customNotch"
$projectDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $projectDir
$out = Join-Path $projectDir "dist\$app"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

# Une application qui tourne encore garde son exe verrouillé.
Get-Process -Name $app -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$projectDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue

& dotnet publish (Join-Path $projectDir "src\CustomNotch.App\CustomNotch.App.csproj") `
    -c $Configuration -r win-x64 --self-contained:$([bool]$SelfContained) `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=$([bool]$SelfContained) -p:DebugType=none -p:SatelliteResourceLanguages=fr `
    -o $out -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish en echec ($LASTEXITCODE)" }

$exe = Join-Path $out "$app.exe"
if (-not (Test-Path $exe)) { throw "$exe introuvable apres publication" }

# Signature (facultative)
$thumb = $env:CUSTOMNOTCH_SIGN_THUMBPRINT
$pfx = $env:CUSTOMNOTCH_SIGN_PFX
if ($thumb -or $pfx) {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like "*x64*" } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { throw "signtool.exe introuvable (Windows SDK)" }
    $args = @("sign", "/fd", "SHA256", "/tr", "http://timestamp.digicert.com", "/td", "SHA256")
    if ($thumb) { $args += @("/sha1", $thumb) } else { $args += @("/f", $pfx, "/p", $env:CUSTOMNOTCH_SIGN_PASSWORD) }
    foreach ($f in @($exe)) {
        & $signtool.FullName @args $f
        if ($LASTEXITCODE -ne 0) { throw "signature en echec pour $f" }
    }
    Write-Host "Signature  : ok"
} else {
    Write-Host "Signature  : non signe - definir CUSTOMNOTCH_SIGN_THUMBPRINT ou CUSTOMNOTCH_SIGN_PFX pour signer"
}

$version = (Get-Item $exe).VersionInfo.ProductVersion
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Executable : $exe"
$mode = if ($SelfContained) { "auto-contenu, aucun runtime a installer" } else { "runtime .NET 10 Desktop requis (pose par l'installateur)" }
Write-Host "Version    : $version - $size MB, $mode"
