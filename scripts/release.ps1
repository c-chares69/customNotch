# Publie la release GitHub de la version courante : setup.exe, archive portable, latest.json.
#
#   powershell -ExecutionPolicy Bypass -File scripts\release.ps1            crée (ou complète) la release v<version>
#   powershell -ExecutionPolicy Bypass -File scripts\release.ps1 -DryRun    montre ce qui serait fait
#
# Le jeton GitHub vient de GITHUB_TOKEN / GH_TOKEN, sinon du gestionnaire d'identifiants Git
# (celui qui sert déjà à pousser) ; il n'est jamais affiché ni écrit. Les notes de la release
# sont la section correspondante de CHANGELOG.md. Une fois publiée, l'adresse du manifeste
# pour *Réglages → Général → Mises à jour* est :
#
#   https://github.com/<owner>/<repo>/releases/latest/download/latest.json
#
# (pour un dépôt privé, ce téléchargement exige d'être connecté : la mise à jour automatique
# suppose un dépôt public ou une autre adresse de dépôt du manifeste).
param(
    [switch]$DryRun,
    # owner/repo ; défaut : le remote origin
    [string]$Repo = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$api = "https://api.github.com"

function Get-GitHubToken {
    foreach ($name in @("GITHUB_TOKEN", "GH_TOKEN")) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($value) { return $value }
    }
    $env:GCM_INTERACTIVE = "never"; $env:GIT_TERMINAL_PROMPT = "0"
    $out = "protocol=https`nhost=github.com`n`n" | & git -C $root credential fill 2>$null
    $line = $out | Where-Object { $_ -like "password=*" } | Select-Object -First 1
    if ($line) { return $line.Substring(9) }
    return ""
}

function Get-OriginRepo {
    $url = (& git -C $root remote get-url origin).Trim()
    $tail = ($url -split "github.com[:/]")[-1]
    if ($tail.EndsWith(".git")) { $tail = $tail.Substring(0, $tail.Length - 4) }
    $parts = $tail -split "/"
    return @($parts[-2], $parts[-1])
}

function Get-ReleaseNotes([string]$version) {
    $text = Get-Content (Join-Path $root "CHANGELOG.md") -Raw -Encoding UTF8
    $marker = "## $version"
    $at = $text.IndexOf($marker)
    if ($at -lt 0) { return "customNotch $version" }
    $section = $text.Substring($at + $marker.Length)
    $next = $section.IndexOf("`n## ")
    if ($next -ge 0) { $section = $section.Substring(0, $next) }
    $body = ($section -split "`n", 2)[-1].Trim()
    return "## customNotch $version`n$body"
}

# La version : celle de l'exécutable publié (publish.ps1 / package.ps1 l'ont produit).
$exe = Join-Path $root "dist\customNotch\customNotch.exe"
if (-not (Test-Path $exe)) { Write-Host "dist\customNotch\customNotch.exe introuvable - lance scripts\package.ps1 d'abord."; exit 1 }
$version = ((Get-Item $exe).VersionInfo.ProductVersion -split "\+")[0]
$tag = "v$version"

if ($Repo) { $owner, $name = $Repo -split "/" } else { $owner, $name = Get-OriginRepo }
$assets = @("customNotch-$version-setup.exe", "customNotch-$version-win64.zip", "latest.json") | ForEach-Object { Join-Path $root "dist\$_" }
$missing = $assets | Where-Object { -not (Test-Path $_) } | ForEach-Object { Split-Path -Leaf $_ }
if ($missing) { Write-Host "Fichiers manquants dans dist/ : $($missing -join ', ') - lance scripts\package.ps1 d'abord."; exit 1 }

$body = (Get-ReleaseNotes $version) + "`n`n**Installation** : télécharger ``customNotch-$version-setup.exe`` et double-cliquer " +
    "(aucun droit administrateur ; le runtime .NET 10 Desktop est posé s'il manque). ``latest.json`` est le manifeste des mises à jour : " +
    "*Réglages → Général → Mises à jour*, adresse ``https://github.com/$owner/$name/releases/latest/download/latest.json``."
Write-Host "Release $tag sur $owner/$name : $(($assets | ForEach-Object { Split-Path -Leaf $_ }) -join ', ')"
if ($DryRun) { Write-Host $body; exit 0 }

$token = Get-GitHubToken
if (-not $token) { Write-Host "Aucun jeton GitHub : définis GITHUB_TOKEN, ou connecte-toi une fois avec git push (HTTPS)."; exit 1 }
$headers = @{ Authorization = "Bearer $token"; Accept = "application/vnd.github+json"; "X-GitHub-Api-Version" = "2022-11-28" }

$release = $null
try { $release = Invoke-RestMethod -Uri "$api/repos/$owner/$name/releases/tags/$tag" -Headers $headers -TimeoutSec 20 } catch { $release = $null }
if ($release) {
    Write-Host "Release existante : $($release.html_url)"
} else {
    $payload = @{ tag_name = $tag; target_commitish = "main"; name = "customNotch $version"; body = $body; draft = $false; prerelease = $false } | ConvertTo-Json
    try {
        $release = Invoke-RestMethod -Method Post -Uri "$api/repos/$owner/$name/releases" -Headers $headers -Body ([Text.Encoding]::UTF8.GetBytes($payload)) -ContentType "application/json; charset=utf-8" -TimeoutSec 30
    } catch { Write-Host "Création refusée : $($_.Exception.Message)"; exit 1 }
    Write-Host "Release créée : $($release.html_url)"
}

$present = @($release.assets | ForEach-Object { $_.name })
$uploadUrl = ($release.upload_url -split "\{")[0]
foreach ($path in $assets) {
    $leaf = Split-Path -Leaf $path
    if ($present -contains $leaf) { Write-Host "  déjà joint : $leaf"; continue }
    $mime = if ($leaf.EndsWith(".json")) { "application/json" } else { "application/octet-stream" }
    $size = [math]::Floor((Get-Item $path).Length / 1MB)
    try {
        Invoke-RestMethod -Method Post -Uri "$uploadUrl`?name=$leaf" -Headers $headers -InFile $path -ContentType $mime -TimeoutSec 900 | Out-Null
        Write-Host "  joint : $leaf ($size Mo)"
    } catch { Write-Host "  REFUSÉ : $leaf - $($_.Exception.Message)"; exit 1 }
}
Write-Host "Manifeste : https://github.com/$owner/$name/releases/latest/download/latest.json"
