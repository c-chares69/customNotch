# customNotch — Plan d'implémentation 1/2 : le socle

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Une app WPF qui affiche une ou plusieurs pilules noires ancrées à un bord d'écran, dont chaque cellule est alimentée par une source générique (système, HTTP, shell, lanceur) configurée dans `cells.json`, rechargée à chaud, avec carte de détail au survol et actions.

**Architecture:** Trois projets .NET : `CustomNotch.Core` (modèle `Reading`/`CellView`, config trois fichiers, registre de sources, ordonnanceur — sans dépendance WPF), `CustomNotch.App` (WPF : `PillWindow` par pilule, contrôles de cellule, carte hover, tray, contrôleur), `CustomNotch.Hook` (créé vide ici, rempli dans le plan 2). Le plan 2 ajoute Claude Code, média, ClickUp, la fenêtre Settings, l'updater et l'installateur.

**Tech Stack:** C# 14 / .NET 10, WPF + WinForms (`NotifyIcon`, `Screen`), `System.Text.Json`, xUnit. Briques copiées de `C:\Users\Coco_FW\Desktop\ClickUp-Extended` (Theme, Ui, Glyphs, Native, Screens, Json, Log, Secrets, Paths).

**Spec:** `docs/superpowers/specs/2026-09-22-customnotch-design.md`

## Global Constraints

- `global.json` : SDK `10.0.0`, `rollForward: latestFeature`. `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true` (Directory.Build.props).
- `CustomNotch.Core` cible `net10.0` (pas `-windows`) : tout P/Invoke Windows vit dans `src/CustomNotch.Core/Platform/` derrière `OperatingSystem.IsWindows()`.
- `CustomNotch.App` cible `net10.0-windows10.0.19041.0`, `UseWPF` + `UseWindowsForms`, avec `<Using Remove="System.Windows.Forms" />` et `<Using Remove="System.Drawing" />`.
- Fichiers `src/CustomNotch.App/Shared/*.cs` : copiés de ClickUp-Extended, seules modifications autorisées = `namespace` et le type `Config` → `AppConfig`.
- Données : `%APPDATA%\customNotch\` ; variable `CUSTOMNOTCH_HOME` ; `cells.json` à l'emplacement `cells_path` de `config.json` (défaut : ce dossier) ; surcharge `cells.<machine>.json` ; `secrets.json` DPAPI.
- Valeurs visuelles (spec §5) : pilule largeur 70, coins 20, fillets 38,7, fond `#000`, liseré `#2e2e2e` ; anneau 44, piste `#303030` ; carte `#0a0a0a`, rayon 16, padding 16, largeur max 246, queue 32×36.
- Commits en français, **sans aucune mention d'IA ni trailer `Co-Authored-By`**. Identité déjà posée dans le dépôt (`git config user.name` = c-chares).
- Commentaires et docs en français, même style que ClickUp-Extended (une phrase qui dit *pourquoi*).
- Toute commande passe par PowerShell (`dotnet build`, `dotnet test`) depuis `C:\Users\Coco_FW\Desktop\customNotch`.

---

## Arborescence produite par ce plan

```text
customNotch/
├── CustomNotch.sln, Directory.Build.props, global.json, .gitignore, .gitattributes
├── src/CustomNotch.Core/
│   ├── CustomNotch.Core.csproj
│   ├── App.cs, Paths.cs, Log.cs, Json.cs, Secrets.cs
│   ├── Model/Status.cs, Model/Reading.cs, Model/Thresholds.cs, Model/CellView.cs
│   ├── Config/CellsFile.cs, Config/Placeholders.cs, Config/ConfigMerge.cs, Config/ConfigValidation.cs,
│   │   Config/AppConfig.cs, Config/SecretsFile.cs, Config/ConfigStore.cs, Config/DefaultCells.cs
│   ├── Sources/ISource.cs, Sources/SourceRegistry.cs, Sources/ReadingStore.cs, Sources/Scheduler.cs
│   ├── Sources/System/CpuSource.cs, MemorySource.cs, DiskSource.cs, NetworkSource.cs, BatterySource.cs
│   ├── Sources/LauncherSource.cs, Sources/HttpSource.cs, Sources/ShellSource.cs, Sources/JsonPath.cs
│   ├── Actions/ActionRunner.cs
│   └── Platform/SystemInfo.cs, Platform/Idle.cs
├── src/CustomNotch.App/
│   ├── CustomNotch.App.csproj, app.manifest, App.xaml, App.xaml.cs, Styles.xaml
│   ├── Controller.cs, TrayIcon.cs, PillDiff.cs
│   ├── Shared/Theme.cs, Ui.cs, Glyphs.cs, Native.cs, Screens.cs
│   ├── Notch/EdgePlacement.cs, PillShape.cs, PillWindow.cs, HoverCard.cs, FullScreenDetector.cs
│   └── Cells/StatusPalette.cs, GlyphLibrary.cs, RingCell.cs, ValueCell.cs, StatusCell.cs, SparklineCell.cs, CellHost.cs
├── src/CustomNotch.Hook/CustomNotch.Hook.csproj, Program.cs (vide : `return 0;`)
├── tests/CustomNotch.Core.Tests/ (xUnit)
└── docs/ARCHITECTURE.md, README.md, CHANGELOG.md, NOTICE.md
```

---

### Task 1: Squelette de solution et utilitaires de base

**Files:**
- Create: `global.json`, `Directory.Build.props`, `CustomNotch.sln`, `.gitattributes`
- Create: `src/CustomNotch.Core/CustomNotch.Core.csproj`, `src/CustomNotch.Core/App.cs`, `Paths.cs`, `Log.cs`, `Json.cs`, `Secrets.cs`
- Create: `src/CustomNotch.App/CustomNotch.App.csproj`, `src/CustomNotch.App/app.manifest`, `src/CustomNotch.App/App.xaml`, `src/CustomNotch.App/App.xaml.cs`
- Create: `src/CustomNotch.Hook/CustomNotch.Hook.csproj`, `src/CustomNotch.Hook/Program.cs`
- Create: `tests/CustomNotch.Core.Tests/CustomNotch.Core.Tests.csproj`, `tests/CustomNotch.Core.Tests/PathsTests.cs`
- Create: `NOTICE.md`, `LICENSE`

**Interfaces:**
- Produces: `CustomNotch.Core.App` (`Name = "customNotch"`, `Id = "DevPilot.CustomNotch"`, `HomeEnv = "CUSTOMNOTCH_HOME"`, `Version`), `Paths.Home()`, `Paths.ConfigFile(home)`, `Paths.LogDir(home)`, `Paths.LocalCellsFile(home)`, `Paths.SecretsFile(home)`, `Json.WriteAtomic(path, text, category)`, `Json.Pretty`, `Log.Info/Warning/Error(category, message)`, `Log.Directory`, `Secrets.Seal/Unseal`.

- [ ] **Step 1: Fichiers de solution**

`global.json` :
```json
{
  "sdk": { "version": "10.0.0", "rollForward": "latestFeature" }
}
```

`Directory.Build.props` :
```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Version>0.1.0</Version>
    <Authors>DevPilot</Authors>
    <Product>customNotch</Product>
    <Copyright>DevPilot</Copyright>
  </PropertyGroup>
</Project>
```

`.gitattributes` :
```
* text=auto eol=lf
*.ps1 text eol=crlf
*.cmd text eol=crlf
*.ico binary
*.png binary
```

Créer la solution et les projets par la CLI (les csproj sont réécrits ensuite) :
```powershell
dotnet new sln -n CustomNotch
dotnet new classlib -n CustomNotch.Core -o src/CustomNotch.Core
dotnet new wpf -n CustomNotch.App -o src/CustomNotch.App
dotnet new console -n CustomNotch.Hook -o src/CustomNotch.Hook
dotnet new xunit -n CustomNotch.Core.Tests -o tests/CustomNotch.Core.Tests
dotnet sln add src/CustomNotch.Core src/CustomNotch.App src/CustomNotch.Hook tests/CustomNotch.Core.Tests
Remove-Item src/CustomNotch.Core/Class1.cs, src/CustomNotch.App/MainWindow.xaml, src/CustomNotch.App/MainWindow.xaml.cs, src/CustomNotch.App/AssemblyInfo.cs, tests/CustomNotch.Core.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 2: csproj**

`src/CustomNotch.Core/CustomNotch.Core.csproj` :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Le cœur sans interface : modèle, configuration, sources, ordonnanceur. Cible net10.0 tout court :
       il doit rester portable ; les appels Windows vivent dans Platform/ derrière OperatingSystem.IsWindows(). -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>CustomNotch.Core</RootNamespace>
    <AssemblyName>CustomNotch.Core</AssemblyName>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="CustomNotch.Core.Tests" />
  </ItemGroup>
</Project>
```

`src/CustomNotch.App/CustomNotch.App.csproj` :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- L'application Windows : pilules, carte hover, tray. WPF ; WinForms seulement pour NotifyIcon et Screen. -->
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <AssemblyName>customNotch</AssemblyName>
    <RootNamespace>CustomNotch.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <SatelliteResourceLanguages>fr</SatelliteResourceLanguages>
    <Description>customNotch - strip de statut et de lancement</Description>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>false</IncludeNativeLibrariesForSelfExtract>
    <DebugType>none</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\CustomNotch.Core\CustomNotch.Core.csproj" />
  </ItemGroup>
  <!-- WinForms n'est là que pour NotifyIcon et Screen : ses noms ne doivent pas entrer en collision avec WPF. -->
  <ItemGroup>
    <Using Remove="System.Windows.Forms" />
    <Using Remove="System.Drawing" />
  </ItemGroup>
</Project>
```

`src/CustomNotch.App/app.manifest` : copier `C:\Users\Coco_FW\Desktop\ClickUp-Extended\src\ClickUpExtended.App\app.manifest` et remplacer `name="DevPilot.ClickUpExtended"` par `name="DevPilot.CustomNotch"`, `version="2.0.0.0"` par `version="0.1.0.0"`.

`src/CustomNotch.Hook/CustomNotch.Hook.csproj` :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Le client minuscule appelé par les hooks Claude Code (rempli dans le plan 2). Pas de dépendance : il doit démarrer en moins de 100 ms. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>customNotch-hook</AssemblyName>
    <RootNamespace>CustomNotch.Hook</RootNamespace>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```
`src/CustomNotch.Hook/Program.cs` : `return 0;`

`tests/CustomNotch.Core.Tests/CustomNotch.Core.Tests.csproj` :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\CustomNotch.Core\CustomNotch.Core.csproj" />
  </ItemGroup>
</Project>
```
Si `dotnet restore` refuse une version de paquet, prendre la dernière stable indiquée par `dotnet package search <nom> --take 1`.

- [ ] **Step 3: Utilitaires du cœur**

`src/CustomNotch.Core/App.cs` :
```csharp
namespace CustomNotch.Core;

/// <summary>Identité de l'application : nom du dossier de données, AppUserModelID, version (Directory.Build.props).</summary>
public static class App
{
    public const string Name = "customNotch";
    public const string Id = "DevPilot.CustomNotch";
    public const string Company = "DevPilot";
    public static readonly string Version = (typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "0.0.0").Split('+')[0];
    public const string HomeEnv = "CUSTOMNOTCH_HOME";
    /// <summary>Accent par défaut de la fenêtre de réglages quand Windows n'en fournit pas.</summary>
    public const string DefaultAccent = "#3B82F6";
}
```

`src/CustomNotch.Core/Paths.cs` :
```csharp
namespace CustomNotch.Core;

/// <summary>Où vivent les données : la variable CUSTOMNOTCH_HOME, le mode portable (dossier data + fichier
/// vide portable à côté de l'exe), puis %APPDATA%\customNotch. Le fichier cells.json, lui, est où l'utilisateur
/// le décide (config.json : cells_path) - typiquement un dossier synchronisé entre machines.</summary>
public static class Paths
{
    public static string? PortableHome(string? exeDir = null)
    {
        var dir = exeDir ?? AppContext.BaseDirectory;
        var candidate = Path.Combine(dir, "data");
        return Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "portable")) ? candidate : null;
    }

    public static string Home()
    {
        var overrideDir = Environment.GetEnvironmentVariable(App.HomeEnv);
        if (!string.IsNullOrWhiteSpace(overrideDir)) return Path.GetFullPath(overrideDir);
        if (PortableHome() is { } portable) return portable;
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrWhiteSpace(appData))
            appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
        return Path.Combine(appData, App.Name);
    }

    public static string ConfigFile(string? home = null) => Path.Combine(home ?? Home(), "config.json");
    public static string DefaultCellsFile(string? home = null) => Path.Combine(home ?? Home(), "cells.json");
    /// <summary>La surcharge locale : positions, écran, visibilité - jamais synchronisée.</summary>
    public static string LocalCellsFile(string? home = null) => Path.Combine(home ?? Home(), $"cells.{MachineSlug()}.json");
    public static string SecretsFile(string? home = null) => Path.Combine(home ?? Home(), "secrets.json");
    public static string LogDir(string? home = null) => Path.Combine(home ?? Home(), "logs");

    /// <summary>Le nom de machine, en minuscules et sans caractère interdit dans un nom de fichier.</summary>
    public static string MachineSlug(string? machine = null)
    {
        var name = (machine ?? Environment.MachineName).ToLowerInvariant();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) || c == '.' ? '-' : c).ToArray();
        var slug = new string(chars).Trim('-');
        return slug.Length == 0 ? "machine" : slug;
    }
}
```

`src/CustomNotch.Core/Json.cs`, `Log.cs`, `Secrets.cs` : copier depuis `C:\Users\Coco_FW\Desktop\ClickUp-Extended\src\ClickUpExtended.Core\` avec ces seules modifications :
- `namespace ClickUpExtended.Core;` → `namespace CustomNotch.Core;`
- `Secrets.cs` : entropie `"ClickUp-Extended.token.v1"` → `"customNotch.secret.v1"`.
- `Log.cs` : remplacer les deux regex de caviardage par une seule, générique, qui masque tout jeton long : `[GeneratedRegex(@"(Bearer\s+|pk_|sk-|ya29\.)([A-Za-z0-9_\-\.]{6})[A-Za-z0-9_\-\.]+")]` nommée `Token()`, et `Redact` devient `return Token().Replace(text, "$1$2…");`.

`NOTICE.md` :
```markdown
# Attributions

- **codenotch** (https://github.com/vinzdg/codenotch, licence MIT, © vinzdg) : le langage visuel de la pilule,
  des anneaux et de la carte, et la logique de lecture de l'usage Claude Code sont repris de ce projet.
- **ClickUp - Extended** et **AutoSort** (DevPilot) : Theme, Ui, Glyphs, Json, Log, Secrets, scripts d'installation.
```
`LICENSE` : MIT, © 2026 DevPilot.

- [ ] **Step 4: App.xaml minimal et test de fumée**

`src/CustomNotch.App/App.xaml` :
```xml
<Application x:Class="CustomNotch.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <ResourceDictionary>
            <FontFamily x:Key="UiFont">Segoe UI</FontFamily>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```
`src/CustomNotch.App/App.xaml.cs` (provisoire, remplacé en tâche 8) :
```csharp
using System.Windows;
namespace CustomNotch.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Shutdown();
    }
}
```

`tests/CustomNotch.Core.Tests/PathsTests.cs` :
```csharp
using CustomNotch.Core;
namespace CustomNotch.Core.Tests;

public class PathsTests
{
    [Fact]
    public void Le_slug_machine_est_un_nom_de_fichier_sur()
    {
        Assert.Equal("pc-de-coco", Paths.MachineSlug("PC.DE:COCO"));
        Assert.Equal("machine", Paths.MachineSlug("..."));
    }

    [Fact]
    public void La_variable_d_environnement_l_emporte()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8]);
        Environment.SetEnvironmentVariable(App.HomeEnv, dir);
        try { Assert.Equal(Path.GetFullPath(dir), Paths.Home()); }
        finally { Environment.SetEnvironmentVariable(App.HomeEnv, null); }
    }
}
```

- [ ] **Step 5: Build et tests**

Run: `dotnet build CustomNotch.sln` puis `dotnet test tests/CustomNotch.Core.Tests`
Expected: build sans avertissement (ils sont des erreurs), 2 tests PASS.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "chore: squelette de solution .NET 10 (Core, App, Hook, tests)"
```

---

### Task 2: Modèle — Status, Reading, Thresholds, CellView

**Files:**
- Create: `src/CustomNotch.Core/Model/Status.cs`, `Model/Reading.cs`, `Model/Thresholds.cs`, `Model/CellView.cs`
- Create: `src/CustomNotch.Core/Config/CellsFile.cs` (les types de config, sans chargement)
- Test: `tests/CustomNotch.Core.Tests/Model/ThresholdsTests.cs`, `tests/CustomNotch.Core.Tests/Model/CellViewTests.cs`

**Interfaces:**
- Produces: `enum Status { Ok, Warn, Crit, Off, Busy, Attention }`, `enum CellKind { Ring, Value, Status, Sparkline, Group }`, `record DetailRow(string Label, string? Text, double? Fraction, string? Hint, Status? Tone)`, `record ActionSpec(string Id, string Label, string? Icon)`, `record Reading(...)` avec `Reading.Empty`, `Reading.AsStale(long nowMs, string error)`, `record Thresholds(double? Warn, double? Crit, bool Invert)` avec `Status Judge(double value)`, `record CellView(...)`, `static CellViews.From(CellConfig cell, Reading reading, long nowMs)`, `CellViews.DeriveKind`, `CellViews.DeriveStatus`, `CellViews.Caption`, `CellViews.Age(long ms)`.
- Produces (config) : `CellConfig`, `PillConfig`, `CellsFile`, `ActionConfig`, `CellActions`.

- [ ] **Step 1: Types du modèle**

`src/CustomNotch.Core/Model/Status.cs` :
```csharp
namespace CustomNotch.Core.Model;

/// <summary>L'état d'une cellule, ce que la couleur de l'anneau dit d'un coup d'œil. Busy et Attention viennent
/// d'une source qui sait qu'un travail est en cours ou qu'on attend l'utilisateur (Claude, un timer, une CI).</summary>
public enum Status { Ok, Warn, Crit, Off, Busy, Attention }

/// <summary>Comment la cellule se dessine ; déduit de la lecture quand la config ne le fixe pas.</summary>
public enum CellKind { Ring, Value, Status, Sparkline, Group }
```

`src/CustomNotch.Core/Model/Reading.cs` :
```csharp
namespace CustomNotch.Core.Model;

/// <summary>Une ligne de la carte hover : libellé, texte à droite, barre 0..1 facultative, indication (« reset dans 51 min »).</summary>
public sealed record DetailRow(string Label, string? Text = null, double? Fraction = null, string? Hint = null, Status? Tone = null);

/// <summary>Un bouton de la carte : l'identifiant est ce que la source reçoit dans InvokeAsync.</summary>
public sealed record ActionSpec(string Id, string Label, string? Icon = null);

/// <summary>Ce qu'une source produit, et la seule chose que l'interface connaît. Tout est facultatif : une source
/// dit ce qu'elle sait, et CellViews en déduit le rendu.</summary>
public sealed record Reading(
    double? Value = null,
    double? Max = null,
    string? Unit = null,
    string? Text = null,
    Status? Status = null,
    IReadOnlyList<DetailRow>? Detail = null,
    IReadOnlyList<ActionSpec>? Actions = null,
    IReadOnlyList<(long Ms, double V)>? History = null,
    long? StaleSinceMs = null,
    string? Error = null)
{
    public static readonly Reading Empty = new();

    /// <summary>La même lecture, marquée périmée depuis maintenant (ou depuis la première panne) avec la raison.</summary>
    public Reading AsStale(long nowMs, string error) => this with { StaleSinceMs = StaleSinceMs ?? nowMs, Error = error };

    /// <summary>La lecture rafraîchie : plus périmée, plus d'erreur.</summary>
    public Reading Fresh() => this with { StaleSinceMs = null, Error = null };
}
```

`src/CustomNotch.Core/Model/Thresholds.cs` :
```csharp
namespace CustomNotch.Core.Model;

/// <summary>Seuils sur la valeur (en % quand la lecture a un maximum, sinon en valeur brute). Invert pour les
/// grandeurs où bas = mauvais (batterie).</summary>
public sealed record Thresholds(double? Warn = null, double? Crit = null, bool Invert = false)
{
    /// <summary>Les seuils d'un anneau quand la config n'en donne pas : jaune à 50 %, rouge à 75 %.</summary>
    public static readonly Thresholds RingDefault = new(50, 75);

    public Status Judge(double value)
    {
        if (Invert)
        {
            if (Crit is { } c && value <= c) return Status.Crit;
            if (Warn is { } w && value <= w) return Status.Warn;
            return Status.Ok;
        }
        if (Crit is { } cc && value >= cc) return Status.Crit;
        if (Warn is { } ww && value >= ww) return Status.Warn;
        return Status.Ok;
    }
}
```

- [ ] **Step 2: Types de configuration**

`src/CustomNotch.Core/Config/CellsFile.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Config;

/// <summary>Une action : ouvrir (URL, chemin, app), lancer une commande, ou appeler une méthode de la source.</summary>
public sealed class ActionConfig
{
    public string? Label { get; set; }
    public string? Icon { get; set; }
    public string? Open { get; set; }
    public string? Shell { get; set; }
    public string? Source { get; set; }
}

public sealed class CellActions
{
    public ActionConfig? Click { get; set; }
    public List<ActionConfig>? Card { get; set; }
}

public sealed class CellConfig
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "launcher";
    public string? Label { get; set; }
    public string? Glyph { get; set; }
    /// <summary>ring | value | status | sparkline | group ; null = déduit de la lecture.</summary>
    public string? Kind { get; set; }
    /// <summary>« 2s », « 500ms », « 5m », « 1h » ; null = cadence par défaut de la source.</summary>
    public string? Refresh { get; set; }
    public Thresholds? Thresholds { get; set; }
    /// <summary>Les paramètres propres à la source (url, drive, command…), laissés en JSON : chaque source lit les siens.</summary>
    public JsonObject? Params { get; set; }
    public List<string>? Children { get; set; }
    public string? Headline { get; set; }
    public CellActions? Actions { get; set; }
    public bool Visible { get; set; } = true;

    public bool IsGroup => Children is { Count: > 0 };

    /// <summary>La cadence demandée, ou null. « 2s » → 2 s ; formats acceptés : ms, s, m, h.</summary>
    public TimeSpan? RefreshSpan()
    {
        if (string.IsNullOrWhiteSpace(Refresh)) return null;
        var s = Refresh.Trim().ToLowerInvariant();
        var unit = s.EndsWith("ms") ? "ms" : s[^1..];
        var number = s[..^unit.Length];
        if (!double.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) || n <= 0) return null;
        return unit switch
        {
            "ms" => TimeSpan.FromMilliseconds(n),
            "s" => TimeSpan.FromSeconds(n),
            "m" => TimeSpan.FromMinutes(n),
            "h" => TimeSpan.FromHours(n),
            _ => null,
        };
    }
}

public sealed class PillConfig
{
    public string Id { get; set; } = "";
    /// <summary>left | right | top | bottom.</summary>
    public string Edge { get; set; } = "right";
    /// <summary>Position le long du bord, 0 = début (haut / gauche), 1 = fin.</summary>
    public double Along { get; set; } = 0.5;
    /// <summary>Nom d'écran (DeviceName WinForms) ; null = écran principal.</summary>
    public string? Screen { get; set; }
    public double Scale { get; set; } = 1.0;
    public bool Visible { get; set; } = true;
    public List<CellConfig> Cells { get; set; } = new();

    public bool IsVertical => Edge is "left" or "right";
}

/// <summary>Le fichier cells.json une fois fusionné avec la surcharge locale et les placeholders résolus.</summary>
public sealed class CellsFile
{
    public int Version { get; set; } = 1;
    public List<PillConfig> Pills { get; set; } = new();
    /// <summary>Réglages globaux par type de source (« claude »: { … }), laissés en JSON.</summary>
    public JsonObject? Sources { get; set; }

    public IEnumerable<CellConfig> AllCells() => Pills.SelectMany(p => p.Cells);
    public CellConfig? Cell(string id) => AllCells().FirstOrDefault(c => c.Id == id);
}
```

- [ ] **Step 3: Tests des seuils et de CellView (écrits avant CellView)**

`tests/CustomNotch.Core.Tests/Model/ThresholdsTests.cs` :
```csharp
using CustomNotch.Core.Model;
namespace CustomNotch.Core.Tests.Model;

public class ThresholdsTests
{
    [Theory]
    [InlineData(10, Status.Ok)]
    [InlineData(50, Status.Warn)]
    [InlineData(74.9, Status.Warn)]
    [InlineData(75, Status.Crit)]
    public void Les_seuils_par_defaut_montent(double value, Status expected) => Assert.Equal(expected, Thresholds.RingDefault.Judge(value));

    [Theory]
    [InlineData(80, Status.Ok)]
    [InlineData(20, Status.Warn)]
    [InlineData(5, Status.Crit)]
    public void Inverses_bas_est_mauvais(double value, Status expected) => Assert.Equal(expected, new Thresholds(20, 5, Invert: true).Judge(value));

    [Fact]
    public void Sans_seuil_tout_est_ok() => Assert.Equal(Status.Ok, new Thresholds().Judge(999));
}
```

`tests/CustomNotch.Core.Tests/Model/CellViewTests.cs` :
```csharp
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
namespace CustomNotch.Core.Tests.Model;

public class CellViewTests
{
    private static CellConfig Cell(string? kind = null, Thresholds? t = null, bool group = false)
        => new() { Id = "c", Label = "CPU", Kind = kind, Thresholds = t, Children = group ? new List<string> { "a" } : null };

    [Fact]
    public void Un_maximum_donne_un_anneau()
        => Assert.Equal(CellKind.Ring, CellViews.DeriveKind(Cell(), new Reading(Value: 30, Max: 100)));

    [Fact]
    public void Un_historique_donne_une_sparkline()
        => Assert.Equal(CellKind.Sparkline, CellViews.DeriveKind(Cell(), new Reading(Value: 3, History: new[] { (1L, 1.0), (2L, 3.0) })));

    [Fact]
    public void Une_valeur_seule_donne_une_valeur()
        => Assert.Equal(CellKind.Value, CellViews.DeriveKind(Cell(), new Reading(Value: 42, Unit: "°C")));

    [Fact]
    public void Rien_donne_un_statut()
        => Assert.Equal(CellKind.Status, CellViews.DeriveKind(Cell(), Reading.Empty));

    [Fact]
    public void Les_enfants_donnent_un_groupe_et_kind_explicite_l_emporte()
    {
        Assert.Equal(CellKind.Group, CellViews.DeriveKind(Cell(group: true), new Reading(Value: 1, Max: 2)));
        Assert.Equal(CellKind.Value, CellViews.DeriveKind(Cell(kind: "value"), new Reading(Value: 1, Max: 2)));
    }

    [Fact]
    public void Le_statut_de_la_source_l_emporte_sur_les_seuils()
        => Assert.Equal(Status.Busy, CellViews.DeriveStatus(Cell(), new Reading(Value: 99, Max: 100, Status: Status.Busy)));

    [Fact]
    public void Sans_statut_les_seuils_jugent_le_pourcentage()
    {
        Assert.Equal(Status.Crit, CellViews.DeriveStatus(Cell(), new Reading(Value: 80, Max: 100)));
        Assert.Equal(Status.Warn, CellViews.DeriveStatus(Cell(t: new Thresholds(Warn: 40)), new Reading(Value: 41, Unit: "°C")));
        Assert.Equal(Status.Off, CellViews.DeriveStatus(Cell(), Reading.Empty));
    }

    [Fact]
    public void La_legende_suit_le_type()
    {
        Assert.Equal("73%", CellViews.Caption(CellKind.Ring, new Reading(Value: 73.4, Max: 100)));
        Assert.Equal("42 °C", CellViews.Caption(CellKind.Value, new Reading(Value: 42, Unit: "°C")));
        Assert.Equal("1 240 €", CellViews.Caption(CellKind.Value, new Reading(Value: 1240, Unit: "€")));
        Assert.Equal("7", CellViews.Caption(CellKind.Value, new Reading(Value: 7)));
        Assert.Null(CellViews.Caption(CellKind.Status, Reading.Empty));
    }

    [Fact]
    public void Une_lecture_perimee_porte_son_age()
    {
        var view = CellViews.From(Cell(), new Reading(Value: 1, Max: 2).AsStale(1_000, "réseau"), nowMs: 1_000 + 5 * 60_000);
        Assert.True(view.Stale);
        Assert.Equal("il y a 5 min", view.StaleAge);
        Assert.Equal(0.5, view.Fraction);
    }

    [Fact]
    public void L_age_est_lisible()
    {
        Assert.Equal("à l'instant", CellViews.Age(30_000));
        Assert.Equal("il y a 2 h", CellViews.Age(2 * 3_600_000));
        Assert.Equal("il y a 3 j", CellViews.Age(3 * 86_400_000L));
    }
}
```

- [ ] **Step 4: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: erreurs de compilation `CellViews` introuvable.

- [ ] **Step 5: CellView**

`src/CustomNotch.Core/Model/CellView.cs` :
```csharp
using System.Globalization;
using CustomNotch.Core.Config;

namespace CustomNotch.Core.Model;

/// <summary>Ce que l'interface dessine pour une cellule : calculé d'une config et d'une lecture, sans WPF,
/// pour être testé sans écran.</summary>
public sealed record CellView(
    string Id,
    CellKind Kind,
    Status Status,
    string Label,
    string? Glyph,
    string? Caption,
    double? Fraction,
    bool Stale,
    string? StaleAge,
    Reading Reading);

public static class CellViews
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static CellView From(CellConfig cell, Reading r, long nowMs)
    {
        var kind = DeriveKind(cell, r);
        var status = DeriveStatus(cell, r);
        var stale = r.StaleSinceMs is not null;
        return new CellView(cell.Id, kind, status, cell.Label ?? cell.Id, cell.Glyph, Caption(kind, r), Fraction(r), stale,
            stale ? Age(nowMs - r.StaleSinceMs!.Value) : null, r);
    }

    public static CellKind DeriveKind(CellConfig cell, Reading r)
    {
        if (cell.IsGroup) return CellKind.Group;
        switch (cell.Kind?.Trim().ToLowerInvariant())
        {
            case "ring": return CellKind.Ring;
            case "value": return CellKind.Value;
            case "status": return CellKind.Status;
            case "sparkline": return CellKind.Sparkline;
            case "group": return CellKind.Group;
        }
        if (r.History is { Count: > 1 }) return CellKind.Sparkline;
        if (r.Max is not null && r.Value is not null) return CellKind.Ring;
        if (r.Value is not null) return CellKind.Value;
        return CellKind.Status;
    }

    public static Status DeriveStatus(CellConfig cell, Reading r)
    {
        if (r.Status is { } s) return s;
        if (r.Value is null) return Status.Off;
        var value = r.Max is { } max and > 0 ? r.Value.Value / max * 100 : r.Value.Value;
        var thresholds = cell.Thresholds ?? (r.Max is not null ? Thresholds.RingDefault : new Thresholds());
        return thresholds.Judge(value);
    }

    public static double? Fraction(Reading r)
        => r.Max is { } max and > 0 && r.Value is { } v ? Math.Clamp(v / max, 0, 1) : null;

    /// <summary>Le texte sous la cellule : « 73% » pour un anneau, « 42 °C » pour une valeur, rien pour un statut.</summary>
    public static string? Caption(CellKind kind, Reading r)
    {
        switch (kind)
        {
            case CellKind.Ring:
                return Fraction(r) is { } f ? $"{Math.Round(f * 100)}%" : null;
            case CellKind.Value:
            case CellKind.Sparkline:
                if (r.Value is not { } v) return r.Text;
                var number = Math.Abs(v) >= 100 || v == Math.Round(v) ? Math.Round(v).ToString("N0", Fr) : v.ToString("0.#", Fr);
                return string.IsNullOrEmpty(r.Unit) ? number : $"{number} {r.Unit}";
            default:
                return null;
        }
    }

    public static string Age(long ms)
    {
        var seconds = Math.Max(0, ms / 1000);
        if (seconds < 60) return "à l'instant";
        if (seconds < 3600) return $"il y a {seconds / 60} min";
        if (seconds < 86400) return $"il y a {seconds / 3600} h";
        return $"il y a {seconds / 86400} j";
    }
}
```
Note : `"N0"` en fr-FR insère une espace insécable fine (U+202F) ; le test attend `"1 240 €"` avec une espace : normaliser dans `Caption` avec `.Replace('\u202F', ' ').Replace('\u00A0', ' ')`.

- [ ] **Step 6: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: tous PASS.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(core): modèle Reading, seuils et CellView"
```

---

### Task 3: Configuration — placeholders, fusion, validation

**Files:**
- Create: `src/CustomNotch.Core/Config/Placeholders.cs`, `Config/ConfigMerge.cs`, `Config/ConfigValidation.cs`, `Config/CellsJson.cs`
- Test: `tests/CustomNotch.Core.Tests/Config/PlaceholdersTests.cs`, `Config/ConfigMergeTests.cs`, `Config/ConfigValidationTests.cs`

**Interfaces:**
- Consumes: `CellsFile`, `PillConfig`, `CellConfig` (tâche 2).
- Produces: `interface ISecretStore { string? Get(string name); }`, `Placeholders.Resolve(JsonNode? node, Func<string, string?> env, ISecretStore secrets, string home) → (JsonNode? Result, List<string> Missing)`, `ConfigMerge.Merge(JsonObject shared, JsonObject? local) → JsonObject` (pilules et cellules fusionnées par `id`), `ConfigValidation.Validate(CellsFile file, IReadOnlySet<string> knownSources) → List<string>`, `CellsJson.Options`, `CellsJson.Parse(string) → JsonObject`, `CellsJson.ToFile(JsonObject) → CellsFile`, `CellsJson.Serialize(object)`, `ConfigException`.

- [ ] **Step 1: Tests des placeholders**

`tests/CustomNotch.Core.Tests/Config/PlaceholdersTests.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
namespace CustomNotch.Core.Tests.Config;

public class PlaceholdersTests
{
    private sealed class Secrets : ISecretStore
    {
        public string? Get(string name) => name == "token" ? "s3cr3t" : null;
    }

    private static (JsonNode?, List<string>) Run(string json)
        => Placeholders.Resolve(JsonNode.Parse(json), n => n == "USERPROFILE" ? @"C:\Users\x" : null, new Secrets(), @"C:\home");

    [Fact]
    public void Env_secret_et_home_sont_remplaces_partout()
    {
        var (node, missing) = Run("""{"a":"${env:USERPROFILE}\\bin","b":{"c":["Bearer ${secret:token}","${home}/x"]}}""");
        Assert.Empty(missing);
        Assert.Equal(@"C:\Users\x\bin", node!["a"]!.GetValue<string>());
        Assert.Equal("Bearer s3cr3t", node["b"]!["c"]![0]!.GetValue<string>());
        Assert.Equal(@"C:\home/x", node["b"]!["c"]![1]!.GetValue<string>());
    }

    [Fact]
    public void Un_placeholder_inconnu_est_signale_et_laisse_vide()
    {
        var (node, missing) = Run("""{"a":"${secret:absent}","b":"${env:NOPE}"}""");
        Assert.Equal(new[] { "secret:absent", "env:NOPE" }, missing);
        Assert.Equal("", node!["a"]!.GetValue<string>());
    }

    [Fact]
    public void Les_valeurs_non_texte_sont_intactes()
    {
        var (node, _) = Run("""{"n":3,"b":true,"x":null}""");
        Assert.Equal(3, node!["n"]!.GetValue<int>());
        Assert.True(node["b"]!.GetValue<bool>());
        Assert.Null(node["x"]);
    }
}
```

- [ ] **Step 2: Placeholders**

`src/CustomNotch.Core/Config/Placeholders.cs` :
```csharp
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CustomNotch.Core.Config;

/// <summary>Où les secrets sont lus - secrets.json (DPAPI) dans l'application, un dictionnaire dans les tests.</summary>
public interface ISecretStore
{
    string? Get(string name);
}

/// <summary>Résolution de ${env:NAME}, ${secret:name} et ${home} dans toutes les chaînes d'un document JSON, pour que
/// cells.json reste portable : aucun chemin de machine ni secret n'y est écrit en clair.</summary>
public static partial class Placeholders
{
    [GeneratedRegex(@"\$\{(env|secret|home)(?::([^}]+))?\}")]
    private static partial Regex Pattern();

    public static (JsonNode? Result, List<string> Missing) Resolve(JsonNode? node, Func<string, string?> env, ISecretStore secrets, string home)
    {
        var missing = new List<string>();
        return (Walk(node, env, secrets, home, missing), missing);
    }

    private static JsonNode? Walk(JsonNode? node, Func<string, string?> env, ISecretStore secrets, string home, List<string> missing)
    {
        switch (node)
        {
            case JsonObject obj:
                var o = new JsonObject();
                foreach (var (k, v) in obj) o[k] = Walk(v, env, secrets, home, missing);
                return o;
            case JsonArray arr:
                var a = new JsonArray();
                foreach (var v in arr) a.Add(Walk(v, env, secrets, home, missing));
                return a;
            case JsonValue value when value.TryGetValue<string>(out var s):
                return JsonValue.Create(Pattern().Replace(s, m =>
                {
                    var kind = m.Groups[1].Value;
                    var name = m.Groups[2].Value;
                    var replacement = kind switch
                    {
                        "home" => home,
                        "env" => env(name),
                        _ => secrets.Get(name),
                    };
                    if (replacement is null) missing.Add($"{kind}:{name}");
                    return replacement ?? "";
                }));
            default:
                return node?.DeepClone();
        }
    }
}
```

- [ ] **Step 3: Tests de la fusion**

`tests/CustomNotch.Core.Tests/Config/ConfigMergeTests.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
namespace CustomNotch.Core.Tests.Config;

public class ConfigMergeTests
{
    private static JsonObject O(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void La_surcharge_locale_change_une_pilule_par_id_sans_toucher_aux_autres()
    {
        var shared = O("""{"pills":[{"id":"main","edge":"right","along":0.5,"cells":[{"id":"cpu","source":"system.cpu"}]},{"id":"side","edge":"left"}]}""");
        var local = O("""{"pills":[{"id":"main","along":0.2,"screen":"\\\\.\\DISPLAY2"}]}""");
        var merged = ConfigMerge.Merge(shared, local);
        var main = merged["pills"]![0]!;
        Assert.Equal(0.2, main["along"]!.GetValue<double>());
        Assert.Equal("right", main["edge"]!.GetValue<string>());
        Assert.Equal(@"\\.\DISPLAY2", main["screen"]!.GetValue<string>());
        Assert.Equal("cpu", main["cells"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("left", merged["pills"]![1]!["edge"]!.GetValue<string>());
    }

    [Fact]
    public void Une_cellule_est_surchargee_par_id_et_les_params_fusionnes_en_profondeur()
    {
        var shared = O("""{"pills":[{"id":"p","cells":[{"id":"disk","source":"system.disk","params":{"drive":"C:","x":1}},{"id":"b"}]}]}""");
        var local = O("""{"pills":[{"id":"p","cells":[{"id":"disk","visible":false,"params":{"drive":"D:"}}]}]}""");
        var disk = ConfigMerge.Merge(shared, local)["pills"]![0]!["cells"]![0]!;
        Assert.False(disk["visible"]!.GetValue<bool>());
        Assert.Equal("D:", disk["params"]!["drive"]!.GetValue<string>());
        Assert.Equal(1, disk["params"]!["x"]!.GetValue<int>());
    }

    [Fact]
    public void Une_pilule_inconnue_du_fichier_partage_est_ignoree()
    {
        var merged = ConfigMerge.Merge(O("""{"pills":[{"id":"p"}]}"""), O("""{"pills":[{"id":"ghost","edge":"top"}]}"""));
        Assert.Single(merged["pills"]!.AsArray());
    }

    [Fact]
    public void Sans_surcharge_le_document_est_rendu_tel_quel()
    {
        var shared = O("""{"version":1,"pills":[]}""");
        Assert.Equal(shared.ToJsonString(), ConfigMerge.Merge(shared, null).ToJsonString());
    }
}
```

- [ ] **Step 4: Fusion**

`src/CustomNotch.Core/Config/ConfigMerge.cs` :
```csharp
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Config;

/// <summary>cells.<machine>.json par-dessus cells.json : les objets se fusionnent clé par clé, les listes de pilules et
/// de cellules par « id ». Une pilule ou une cellule que le fichier partagé ne connaît pas est ignorée : la surcharge
/// locale ajuste, elle ne crée pas.</summary>
public static class ConfigMerge
{
    public static JsonObject Merge(JsonObject shared, JsonObject? local)
    {
        var result = shared.DeepClone().AsObject();
        if (local is null) return result;
        MergeObject(result, local);
        return result;
    }

    private static void MergeObject(JsonObject target, JsonObject overlay)
    {
        foreach (var (key, value) in overlay)
        {
            if (value is JsonObject o && target[key] is JsonObject t) MergeObject(t, o);
            else if (value is JsonArray a && target[key] is JsonArray ta && key is "pills" or "cells") MergeById(ta, a);
            else target[key] = value?.DeepClone();
        }
    }

    private static void MergeById(JsonArray target, JsonArray overlay)
    {
        foreach (var item in overlay.OfType<JsonObject>())
        {
            var id = item["id"]?.GetValue<string>();
            if (id is null) continue;
            var existing = target.OfType<JsonObject>().FirstOrDefault(x => x["id"]?.GetValue<string>() == id);
            if (existing is not null) MergeObject(existing, item);
        }
    }
}
```

- [ ] **Step 5: Tests de validation et du parseur**

`tests/CustomNotch.Core.Tests/Config/ConfigValidationTests.cs` :
```csharp
using CustomNotch.Core.Config;
namespace CustomNotch.Core.Tests.Config;

public class ConfigValidationTests
{
    private static readonly HashSet<string> Known = new() { "system.cpu", "launcher" };

    private static CellsFile File(string json) => CellsJson.ToFile(CellsJson.Parse(json));

    [Fact]
    public void Un_fichier_correct_ne_donne_aucune_erreur()
    {
        var f = File("""{"pills":[{"id":"p","edge":"right","cells":[{"id":"cpu","source":"system.cpu","refresh":"2s"},{"id":"g","children":["cpu"],"headline":"cpu"}]}]}""");
        Assert.Empty(ConfigValidation.Validate(f, Known));
    }

    [Fact]
    public void Les_fautes_sont_nommees()
    {
        var f = File("""{"pills":[{"id":"p","edge":"middle","along":3,"cells":[{"id":"a","source":"nope"},{"id":"a","source":"launcher","refresh":"vite","kind":"blob"},{"id":"g","children":["zz"],"headline":"a"}]},{"id":"p"}]}""");
        var errors = ConfigValidation.Validate(f, Known);
        Assert.Contains(errors, e => e.Contains("pills[1]") && e.Contains("id « p » en double"));
        Assert.Contains(errors, e => e.Contains("edge « middle »"));
        Assert.Contains(errors, e => e.Contains("along"));
        Assert.Contains(errors, e => e.Contains("source « nope » inconnue"));
        Assert.Contains(errors, e => e.Contains("cellule « a » en double"));
        Assert.Contains(errors, e => e.Contains("refresh « vite »"));
        Assert.Contains(errors, e => e.Contains("kind « blob »"));
        Assert.Contains(errors, e => e.Contains("enfant « zz » introuvable"));
        Assert.Contains(errors, e => e.Contains("headline « a » n'est pas un enfant"));
    }

    [Fact]
    public void Le_parseur_accepte_commentaires_et_virgules_finales()
    {
        var f = File("""
            {
              // la pilule principale
              "pills": [ { "id": "p", "cells": [], }, ],
            }
            """);
        Assert.Single(f.Pills);
    }

    [Fact]
    public void Un_json_illisible_leve_une_exception_lisible()
    {
        var ex = Assert.Throws<ConfigException>(() => File("{"));
        Assert.Contains("JSON", ex.Message);
    }
}
```

- [ ] **Step 6: Parseur et validation**

`src/CustomNotch.Core/Config/CellsJson.cs` :
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CustomNotch.Core.Config;

public sealed class ConfigException : Exception
{
    public ConfigException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Lecture de cells.json : commentaires et virgules finales tolérés (c'est un fichier que l'on édite à la main),
/// noms de propriétés en camelCase, casse indifférente.</summary>
public static class CellsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static JsonObject Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text, null, DocOptions) as JsonObject ?? throw new ConfigException("JSON : un objet { … } était attendu à la racine.");
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"JSON illisible : {ex.Message}", ex);
        }
    }

    public static CellsFile ToFile(JsonObject node)
    {
        try
        {
            return node.Deserialize<CellsFile>(Options) ?? new CellsFile();
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Configuration illisible : {ex.Message}", ex);
        }
    }

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
}
```

`src/CustomNotch.Core/Config/ConfigValidation.cs` :
```csharp
namespace CustomNotch.Core.Config;

/// <summary>Une configuration fausse est refusée en bloc, avec le chemin du champ : jamais appliquée à moitié.</summary>
public static class ConfigValidation
{
    private static readonly string[] Edges = { "left", "right", "top", "bottom" };
    private static readonly string[] Kinds = { "ring", "value", "status", "sparkline", "group" };

    public static List<string> Validate(CellsFile file, IReadOnlySet<string> knownSources)
    {
        var errors = new List<string>();
        var pillIds = new HashSet<string>();
        var cellIds = new HashSet<string>();
        for (var i = 0; i < file.Pills.Count; i++)
        {
            var pill = file.Pills[i];
            var at = $"pills[{i}]";
            if (string.IsNullOrWhiteSpace(pill.Id)) errors.Add($"{at} : id manquant");
            else if (!pillIds.Add(pill.Id)) errors.Add($"{at} : id « {pill.Id} » en double");
            if (!Edges.Contains(pill.Edge)) errors.Add($"{at}.edge « {pill.Edge} » : attendu left, right, top ou bottom");
            if (pill.Along is < 0 or > 1) errors.Add($"{at}.along : attendu entre 0 et 1");
            if (pill.Scale is < 0.5 or > 3) errors.Add($"{at}.scale : attendu entre 0.5 et 3");
            for (var j = 0; j < pill.Cells.Count; j++)
            {
                var cell = pill.Cells[j];
                var cat = $"{at}.cells[{j}]";
                if (string.IsNullOrWhiteSpace(cell.Id)) errors.Add($"{cat} : id manquant");
                else if (!cellIds.Add(cell.Id)) errors.Add($"{cat} : cellule « {cell.Id} » en double");
                if (!cell.IsGroup && !knownSources.Contains(cell.Source)) errors.Add($"{cat}.source « {cell.Source} » inconnue");
                if (cell.Refresh is not null && cell.RefreshSpan() is null) errors.Add($"{cat}.refresh « {cell.Refresh} » : attendu 500ms, 2s, 5m ou 1h");
                if (cell.Kind is not null && !Kinds.Contains(cell.Kind.ToLowerInvariant())) errors.Add($"{cat}.kind « {cell.Kind} » : attendu ring, value, status, sparkline ou group");
            }
        }
        foreach (var cell in file.AllCells().Where(c => c.IsGroup))
        {
            foreach (var child in cell.Children!)
                if (!cellIds.Contains(child)) errors.Add($"cellule « {cell.Id} » : enfant « {child} » introuvable");
            if (cell.Headline is { } h && !cell.Children!.Contains(h)) errors.Add($"cellule « {cell.Id} » : headline « {h} » n'est pas un enfant");
        }
        return errors;
    }
}
```

- [ ] **Step 7: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(core): placeholders, fusion locale et validation de cells.json"
```

---

### Task 4: ConfigStore — trois fichiers, rechargement à chaud, écriture locale

**Files:**
- Create: `src/CustomNotch.Core/Config/AppConfig.cs`, `Config/SecretsFile.cs`, `Config/DefaultCells.cs`, `Config/ConfigStore.cs`
- Test: `tests/CustomNotch.Core.Tests/Config/ConfigStoreTests.cs`

**Interfaces:**
- Consumes: `CellsJson`, `ConfigMerge`, `Placeholders`, `ConfigValidation`, `Json.WriteAtomic`, `Secrets.Seal/Unseal`, `Paths`.
- Produces: `AppConfig` (`ctor(path)`, `GetString(key, fallback)`, `GetBool`, `GetDouble`, `GetInt`, `Set(key, value)`, `Save()`, clés à points), `SecretsFile : ISecretStore` (`Get`, `Set(name, value)`, `Remove`, `Names`), `DefaultCells.Json()`, `ConfigStore` : `ctor(string home, IReadOnlySet<string> knownSources)`, `Current`, `App`, `Secrets`, `CellsPath`, `LocalPath`, `bool Load()`, `StartWatching()`, `event Action<CellsFile> Changed`, `event Action<string> Rejected`, `bool SetPillLocal(string pillId, Action<JsonObject> mutate)`, `LastErrors`, `Dispose()`.

- [ ] **Step 1: AppConfig et SecretsFile**

`src/CustomNotch.Core/Config/AppConfig.cs` :
```csharp
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Config;

/// <summary>config.json : l'état de l'application (chemin de cells.json, thème, langue, mises à jour). Clés à points,
/// même petit contrat que la Config de ClickUp-Extended pour que Theme.cs se copie tel quel.</summary>
public sealed class AppConfig
{
    private readonly string _path;
    private readonly JsonObject _root;

    public AppConfig(string path)
    {
        _path = path;
        _root = Load(path);
    }

    private static JsonObject Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o) return o;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            Log.Warning("config", $"config.json illisible, valeurs par défaut : {ex.Message}");
        }
        return new JsonObject();
    }

    private JsonNode? Node(string dotted)
    {
        JsonNode? node = _root;
        foreach (var part in dotted.Split('.'))
        {
            node = node is JsonObject o ? o[part] : null;
            if (node is null) return null;
        }
        return node;
    }

    public string GetString(string key, string fallback = "") => Node(key) is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;
    public bool GetBool(string key, bool fallback = false) => Node(key) is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
    public double GetDouble(string key, double fallback = 0) => Node(key) is JsonValue v && v.TryGetValue<double>(out var d) ? d : fallback;
    public int GetInt(string key, int fallback = 0) => (int)GetDouble(key, fallback);

    public void Set(string key, object? value)
    {
        var parts = key.Split('.');
        var node = _root;
        foreach (var part in parts[..^1])
        {
            if (node[part] is not JsonObject child) node[part] = child = new JsonObject();
            node = child;
        }
        node[parts[^1]] = value is null ? null : JsonValue.Create(value);
    }

    public bool Save() => Json.WriteAtomic(_path, Json.Format(_root), "config");
}
```

`src/CustomNotch.Core/Config/SecretsFile.cs` :
```csharp
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Config;

/// <summary>secrets.json : { "clickup_token": "dpapi:…" }. Jamais synchronisé, chiffré avec le compte Windows ; un blob
/// d'une autre machine se lit vide et la fenêtre de réglages redemande la valeur.</summary>
public sealed class SecretsFile : ISecretStore
{
    private readonly string _path;
    private readonly JsonObject _root;

    public SecretsFile(string path)
    {
        _path = path;
        _root = new JsonObject();
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o) _root = o;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            Log.Warning("secrets", $"secrets.json illisible : {ex.Message}");
        }
    }

    public IEnumerable<string> Names => _root.Select(kv => kv.Key).ToList();

    public string? Get(string name)
    {
        if (_root[name] is not JsonValue v || !v.TryGetValue<string>(out var stored)) return null;
        var clear = Secrets.Unseal(stored);
        return clear.Length == 0 ? null : clear;
    }

    public bool Set(string name, string value)
    {
        _root[name] = Secrets.Seal(value);
        return Json.WriteAtomic(_path, Json.Format(_root), "secrets");
    }

    public bool Remove(string name)
    {
        _root.Remove(name);
        return Json.WriteAtomic(_path, Json.Format(_root), "secrets");
    }
}
```

- [ ] **Step 2: Configuration par défaut**

`src/CustomNotch.Core/Config/DefaultCells.cs` :
```csharp
namespace CustomNotch.Core.Config;

/// <summary>Le cells.json du premier lancement : une pilule à droite, système + un lanceur. Assez pour voir que tout
/// marche, et un modèle à copier.</summary>
public static class DefaultCells
{
    public static string Json() => """
        {
          // customNotch - configuration des pilules. Ce fichier est portable : pas de chemin machine, pas de secret.
          // Placeholders : ${env:NAME}, ${secret:name}, ${home}. Surcharge locale : cells.<machine>.json.
          "version": 1,
          "pills": [
            {
              "id": "main",
              "edge": "right",
              "along": 0.5,
              "cells": [
                { "id": "cpu",  "source": "system.cpu",     "label": "CPU",     "glyph": "cpu",     "refresh": "2s" },
                { "id": "mem",  "source": "system.memory",  "label": "Mémoire", "glyph": "memory",  "refresh": "5s" },
                { "id": "disk", "source": "system.disk",    "label": "Disque",  "glyph": "disk",    "refresh": "1m", "params": { "drive": "C:" } },
                { "id": "net",  "source": "system.network", "label": "Réseau",  "glyph": "network", "refresh": "2s" },
                { "id": "clickup", "source": "launcher", "label": "ClickUp", "glyph": "link", "params": { "open": "https://app.clickup.com" } }
              ]
            }
          ]
        }
        """;
}
```

- [ ] **Step 3: Tests du ConfigStore**

`tests/CustomNotch.Core.Tests/Config/ConfigStoreTests.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core;
using CustomNotch.Core.Config;
namespace CustomNotch.Core.Tests.Config;

public class ConfigStoreTests : IDisposable
{
    private static readonly HashSet<string> Known = new() { "system.cpu", "system.memory", "system.disk", "system.network", "launcher", "http" };
    private readonly string _home = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8]);

    public ConfigStoreTests() => Directory.CreateDirectory(_home);
    public void Dispose() { try { Directory.Delete(_home, true); } catch (IOException) { } }

    [Fact]
    public void Le_premier_lancement_ecrit_un_cells_json_par_defaut()
    {
        var store = new ConfigStore(_home, Known);
        Assert.True(store.Load());
        Assert.True(File.Exists(Path.Combine(_home, "cells.json")));
        Assert.Single(store.Current.Pills);
        Assert.Equal("cpu", store.Current.Pills[0].Cells[0].Id);
    }

    [Fact]
    public void Le_chemin_de_cells_json_vient_de_config_json()
    {
        var elsewhere = Path.Combine(_home, "sync", "notch.json");
        var app = new AppConfig(Paths.ConfigFile(_home));
        app.Set("cells_path", elsewhere);
        app.Save();
        var store = new ConfigStore(_home, Known);
        store.Load();
        Assert.Equal(elsewhere, store.CellsPath);
        Assert.True(File.Exists(elsewhere));
    }

    [Fact]
    public void La_surcharge_locale_et_les_secrets_sont_appliques()
    {
        File.WriteAllText(Path.Combine(_home, "cells.json"), """{"pills":[{"id":"p","along":0.5,"cells":[{"id":"h","source":"http","params":{"url":"https://x","headers":{"Authorization":"Bearer ${secret:tok}"}}}]}]}""");
        File.WriteAllText(Paths.LocalCellsFile(_home), """{"pills":[{"id":"p","along":0.1}]}""");
        var store = new ConfigStore(_home, Known);
        store.Secrets.Set("tok", "abc");
        Assert.True(store.Load());
        Assert.Equal(0.1, store.Current.Pills[0].Along);
        Assert.Equal("Bearer abc", store.Current.Pills[0].Cells[0].Params!["headers"]!["Authorization"]!.GetValue<string>());
    }

    [Fact]
    public void Une_configuration_invalide_est_refusee_et_la_precedente_reste()
    {
        var store = new ConfigStore(_home, Known);
        store.Load();
        var before = store.Current;
        string? rejected = null;
        store.Rejected += m => rejected = m;
        File.WriteAllText(store.CellsPath, """{"pills":[{"id":"p","edge":"nowhere"}]}""");
        Assert.False(store.Load());
        Assert.Same(before, store.Current);
        Assert.Contains("edge", rejected);
    }

    [Fact]
    public void SetPillLocal_ecrit_dans_la_surcharge_pas_dans_le_partage()
    {
        var store = new ConfigStore(_home, Known);
        store.Load();
        var sharedBefore = File.ReadAllText(store.CellsPath);
        store.SetPillLocal("main", p => { p["along"] = 0.9; p["screen"] = @"\\.\DISPLAY2"; });
        Assert.Equal(sharedBefore, File.ReadAllText(store.CellsPath));
        var local = JsonNode.Parse(File.ReadAllText(store.LocalPath))!;
        Assert.Equal(0.9, local["pills"]![0]!["along"]!.GetValue<double>());
        Assert.Equal(0.9, store.Current.Pills[0].Along);
    }

    [Fact]
    public async Task Le_watcher_recharge_apres_une_ecriture()
    {
        using var store = new ConfigStore(_home, Known);
        store.Load();
        var changed = new TaskCompletionSource<CellsFile>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += f => changed.TrySetResult(f);
        store.StartWatching();
        await Task.Delay(200);
        File.WriteAllText(store.CellsPath, """{"pills":[{"id":"only","cells":[]}]}""");
        var result = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("only", result.Pills[0].Id);
    }
}
```

- [ ] **Step 4: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: erreurs de compilation (`ConfigStore` introuvable).

- [ ] **Step 5: ConfigStore**

`src/CustomNotch.Core/Config/ConfigStore.cs` :
```csharp
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Config;

/// <summary>Les trois fichiers et leur cycle : cells.json (partagé, où l'utilisateur veut) + cells.<machine>.json (local)
/// + secrets.json → un CellsFile validé. Surveillance avec anti-rebond ; une version fausse est refusée et l'ancienne
/// reste en service. L'application n'écrit le fichier partagé que depuis Settings ; ce que l'app décide seule
/// (position après un drag, écran, visibilité) va dans la surcharge locale.</summary>
public sealed class ConfigStore : IDisposable
{
    private readonly string _home;
    private readonly IReadOnlySet<string> _knownSources;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _lock = new();
    private System.Threading.Timer? _debounce;

    public event Action<CellsFile>? Changed;
    public event Action<string>? Rejected;

    public ConfigStore(string home, IReadOnlySet<string> knownSources)
    {
        _home = home;
        _knownSources = knownSources;
        Directory.CreateDirectory(home);
        App = new AppConfig(Paths.ConfigFile(home));
        Secrets = new SecretsFile(Paths.SecretsFile(home));
        var custom = App.GetString("cells_path");
        CellsPath = custom.Length > 0 ? Path.GetFullPath(custom) : Paths.DefaultCellsFile(home);
        LocalPath = Paths.LocalCellsFile(home);
        Current = new CellsFile();
    }

    public AppConfig App { get; }
    public SecretsFile Secrets { get; }
    public string CellsPath { get; }
    public string LocalPath { get; }
    public CellsFile Current { get; private set; }
    public IReadOnlyList<string> LastErrors { get; private set; } = Array.Empty<string>();

    /// <summary>Lit, fusionne, résout, valide. Rend true si une nouvelle configuration est en service.</summary>
    public bool Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(CellsPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CellsPath)!);
                    Json.WriteAtomic(CellsPath, DefaultCells.Json(), "config");
                    Log.Info("config", $"cells.json créé : {CellsPath}");
                }
                var shared = CellsJson.Parse(File.ReadAllText(CellsPath));
                var local = File.Exists(LocalPath) ? CellsJson.Parse(File.ReadAllText(LocalPath)) : null;
                var merged = ConfigMerge.Merge(shared, local);
                var (resolved, missing) = Placeholders.Resolve(merged, Environment.GetEnvironmentVariable, Secrets, _home);
                foreach (var m in missing) Log.Warning("config", $"placeholder ${{{m}}} sans valeur");
                var file = CellsJson.ToFile(resolved!.AsObject());
                var errors = ConfigValidation.Validate(file, _knownSources);
                LastErrors = errors;
                if (errors.Count > 0)
                {
                    var message = $"{Path.GetFileName(CellsPath)} refusé : " + string.Join(" ; ", errors);
                    Log.Warning("config", message);
                    Rejected?.Invoke(message);
                    return false;
                }
                Current = file;
                Changed?.Invoke(file);
                return true;
            }
            catch (Exception ex) when (ex is ConfigException or IOException or UnauthorizedAccessException)
            {
                LastErrors = new[] { ex.Message };
                Log.Warning("config", ex.Message);
                Rejected?.Invoke(ex.Message);
                return false;
            }
        }
    }

    public void StartWatching()
    {
        foreach (var path in new[] { CellsPath, LocalPath, Paths.SecretsFile(_home) }.Distinct())
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var w = new FileSystemWatcher(dir, Path.GetFileName(path)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            w.Changed += (_, _) => Bump();
            w.Created += (_, _) => Bump();
            w.Renamed += (_, _) => Bump();
            w.EnableRaisingEvents = true;
            _watchers.Add(w);
        }
    }

    /// <summary>Un éditeur écrit en plusieurs fois : on attend 300 ms de calme avant de relire.</summary>
    private void Bump()
    {
        lock (_lock)
        {
            _debounce ??= new System.Threading.Timer(_ => Load(), null, Timeout.Infinite, Timeout.Infinite);
            _debounce.Change(300, Timeout.Infinite);
        }
    }

    /// <summary>Modifie une pilule dans la surcharge locale (créée au besoin) puis recharge.</summary>
    public bool SetPillLocal(string pillId, Action<JsonObject> mutate)
    {
        lock (_lock)
        {
            JsonObject root;
            try { root = File.Exists(LocalPath) ? CellsJson.Parse(File.ReadAllText(LocalPath)) : new JsonObject(); }
            catch (ConfigException) { root = new JsonObject(); }
            if (root["pills"] is not JsonArray pills) root["pills"] = pills = new JsonArray();
            var pill = pills.OfType<JsonObject>().FirstOrDefault(p => p["id"]?.GetValue<string>() == pillId);
            if (pill is null) pills.Add(pill = new JsonObject { ["id"] = pillId });
            mutate(pill);
            var ok = Json.WriteAtomic(LocalPath, Json.Format(root), "config");
            Load();
            return ok;
        }
    }

    public void Dispose()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        _debounce?.Dispose();
    }
}
```

- [ ] **Step 6: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: PASS (le test du watcher prend ~1 s).

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(core): ConfigStore à trois fichiers, rechargement à chaud, surcharge locale"
```

---

### Task 5: Contrat de source, registre, ReadingStore, ordonnanceur

**Files:**
- Create: `src/CustomNotch.Core/Sources/ISource.cs`, `Sources/SourceRegistry.cs`, `Sources/ReadingStore.cs`, `Sources/Scheduler.cs`, `src/CustomNotch.Core/Platform/Idle.cs`
- Test: `tests/CustomNotch.Core.Tests/Sources/SchedulerTests.cs`

**Interfaces:**
- Consumes: `Reading`, `CellsFile`, `CellConfig`.
- Produces: `record CellContext(string CellId, JsonObject Params, JsonObject? Globals)` avec `string? Str(string key)`, `double? Num(string key)`, `bool Flag(string key, bool fallback)` ; `record SchemaField(string Name, string Type, string Label, bool Required = false, string? Help = null, string? Default = null)` ; `record SourceSchema(string Type, string Title, IReadOnlyList<SchemaField> Fields, string? DefaultGlyph = null)` ; `interface ISource { string Type; SourceSchema Schema; TimeSpan DefaultRefresh; Task<Reading> ReadAsync(CellContext, CancellationToken); Task InvokeAsync(string action, CellContext, CancellationToken); event Action<string>? Pushed; }` ; `abstract class SourceBase : ISource` (Pushed + `Push(cellId)` + InvokeAsync vide) ; `SourceRegistry` (`Register(ISource)`, `ISource? Get(type)`, `IReadOnlySet<string> Types`) ; `ReadingStore` (`Reading? Get(id)`, `void Set(id, Reading)`, `event Action<string>? Changed`) ; `Scheduler` (`ctor(SourceRegistry, ReadingStore, Func<long> nowMs, Func<long> idleMs)`, `Apply(CellsFile)`, `RefreshNow(cellId)`, `Task InvokeAsync(cellId, action)`, `Dispose()`).

- [ ] **Step 1: Contrat**

`src/CustomNotch.Core/Sources/ISource.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Ce qu'une source reçoit : l'id de la cellule, ses params, et les réglages globaux de son type
/// (cells.json → "sources": { "clickup": { … } }).</summary>
public sealed record CellContext(string CellId, JsonObject Params, JsonObject? Globals)
{
    public string? Str(string key) => Params[key] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s
        : Globals?[key] is JsonValue g && g.TryGetValue<string>(out var gs) && gs.Length > 0 ? gs : null;
    public double? Num(string key) => Params[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    public bool Flag(string key, bool fallback = false) => Params[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
}

/// <summary>Un champ de paramètre : la fenêtre de réglages en fait un formulaire, la validation le vérifie.
/// Type : string | number | bool | secret | path | url | choice.</summary>
public sealed record SchemaField(string Name, string Type, string Label, bool Required = false, string? Help = null, string? Default = null, IReadOnlyList<string>? Choices = null);

public sealed record SourceSchema(string Type, string Title, IReadOnlyList<SchemaField> Fields, string? DefaultGlyph = null);

/// <summary>Le contrat de toute source : lire, agir, et pousser une mise à jour hors cadence.</summary>
public interface ISource
{
    string Type { get; }
    SourceSchema Schema { get; }
    TimeSpan DefaultRefresh { get; }
    Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct);
    Task InvokeAsync(string action, CellContext ctx, CancellationToken ct);
    /// <summary>« Relis cette cellule maintenant » (hook Claude, changement de piste média…).</summary>
    event Action<string>? Pushed;
}

public abstract class SourceBase : ISource
{
    public abstract string Type { get; }
    public abstract SourceSchema Schema { get; }
    public virtual TimeSpan DefaultRefresh => TimeSpan.FromSeconds(30);
    public abstract Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct);
    public virtual Task InvokeAsync(string action, CellContext ctx, CancellationToken ct) => Task.CompletedTask;
    public event Action<string>? Pushed;
    protected void Push(string cellId) => Pushed?.Invoke(cellId);
}
```

`src/CustomNotch.Core/Sources/SourceRegistry.cs` :
```csharp
namespace CustomNotch.Core.Sources;

public sealed class SourceRegistry
{
    private readonly Dictionary<string, ISource> _sources = new(StringComparer.Ordinal);

    public void Register(ISource source) => _sources[source.Type] = source;
    public ISource? Get(string type) => _sources.GetValueOrDefault(type);
    public IReadOnlySet<string> Types => _sources.Keys.ToHashSet();
    public IEnumerable<ISource> All => _sources.Values;
}
```

`src/CustomNotch.Core/Sources/ReadingStore.cs` :
```csharp
using System.Collections.Concurrent;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>La dernière lecture de chaque cellule. Changed est levé sur le thread de la source : l'interface repasse par
/// son Dispatcher.</summary>
public sealed class ReadingStore
{
    private readonly ConcurrentDictionary<string, Reading> _readings = new();
    public event Action<string>? Changed;

    public Reading? Get(string cellId) => _readings.GetValueOrDefault(cellId);

    public void Set(string cellId, Reading reading)
    {
        _readings[cellId] = reading;
        Changed?.Invoke(cellId);
    }

    public void Remove(string cellId) => _readings.TryRemove(cellId, out _);
}
```

`src/CustomNotch.Core/Platform/Idle.cs` : copier la classe `Idle` (et elle seule : `LastInputInfo`, `GetLastInputInfo`, `GetTickCount`, `Ms()`) depuis `ClickUp-Extended\src\ClickUpExtended.Core\Platform.cs`, namespace `CustomNotch.Core.Platform`.

- [ ] **Step 2: Tests de l'ordonnanceur**

`tests/CustomNotch.Core.Tests/Sources/SchedulerTests.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Sources;

public class SchedulerTests
{
    private sealed class FakeSource : SourceBase
    {
        public int Reads;
        public Func<Reading>? Produce;
        public string? LastAction;
        public override string Type => "fake";
        public override SourceSchema Schema => new("fake", "Fake", Array.Empty<SchemaField>());
        public override TimeSpan DefaultRefresh => TimeSpan.FromMilliseconds(50);
        public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
        {
            Interlocked.Increment(ref Reads);
            return Task.FromResult(Produce?.Invoke() ?? new Reading(Value: Reads));
        }
        public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct) { LastAction = action; return Task.CompletedTask; }
        public void Fire(string id) => Push(id);
    }

    private static CellsFile File(params string[] ids) => new()
    {
        Pills = { new PillConfig { Id = "p", Cells = ids.Select(i => new CellConfig { Id = i, Source = "fake" }).ToList() } },
    };

    private static (Scheduler, FakeSource, ReadingStore) Make(Func<long>? idle = null)
    {
        var registry = new SourceRegistry();
        var source = new FakeSource();
        registry.Register(source);
        var store = new ReadingStore();
        return (new Scheduler(registry, store, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), idle ?? (() => 0)), source, store);
    }

    [Fact]
    public async Task Chaque_cellule_est_lue_a_sa_cadence()
    {
        var (scheduler, source, store) = Make();
        using (scheduler)
        {
            scheduler.Apply(File("a"));
            await Task.Delay(300);
            Assert.InRange(source.Reads, 3, 10);
            Assert.NotNull(store.Get("a"));
        }
    }

    [Fact]
    public async Task Une_panne_marque_la_derniere_lecture_perimee_sans_la_perdre()
    {
        var (scheduler, source, store) = Make();
        using (scheduler)
        {
            source.Produce = () => new Reading(Value: 42);
            scheduler.Apply(File("a"));
            await Task.Delay(120);
            source.Produce = () => throw new InvalidOperationException("réseau HS");
            await Task.Delay(200);
            var r = store.Get("a")!;
            Assert.Equal(42, r.Value);
            Assert.NotNull(r.StaleSinceMs);
            Assert.Equal("réseau HS", r.Error);
        }
    }

    [Fact]
    public async Task Les_pannes_repetees_espacent_les_lectures()
    {
        var (scheduler, source, _) = Make();
        using (scheduler)
        {
            source.Produce = () => throw new InvalidOperationException("x");
            scheduler.Apply(File("a"));
            await Task.Delay(400);
            // 50 ms, 100, 200, 400… : au plus 4 lectures en 400 ms au lieu de 8
            Assert.InRange(source.Reads, 2, 5);
        }
    }

    [Fact]
    public async Task Une_cellule_retiree_de_la_config_s_arrete()
    {
        var (scheduler, source, store) = Make();
        using (scheduler)
        {
            scheduler.Apply(File("a", "b"));
            await Task.Delay(120);
            scheduler.Apply(File("a"));
            var reads = source.Reads;
            await Task.Delay(150);
            Assert.Null(store.Get("b"));
            Assert.True(source.Reads > reads, "a continue");
        }
    }

    [Fact]
    public async Task Push_et_RefreshNow_relisent_sans_attendre()
    {
        var (scheduler, source, _) = Make();
        using (scheduler)
        {
            scheduler.Apply(new CellsFile { Pills = { new PillConfig { Id = "p", Cells = { new CellConfig { Id = "a", Source = "fake", Refresh = "1h" } } } } });
            await Task.Delay(100);
            Assert.Equal(1, source.Reads);
            source.Fire("a");
            await Task.Delay(100);
            Assert.Equal(2, source.Reads);
            scheduler.RefreshNow("a");
            await Task.Delay(100);
            Assert.Equal(3, source.Reads);
        }
    }

    [Fact]
    public async Task Inactif_les_cadences_courtes_passent_a_30_s()
    {
        var (scheduler, source, _) = Make(idle: () => 10 * 60_000);
        using (scheduler)
        {
            scheduler.Apply(File("a"));
            await Task.Delay(250);
            Assert.Equal(1, source.Reads);
        }
    }

    [Fact]
    public async Task Invoke_atteint_la_source_avec_les_params_de_la_cellule()
    {
        var (scheduler, source, _) = Make();
        using (scheduler)
        {
            scheduler.Apply(File("a"));
            await scheduler.InvokeAsync("a", "toggle");
            Assert.Equal("toggle", source.LastAction);
        }
    }
}
```

- [ ] **Step 3: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: erreurs de compilation (`Scheduler` introuvable).

- [ ] **Step 4: Ordonnanceur**

`src/CustomNotch.Core/Sources/Scheduler.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Une boucle par cellule à sa cadence. Une source qui échoue laisse sa dernière lecture, marquée périmée,
/// et ses lectures s'espacent (×2, plafond 10 min) jusqu'au prochain succès. Une cellule-groupe n'a pas de boucle :
/// ses enfants en ont. À l'inactivité (> 5 min sans souris ni clavier) les cadences sous 30 s passent à 30 s.</summary>
public sealed class Scheduler : IDisposable
{
    private static readonly TimeSpan BackoffCap = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleFloor = TimeSpan.FromSeconds(30);

    private sealed class Loop
    {
        public required CellConfig Cell;
        public required ISource Source;
        public required CellContext Context;
        public required TimeSpan Refresh;
        public readonly CancellationTokenSource Stop = new();
        public readonly SemaphoreSlim Wake = new(0);
        public int Failures;
        public Task? Task;
    }

    private readonly SourceRegistry _registry;
    private readonly ReadingStore _store;
    private readonly Func<long> _now;
    private readonly Func<long> _idleMs;
    private readonly Dictionary<string, Loop> _loops = new();
    private readonly object _lock = new();
    private JsonObject? _globals;

    public Scheduler(SourceRegistry registry, ReadingStore store, Func<long> nowMs, Func<long> idleMs)
    {
        _registry = registry;
        _store = store;
        _now = nowMs;
        _idleMs = idleMs;
        foreach (var source in registry.All) source.Pushed += RefreshNow;
    }

    /// <summary>Met les boucles en accord avec la config : celles dont la cellule a changé (source, params, cadence)
    /// redémarrent, les autres continuent, les disparues s'arrêtent.</summary>
    public void Apply(CellsFile file)
    {
        lock (_lock)
        {
            _globals = file.Sources;
            var wanted = file.AllCells().Where(c => !c.IsGroup).ToDictionary(c => c.Id);
            foreach (var id in _loops.Keys.Where(id => !wanted.ContainsKey(id) || Signature(wanted[id]) != Signature(_loops[id].Cell)).ToList())
            {
                _loops[id].Stop.Cancel();
                _loops.Remove(id);
                if (!wanted.ContainsKey(id)) _store.Remove(id);
            }
            foreach (var cell in wanted.Values.Where(c => !_loops.ContainsKey(c.Id)))
            {
                var source = _registry.Get(cell.Source);
                if (source is null) continue;
                var loop = new Loop
                {
                    Cell = cell, Source = source,
                    Context = new CellContext(cell.Id, cell.Params ?? new JsonObject(), _globals?[cell.Source] as JsonObject),
                    Refresh = cell.RefreshSpan() ?? source.DefaultRefresh,
                };
                loop.Task = Task.Run(() => RunAsync(loop));
                _loops[cell.Id] = loop;
            }
        }
    }

    private static string Signature(CellConfig c) => $"{c.Source}|{c.Refresh}|{c.Params?.ToJsonString()}";

    private async Task RunAsync(Loop loop)
    {
        var ct = loop.Stop.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var reading = await loop.Source.ReadAsync(loop.Context, ct).ConfigureAwait(false);
                loop.Failures = 0;
                _store.Set(loop.Cell.Id, reading.Fresh());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                loop.Failures = Math.Min(loop.Failures + 1, 20);
                var previous = _store.Get(loop.Cell.Id) ?? Reading.Empty;
                _store.Set(loop.Cell.Id, previous.AsStale(_now(), ex.Message));
                Log.Warning("source", $"{loop.Cell.Id} ({loop.Source.Type}) : {ex.Message}");
            }
            var wait = loop.Refresh;
            if (loop.Failures > 0)
            {
                var factor = Math.Pow(2, loop.Failures - 1);
                wait = TimeSpan.FromMilliseconds(Math.Min(loop.Refresh.TotalMilliseconds * factor, BackoffCap.TotalMilliseconds));
            }
            if (_idleMs() > IdleAfter.TotalMilliseconds && wait < IdleFloor) wait = IdleFloor;
            try { await loop.Wake.WaitAsync(wait, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    public void RefreshNow(string cellId)
    {
        lock (_lock)
        {
            if (_loops.TryGetValue(cellId, out var loop))
            {
                loop.Failures = 0;
                if (loop.Wake.CurrentCount == 0) loop.Wake.Release();
            }
        }
    }

    public async Task InvokeAsync(string cellId, string action)
    {
        Loop? loop;
        lock (_lock) _loops.TryGetValue(cellId, out loop);
        if (loop is null) return;
        try
        {
            await loop.Source.InvokeAsync(action, loop.Context, loop.Stop.Token).ConfigureAwait(false);
            RefreshNow(cellId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning("action", $"{cellId}.{action} : {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var loop in _loops.Values) loop.Stop.Cancel();
            _loops.Clear();
        }
        foreach (var source in _registry.All) source.Pushed -= RefreshNow;
    }
}
```

- [ ] **Step 5: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: PASS. Si `Les_pannes_repetees_espacent_les_lectures` est instable sur une machine chargée, élargir la borne haute à 6 ; ne pas retirer le test.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(core): contrat ISource, registre, ReadingStore et ordonnanceur"
```

---

### Task 6: Sources système — CPU, mémoire, disque, réseau, batterie

**Files:**
- Create: `src/CustomNotch.Core/Platform/SystemInfo.cs`, `src/CustomNotch.Core/Sources/System/Units.cs`, `Sources/System/CpuSource.cs`, `MemorySource.cs`, `DiskSource.cs`, `NetworkSource.cs`, `BatterySource.cs`, `src/CustomNotch.Core/Sources/System/SystemSources.cs`
- Test: `tests/CustomNotch.Core.Tests/Sources/UnitsTests.cs`, `tests/CustomNotch.Core.Tests/Sources/SystemSourcesTests.cs`

**Interfaces:**
- Consumes: `SourceBase`, `Reading`, `DetailRow`, `Status`.
- Produces: `SystemInfo.CpuTimes() → (ulong Idle, ulong Kernel, ulong User)?`, `SystemInfo.Memory() → (ulong Total, ulong Available)?`, `SystemInfo.Power() → (bool HasBattery, bool OnAc, int Percent, int SecondsLeft)?` ; `Units.Gb(ulong bytes)`, `Units.Rate(ulong deltaBytes, double seconds) → (double Value, string Unit)`, `Units.CpuPercent(prev, cur)` ; sources `system.cpu`, `system.memory`, `system.disk`, `system.network`, `system.battery` ; `SystemSources.RegisterAll(SourceRegistry)`.

- [ ] **Step 1: Tests des conversions**

`tests/CustomNotch.Core.Tests/Sources/UnitsTests.cs` :
```csharp
using CustomNotch.Core.Sources.System;
namespace CustomNotch.Core.Tests.Sources;

public class UnitsTests
{
    [Fact]
    public void Les_octets_deviennent_des_gigaoctets_arrondis()
    {
        Assert.Equal(15.9, Units.Gb(17_070_000_000UL), 1);
        Assert.Equal(0, Units.Gb(0));
    }

    [Fact]
    public void Le_debit_choisit_son_unite()
    {
        Assert.Equal((512.0, "Ko/s"), Units.Rate(512 * 1024, 1));
        Assert.Equal((2.5, "Mo/s"), Units.Rate(5 * 1024 * 1024, 2));
        Assert.Equal((0.0, "Ko/s"), Units.Rate(0, 1));
    }

    [Fact]
    public void Le_cpu_est_la_part_non_inactive_du_temps()
    {
        var prev = (Idle: 100UL, Kernel: 200UL, User: 100UL);   // kernel inclut idle sous Windows
        var cur = (Idle: 150UL, Kernel: 300UL, User: 200UL);
        // Δkernel+Δuser = 200, Δidle = 50 → 75 %
        Assert.Equal(75, Units.CpuPercent(prev, cur), 1);
        Assert.Equal(0, Units.CpuPercent(prev, prev));
    }
}
```

- [ ] **Step 2: Conversions et P/Invoke**

`src/CustomNotch.Core/Sources/System/Units.cs` :
```csharp
namespace CustomNotch.Core.Sources.System;

public static class Units
{
    public static double Gb(ulong bytes) => Math.Round(bytes / 1_073_741_824.0, 1);

    /// <summary>Un débit lisible : Ko/s sous 1 Mo/s, Mo/s au-dessus.</summary>
    public static (double Value, string Unit) Rate(ulong deltaBytes, double seconds)
    {
        if (seconds <= 0) return (0, "Ko/s");
        var perSecond = deltaBytes / seconds;
        return perSecond >= 1024 * 1024 ? (Math.Round(perSecond / (1024 * 1024), 1), "Mo/s") : (Math.Round(perSecond / 1024, 0), "Ko/s");
    }

    /// <summary>GetSystemTimes : kernel contient idle ; l'occupation est 1 − Δidle / (Δkernel + Δuser).</summary>
    public static double CpuPercent((ulong Idle, ulong Kernel, ulong User) prev, (ulong Idle, ulong Kernel, ulong User) cur)
    {
        var total = (double)(cur.Kernel - prev.Kernel) + (cur.User - prev.User);
        if (total <= 0) return 0;
        var idle = (double)(cur.Idle - prev.Idle);
        return Math.Clamp((1 - idle / total) * 100, 0, 100);
    }
}
```

`src/CustomNotch.Core/Platform/SystemInfo.cs` :
```csharp
using System.Runtime.InteropServices;

namespace CustomNotch.Core.Platform;

/// <summary>Les trois lectures système que Windows donne sans compteur de performance ni WMI : temps CPU, mémoire,
/// alimentation. Hors Windows chaque fonction rend null et la source se dit indisponible.</summary>
public static partial class SystemInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime { public uint Low, High; public ulong Value => ((ulong)High << 32) | Low; }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    public static (ulong Idle, ulong Kernel, ulong User)? CpuTimes()
    {
        if (!OperatingSystem.IsWindows() || !GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        return (idle.Value, kernel.Value, user.Value);
    }

    public static (ulong Total, ulong Available)? Memory()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? (status.TotalPhys, status.AvailPhys) : null;
    }

    /// <summary>BatteryFlag 128 = pas de batterie ; BatteryLifePercent 255 = inconnu ; BatteryLifeTime −1 = inconnu.</summary>
    public static (bool HasBattery, bool OnAc, int Percent, int SecondsLeft)? Power()
    {
        if (!OperatingSystem.IsWindows() || !GetSystemPowerStatus(out var s)) return null;
        var has = (s.BatteryFlag & 128) == 0 && s.BatteryLifePercent != 255;
        return (has, s.AcLineStatus == 1, s.BatteryLifePercent == 255 ? 0 : s.BatteryLifePercent, s.BatteryLifeTime);
    }
}
```

- [ ] **Step 3: Tests des sources (de fumée, sous Windows)**

`tests/CustomNotch.Core.Tests/Sources/SystemSourcesTests.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
using CustomNotch.Core.Sources.System;
namespace CustomNotch.Core.Tests.Sources;

public class SystemSourcesTests
{
    private static CellContext Ctx(string json = "{}") => new("c", JsonNode.Parse(json)!.AsObject(), null);

    [Fact]
    public async Task Le_cpu_rend_un_pourcentage_apres_deux_lectures()
    {
        var src = new CpuSource();
        await src.ReadAsync(Ctx(), CancellationToken.None);
        await Task.Delay(100);
        var r = await src.ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal(100, r.Max);
        Assert.InRange(r.Value!.Value, 0, 100);
    }

    [Fact]
    public async Task La_memoire_donne_utilise_sur_total_en_go()
    {
        var r = await new MemorySource().ReadAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Max > 1);
        Assert.InRange(r.Value!.Value, 0, r.Max!.Value);
        Assert.Equal("Go", r.Unit);
        Assert.Contains(r.Detail!, d => d.Label == "Libre");
    }

    [Fact]
    public async Task Le_disque_lit_le_lecteur_demande_et_refuse_un_lecteur_absent()
    {
        var r = await new DiskSource().ReadAsync(Ctx("""{"drive":"C:"}"""), CancellationToken.None);
        Assert.True(r.Max > 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DiskSource().ReadAsync(Ctx("""{"drive":"Q:"}"""), CancellationToken.None));
    }

    [Fact]
    public async Task Le_reseau_a_un_historique_et_une_unite()
    {
        var src = new NetworkSource();
        await src.ReadAsync(Ctx(), CancellationToken.None);
        await Task.Delay(50);
        var r = await src.ReadAsync(Ctx(), CancellationToken.None);
        Assert.NotNull(r.Value);
        Assert.Contains(r.Unit, new[] { "Ko/s", "Mo/s" });
        Assert.NotEmpty(r.History!);
    }

    [Fact]
    public async Task La_batterie_est_off_sans_batterie_sinon_un_anneau_inverse()
    {
        var r = await new BatterySource().ReadAsync(Ctx(), CancellationToken.None);
        if (r.Status == Status.Off) Assert.Null(r.Value);
        else { Assert.Equal(100, r.Max); Assert.InRange(r.Value!.Value, 0, 100); }
    }

    [Fact]
    public void Tout_est_enregistre()
    {
        var registry = new SourceRegistry();
        SystemSources.RegisterAll(registry);
        Assert.Superset(new HashSet<string> { "system.cpu", "system.memory", "system.disk", "system.network", "system.battery" }, registry.Types.ToHashSet());
    }
}
```

- [ ] **Step 4: Sources**

`src/CustomNotch.Core/Sources/System/CpuSource.cs` :
```csharp
using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;

namespace CustomNotch.Core.Sources.System;

public sealed class CpuSource : SourceBase
{
    private (ulong Idle, ulong Kernel, ulong User)? _previous;
    private readonly Queue<(long, double)> _history = new();

    public override string Type => "system.cpu";
    public override SourceSchema Schema => new(Type, "Processeur", Array.Empty<SchemaField>(), "cpu");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(2);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var now = SystemInfo.CpuTimes() ?? throw new InvalidOperationException("Temps CPU indisponibles sur ce système.");
        var percent = _previous is { } prev ? Units.CpuPercent(prev, now) : 0;
        _previous = now;
        History.Keep(_history, percent, 60);
        return Task.FromResult(new Reading(Value: Math.Round(percent), Max: 100, Unit: "%",
            Detail: new[] { new DetailRow("Occupation", $"{Math.Round(percent)} %", percent / 100) },
            History: _history.ToList()));
    }
}

/// <summary>Un historique borné pour les sparklines : N derniers points, horodatés.</summary>
internal static class History
{
    public static void Keep(Queue<(long Ms, double V)> queue, double value, int max)
    {
        queue.Enqueue((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), value));
        while (queue.Count > max) queue.Dequeue();
    }
}
```

`src/CustomNotch.Core/Sources/System/MemorySource.cs` :
```csharp
using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;

namespace CustomNotch.Core.Sources.System;

public sealed class MemorySource : SourceBase
{
    public override string Type => "system.memory";
    public override SourceSchema Schema => new(Type, "Mémoire", Array.Empty<SchemaField>(), "memory");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(5);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var (total, available) = SystemInfo.Memory() ?? throw new InvalidOperationException("Mémoire indisponible sur ce système.");
        var used = total - available;
        return Task.FromResult(new Reading(Value: Units.Gb(used), Max: Units.Gb(total), Unit: "Go",
            Detail: new[]
            {
                new DetailRow("Utilisée", $"{Units.Gb(used)} Go", (double)used / total),
                new DetailRow("Libre", $"{Units.Gb(available)} Go"),
            }));
    }
}
```

`src/CustomNotch.Core/Sources/System/DiskSource.cs` :
```csharp
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.System;

public sealed class DiskSource : SourceBase
{
    public override string Type => "system.disk";
    public override SourceSchema Schema => new(Type, "Disque", new[] { new SchemaField("drive", "string", "Lecteur", Required: true, Help: "C:", Default: "C:") }, "disk");
    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(1);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var letter = (ctx.Str("drive") ?? "C:").TrimEnd('\\', '/');
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.TrimEnd('\\').Equals(letter, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Lecteur {letter} introuvable.");
        if (!drive.IsReady) throw new InvalidOperationException($"Lecteur {letter} non prêt.");
        var total = (ulong)drive.TotalSize;
        var free = (ulong)drive.AvailableFreeSpace;
        var used = total - free;
        return Task.FromResult(new Reading(Value: Units.Gb(used), Max: Units.Gb(total), Unit: "Go",
            Detail: new[]
            {
                new DetailRow($"{letter} {drive.VolumeLabel}".Trim(), $"{Units.Gb(used)} / {Units.Gb(total)} Go", (double)used / total),
                new DetailRow("Libre", $"{Units.Gb(free)} Go"),
            },
            Actions: new[] { new ActionSpec("open", "Ouvrir", "folder") }));
    }

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        if (action == "open") Actions.ActionRunner.Open((ctx.Str("drive") ?? "C:") + "\\");
        return Task.CompletedTask;
    }
}
```

`src/CustomNotch.Core/Sources/System/NetworkSource.cs` :
```csharp
using System.Net.NetworkInformation;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.System;

public sealed class NetworkSource : SourceBase
{
    private (long Ms, ulong Rx, ulong Tx)? _previous;
    private readonly Queue<(long, double)> _history = new();

    public override string Type => "system.network";
    public override SourceSchema Schema => new(Type, "Réseau", new[] { new SchemaField("iface", "string", "Interface", Help: "Vide = toutes") }, "network");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(2);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var wanted = ctx.Str("iface");
        ulong rx = 0, tx = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.OperationalStatus != OperationalStatus.Up) continue;
            if (wanted is not null && !nic.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            var stats = nic.GetIPStatistics();
            rx += (ulong)stats.BytesReceived;
            tx += (ulong)stats.BytesSent;
        }
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (down, up) = (0.0, 0.0);
        var unit = "Ko/s";
        if (_previous is { } p && now > p.Ms)
        {
            var seconds = (now - p.Ms) / 1000.0;
            var d = Units.Rate(rx >= p.Rx ? rx - p.Rx : 0, seconds);
            var u = Units.Rate(tx >= p.Tx ? tx - p.Tx : 0, seconds);
            var total = Units.Rate((rx >= p.Rx ? rx - p.Rx : 0) + (tx >= p.Tx ? tx - p.Tx : 0), seconds);
            (down, up, unit) = (d.Value, u.Value, total.Unit);
            History.Keep(_history, total.Value * (total.Unit == "Mo/s" ? 1024 : 1), 60);
            _previous = (now, rx, tx);
            return Task.FromResult(new Reading(Value: total.Value, Unit: unit,
                Detail: new[] { new DetailRow("↓ Réception", $"{d.Value} {d.Unit}"), new DetailRow("↑ Émission", $"{u.Value} {u.Unit}") },
                History: _history.ToList()));
        }
        _previous = (now, rx, tx);
        return Task.FromResult(new Reading(Value: 0, Unit: unit, History: _history.ToList()));
    }
}
```

`src/CustomNotch.Core/Sources/System/BatterySource.cs` :
```csharp
using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;

namespace CustomNotch.Core.Sources.System;

public sealed class BatterySource : SourceBase
{
    public override string Type => "system.battery";
    public override SourceSchema Schema => new(Type, "Batterie", Array.Empty<SchemaField>(), "battery");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(30);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var power = SystemInfo.Power() ?? throw new InvalidOperationException("Alimentation indisponible sur ce système.");
        if (!power.HasBattery)
            return Task.FromResult(new Reading(Status: Status.Off, Text: "Secteur", Detail: new[] { new DetailRow("Batterie", "aucune") }));
        var thresholds = new Thresholds(20, 10, Invert: true);
        var status = power.OnAc ? Status.Ok : thresholds.Judge(power.Percent);
        var left = power.SecondsLeft > 0 ? $"{power.SecondsLeft / 3600} h {(power.SecondsLeft % 3600) / 60:00}" : null;
        return Task.FromResult(new Reading(Value: power.Percent, Max: 100, Unit: "%", Status: status,
            Detail: new[]
            {
                new DetailRow("Charge", $"{power.Percent} %", power.Percent / 100.0, power.OnAc ? "sur secteur" : left is null ? null : $"reste {left}"),
            }));
    }
}
```

`src/CustomNotch.Core/Sources/System/SystemSources.cs` :
```csharp
namespace CustomNotch.Core.Sources.System;

public static class SystemSources
{
    public static void RegisterAll(SourceRegistry registry)
    {
        registry.Register(new CpuSource());
        registry.Register(new MemorySource());
        registry.Register(new DiskSource());
        registry.Register(new NetworkSource());
        registry.Register(new BatterySource());
    }
}
```

`DiskSource` référence `Actions.ActionRunner.Open` (tâche 7) : créer dès maintenant `src/CustomNotch.Core/Actions/ActionRunner.cs` avec la seule méthode `Open` (le reste vient en tâche 7) :
```csharp
using System.Diagnostics;

namespace CustomNotch.Core.Actions;

public static class ActionRunner
{
    /// <summary>Ouvre une URL, un fichier, un dossier ou une application par le shell (comme un double-clic).</summary>
    public static bool Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Log.Warning("action", $"open « {target} » : {ex.Message}");
            return false;
        }
    }
}
```

- [ ] **Step 5: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: PASS (les tests système tournent sur cette machine Windows).

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(core): sources système CPU, mémoire, disque, réseau, batterie"
```

---

### Task 7: Sources launcher, http, shell et ActionRunner complet

**Files:**
- Create: `src/CustomNotch.Core/Sources/JsonPath.cs`, `Sources/LauncherSource.cs`, `Sources/HttpSource.cs`, `Sources/ShellSource.cs`, `Sources/CoreSources.cs`
- Modify: `src/CustomNotch.Core/Actions/ActionRunner.cs`
- Test: `tests/CustomNotch.Core.Tests/Sources/JsonPathTests.cs`, `Sources/HttpSourceTests.cs`, `Sources/ShellSourceTests.cs`, `Sources/ActionRunnerTests.cs`

**Interfaces:**
- Consumes: `SourceBase`, `CellContext`, `Reading`, `ActionConfig`.
- Produces: `JsonPath.Select(JsonNode? root, string path) → JsonNode?` (notation pointée + `[n]`), `LauncherSource` (`launcher`, param `open`), `HttpSource` (`http`, params `url`, `method`, `headers`, `body`, `path`, `textPath`, `max`, `unit`, `label`; `HttpSource.Http` remplaçable), `ShellSource` (`shell`, params `command`, `parse` = number|json|text, `path`, `timeoutSeconds`), `CoreSources.RegisterAll(SourceRegistry)`, `ActionRunner.Open(target)`, `ActionRunner.Shell(command)`, `ActionRunner.RunAsync(ActionConfig action, Func<string, Task> sourceAction)`.

- [ ] **Step 1: Tests JsonPath et ActionRunner**

`tests/CustomNotch.Core.Tests/Sources/JsonPathTests.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Sources;

public class JsonPathTests
{
    private static readonly JsonNode Doc = JsonNode.Parse("""{"data":{"count":7,"items":[{"n":"a"},{"n":"b"}],"x.y":1}}""")!;

    [Theory]
    [InlineData("data.count", "7")]
    [InlineData("data.items[1].n", "b")]
    [InlineData("data.items.0.n", "a")]
    public void La_notation_pointee_et_les_index_marchent(string path, string expected)
        => Assert.Equal(expected, JsonPath.Select(Doc, path)!.ToString());

    [Fact]
    public void Un_chemin_absent_rend_null()
    {
        Assert.Null(JsonPath.Select(Doc, "data.nope"));
        Assert.Null(JsonPath.Select(Doc, "data.items[9]"));
    }

    [Fact]
    public void Un_chemin_vide_rend_la_racine() => Assert.Same(Doc, JsonPath.Select(Doc, ""));
}
```

`tests/CustomNotch.Core.Tests/Sources/ActionRunnerTests.cs` :
```csharp
using CustomNotch.Core.Actions;
using CustomNotch.Core.Config;
namespace CustomNotch.Core.Tests.Sources;

public class ActionRunnerTests
{
    [Fact]
    public async Task Une_action_source_est_deleguee()
    {
        string? got = null;
        await ActionRunner.RunAsync(new ActionConfig { Source = "toggle" }, a => { got = a; return Task.CompletedTask; });
        Assert.Equal("toggle", got);
    }

    [Fact]
    public async Task Une_action_shell_s_execute_sans_fenetre()
    {
        var marker = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        await ActionRunner.RunAsync(new ActionConfig { Shell = $"echo ok > \"{marker}\"" }, _ => Task.CompletedTask);
        await Task.Delay(500);
        Assert.True(File.Exists(marker));
        File.Delete(marker);
    }

    [Fact]
    public async Task Une_action_vide_ne_fait_rien()
        => await ActionRunner.RunAsync(new ActionConfig(), _ => throw new InvalidOperationException("ne doit pas être appelé"));
}
```

- [ ] **Step 2: JsonPath et ActionRunner**

`src/CustomNotch.Core/Sources/JsonPath.cs` :
```csharp
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources;

/// <summary>Le strict nécessaire pour pointer une valeur dans un JSON : « data.items[1].n » ou « data.items.1.n ».
/// Pas de jokers ni de filtres - pour ça, la source shell et un jq.</summary>
public static class JsonPath
{
    public static JsonNode? Select(JsonNode? root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return root;
        var node = root;
        foreach (var raw in path.Replace("[", ".").Replace("]", "").Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (node)
            {
                case JsonArray arr when int.TryParse(raw, out var i):
                    node = i >= 0 && i < arr.Count ? arr[i] : null;
                    break;
                case JsonObject obj:
                    node = obj[raw];
                    break;
                default:
                    return null;
            }
            if (node is null) return null;
        }
        return node;
    }
}
```

`src/CustomNotch.Core/Actions/ActionRunner.cs` (remplace la version de la tâche 6) :
```csharp
using System.Diagnostics;
using CustomNotch.Core.Config;

namespace CustomNotch.Core.Actions;

/// <summary>Les trois actions d'une cellule : open (URL, fichier, app), shell (commande, sans fenêtre), source
/// (méthode de la source, déléguée à l'ordonnanceur).</summary>
public static class ActionRunner
{
    public static bool Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Log.Warning("action", $"open « {target} » : {ex.Message}");
            return false;
        }
    }

    /// <summary>cmd.exe /c, fenêtre cachée, sans attendre : une action ne bloque jamais la pilule.</summary>
    public static bool Shell(string command)
    {
        try
        {
            var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add(command);
            Process.Start(info);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Log.Warning("action", $"shell « {command} » : {ex.Message}");
            return false;
        }
    }

    public static async Task RunAsync(ActionConfig action, Func<string, Task> sourceAction)
    {
        if (!string.IsNullOrWhiteSpace(action.Open)) Open(action.Open);
        else if (!string.IsNullOrWhiteSpace(action.Shell)) Shell(action.Shell);
        else if (!string.IsNullOrWhiteSpace(action.Source)) await sourceAction(action.Source).ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Tests http et shell**

`tests/CustomNotch.Core.Tests/Sources/HttpSourceTests.cs` :
```csharp
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Sources;

public class HttpSourceTests : IDisposable
{
    private readonly HttpListener _server = new();
    private readonly string _prefix;
    private string _body = "{}";
    private string? _seenAuth;

    public HttpSourceTests()
    {
        var port = Random.Shared.Next(20000, 40000);
        _prefix = $"http://127.0.0.1:{port}/";
        _server.Prefixes.Add(_prefix);
        _server.Start();
        _ = Task.Run(async () =>
        {
            while (_server.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _server.GetContextAsync(); } catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { return; }
                _seenAuth = ctx.Request.Headers["Authorization"];
                var bytes = Encoding.UTF8.GetBytes(ctx.Request.Url!.AbsolutePath == "/fail" ? "boom" : _body);
                ctx.Response.StatusCode = ctx.Request.Url.AbsolutePath == "/fail" ? 500 : 200;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        });
    }

    public void Dispose() => _server.Stop();

    private CellContext Ctx(string paramsJson) => new("h", JsonNode.Parse(paramsJson)!.AsObject(), null);

    [Fact]
    public async Task Une_valeur_est_extraite_avec_max_et_unite()
    {
        _body = """{"data":{"open":12,"limit":40}}""";
        var r = await new HttpSource().ReadAsync(Ctx($$"""{"url":"{{_prefix}}x","path":"data.open","max":40,"unit":"tickets","headers":{"Authorization":"Bearer t"}}"""), CancellationToken.None);
        Assert.Equal(12, r.Value);
        Assert.Equal(40, r.Max);
        Assert.Equal("tickets", r.Unit);
        Assert.Equal("Bearer t", _seenAuth);
    }

    [Fact]
    public async Task Un_texte_peut_etre_extrait_a_part()
    {
        _body = """{"status":"green","count":3}""";
        var r = await new HttpSource().ReadAsync(Ctx($$"""{"url":"{{_prefix}}","path":"count","textPath":"status"}"""), CancellationToken.None);
        Assert.Equal(3, r.Value);
        Assert.Equal("green", r.Text);
    }

    [Fact]
    public async Task Une_erreur_http_leve()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpSource().ReadAsync(Ctx($$"""{"url":"{{_prefix}}fail"}"""), CancellationToken.None));
        Assert.Contains("500", ex.Message);
    }
}
```

`tests/CustomNotch.Core.Tests/Sources/ShellSourceTests.cs` :
```csharp
using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Sources;

public class ShellSourceTests
{
    private static CellContext Ctx(string json) => new("s", JsonNode.Parse(json)!.AsObject(), null);

    [Fact]
    public async Task Un_nombre_est_lu_sur_la_sortie()
    {
        var r = await new ShellSource().ReadAsync(Ctx("""{"command":"echo 42","parse":"number","unit":"°C"}"""), CancellationToken.None);
        Assert.Equal(42, r.Value);
        Assert.Equal("°C", r.Unit);
    }

    [Fact]
    public async Task Un_json_est_lu_avec_un_chemin()
    {
        var r = await new ShellSource().ReadAsync(Ctx("""{"command":"echo {\"a\":{\"b\":5}}","parse":"json","path":"a.b"}"""), CancellationToken.None);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task Le_texte_est_gardé_tel_quel()
    {
        var r = await new ShellSource().ReadAsync(Ctx("""{"command":"echo hello","parse":"text"}"""), CancellationToken.None);
        Assert.Equal("hello", r.Text);
    }

    [Fact]
    public async Task Un_code_de_sortie_non_nul_leve()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => new ShellSource().ReadAsync(Ctx("""{"command":"exit 3"}"""), CancellationToken.None));

    [Fact]
    public async Task Le_delai_est_respecte()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new ShellSource().ReadAsync(Ctx("""{"command":"ping -n 6 127.0.0.1 > nul","timeoutSeconds":1}"""), CancellationToken.None));
        Assert.Contains("délai", ex.Message);
    }
}
```

- [ ] **Step 4: Sources**

`src/CustomNotch.Core/Sources/LauncherSource.cs` :
```csharp
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Pas de donnée : un glyph et une action. C'est ce qui remplace un raccourci de barre des tâches.</summary>
public sealed class LauncherSource : SourceBase
{
    public override string Type => "launcher";
    public override SourceSchema Schema => new(Type, "Lanceur", new[] { new SchemaField("open", "string", "Ouvrir", Required: true, Help: "URL, chemin, ou application") }, "link");
    public override TimeSpan DefaultRefresh => TimeSpan.FromHours(24);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
        => Task.FromResult(new Reading(Status: Status.Off, Text: ctx.Str("open"), Actions: new[] { new ActionSpec("open", "Ouvrir", "open") }));

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        if (action == "open" && ctx.Str("open") is { } target) Actions.ActionRunner.Open(target);
        return Task.CompletedTask;
    }
}
```

`src/CustomNotch.Core/Sources/HttpSource.cs` :
```csharp
using System.Text;
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Une URL, une extraction JSON, un maximum facultatif : de quoi afficher n'importe quel chiffre métier sans
/// écrire de code. Les en-têtes portent l'auth via ${secret:…}.</summary>
public sealed class HttpSource : SourceBase
{
    public static HttpClient Http { get; set; } = new() { Timeout = TimeSpan.FromSeconds(15) };

    public override string Type => "http";
    public override SourceSchema Schema => new(Type, "HTTP / JSON", new SchemaField[]
    {
        new("url", "url", "URL", Required: true),
        new("method", "choice", "Méthode", Default: "GET", Choices: new[] { "GET", "POST" }),
        new("path", "string", "Chemin JSON de la valeur", Help: "data.count ou items[0].n"),
        new("textPath", "string", "Chemin JSON du texte"),
        new("max", "number", "Maximum", Help: "Présent = anneau en %"),
        new("unit", "string", "Unité"),
        new("body", "string", "Corps (POST)"),
    }, "globe");
    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(1);

    public override async Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var url = ctx.Str("url") ?? throw new InvalidOperationException("url manquante");
        using var request = new HttpRequestMessage(new HttpMethod(ctx.Str("method") ?? "GET"), url);
        request.Headers.TryAddWithoutValidation("User-Agent", $"{App.Name}/{App.Version}");
        if (ctx.Params["headers"] is JsonObject headers)
            foreach (var (k, v) in headers) request.Headers.TryAddWithoutValidation(k, v?.ToString() ?? "");
        if (ctx.Str("body") is { } body) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");
        JsonNode? root;
        try { root = JsonNode.Parse(text); }
        catch (System.Text.Json.JsonException) { root = JsonValue.Create(text); }
        var valueNode = JsonPath.Select(root, ctx.Str("path") ?? "");
        double? value = valueNode is JsonValue v && v.TryGetValue<double>(out var d) ? d
            : double.TryParse(valueNode?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        var textValue = ctx.Str("textPath") is { } tp ? JsonPath.Select(root, tp)?.ToString() : value is null ? valueNode?.ToString() : null;
        return new Reading(Value: value, Max: ctx.Num("max"), Unit: ctx.Str("unit"), Text: textValue,
            Detail: new[] { new DetailRow(ctx.Str("label") ?? "Valeur", textValue ?? value?.ToString() ?? "—") });
    }
}
```

`src/CustomNotch.Core/Sources/ShellSource.cs` :
```csharp
using System.Diagnostics;
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>L'échappatoire : une commande, sa sortie lue comme nombre, JSON ou texte. Fenêtre cachée, délai borné.</summary>
public sealed class ShellSource : SourceBase
{
    public override string Type => "shell";
    public override SourceSchema Schema => new(Type, "Commande", new SchemaField[]
    {
        new("command", "string", "Commande", Required: true, Help: "Exécutée par cmd.exe /c"),
        new("parse", "choice", "Lire la sortie comme", Default: "number", Choices: new[] { "number", "json", "text" }),
        new("path", "string", "Chemin JSON", Help: "Si parse = json"),
        new("max", "number", "Maximum"),
        new("unit", "string", "Unité"),
        new("timeoutSeconds", "number", "Délai (s)", Default: "5"),
    }, "terminal");
    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(1);

    public override async Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var command = ctx.Str("command") ?? throw new InvalidOperationException("command manquante");
        var timeout = TimeSpan.FromSeconds(ctx.Num("timeoutSeconds") ?? 5);
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };
        info.ArgumentList.Add("/c");
        info.ArgumentList.Add(command);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Impossible de lancer la commande.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new InvalidOperationException($"délai de {timeout.TotalSeconds} s dépassé");
        }
        var stdout = (await output.ConfigureAwait(false)).Trim();
        if (process.ExitCode != 0) throw new InvalidOperationException($"code de sortie {process.ExitCode}");
        var unit = ctx.Str("unit");
        switch ((ctx.Str("parse") ?? "number").ToLowerInvariant())
        {
            case "json":
                var node = JsonPath.Select(JsonNode.Parse(stdout), ctx.Str("path") ?? "");
                var number = node is JsonValue jv && jv.TryGetValue<double>(out var d) ? d : (double?)null;
                return new Reading(Value: number, Max: ctx.Num("max"), Unit: unit, Text: number is null ? node?.ToString() : null);
            case "text":
                return new Reading(Text: stdout, Detail: new[] { new DetailRow("Sortie", stdout) });
            default:
                var digits = new string(stdout.TakeWhile(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray()).Replace(',', '.');
                if (!double.TryParse(digits, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                    throw new InvalidOperationException($"« {stdout} » n'est pas un nombre");
                return new Reading(Value: value, Max: ctx.Num("max"), Unit: unit);
        }
    }
}
```

`src/CustomNotch.Core/Sources/CoreSources.cs` :
```csharp
namespace CustomNotch.Core.Sources;

/// <summary>Toutes les sources livrées avec le cœur, dans un registre neuf.</summary>
public static class CoreSources
{
    public static SourceRegistry Build()
    {
        var registry = new SourceRegistry();
        System.SystemSources.RegisterAll(registry);
        registry.Register(new LauncherSource());
        registry.Register(new HttpSource());
        registry.Register(new ShellSource());
        return registry;
    }
}
```

- [ ] **Step 5: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(core): sources launcher, http et shell ; ActionRunner"
```

---

### Task 8: Squelette WPF — briques partagées, instance unique, tray, contrôleur

**Files:**
- Create: `src/CustomNotch.Core/Platform/SingleInstance.cs`
- Create: `src/CustomNotch.App/Shared/Theme.cs`, `Shared/Ui.cs`, `Shared/Glyphs.cs`, `Shared/Native.cs`, `Shared/Screens.cs`
- Create: `src/CustomNotch.App/Styles.xaml`, `src/CustomNotch.App/TrayIcon.cs`, `src/CustomNotch.App/Controller.cs`, `src/CustomNotch.App/IPillHost.cs`
- Modify: `src/CustomNotch.App/App.xaml`, `src/CustomNotch.App/App.xaml.cs`

**Interfaces:**
- Consumes: `ConfigStore`, `CoreSources.Build()`, `ReadingStore`, `Scheduler`, `Idle.Ms`, `Paths`, `Log`.
- Produces: `SingleInstance` (`ctor(home)`, `Acquire()`, `static Ping(home, command)`, `ShowRequested`, `CommandReceived`) ; `interface IPillHost` (voir ci-dessous) ; `Controller` (`ctor(string home)`, `Start()`, `Stop()`, `ShowSettings()`, `RefreshAll()`, `TogglePill(string id)`, implémente `IPillHost`) ; `TrayIcon` (`ctor()`, `SetPills(IEnumerable<(string Id, bool Visible)>)`, events `RefreshRequested`, `SettingsRequested`, `QuitRequested`, `PillToggleRequested(string)`, `ToggleAllRequested`, `Notify(title, body)`).
- La tâche 9 fournit `PillWindow` ; ici le contrôleur compile avec une classe `PillWindow` **provisoire** (fenêtre vide) créée dans ce même commit et remplacée en tâche 9.

- [ ] **Step 1: SingleInstance**

`src/CustomNotch.Core/Platform/SingleInstance.cs` : copier la classe `SingleInstance` (lignes `public sealed class SingleInstance` jusqu'à son accolade fermante, avec ses `using`) depuis `ClickUp-Extended\src\ClickUpExtended.Core\Platform.cs`, namespace `CustomNotch.Core.Platform`. Aucune autre modification.

- [ ] **Step 2: Briques partagées**

Copier depuis `C:\Users\Coco_FW\Desktop\ClickUp-Extended\src\ClickUpExtended.App\` vers `src/CustomNotch.App/Shared/` :
- `Theme.cs` : `namespace ClickUpExtended.App;` → `namespace CustomNotch.App;` ; `using ClickUpExtended.Core;` → `using CustomNotch.Core;` + `using CustomNotch.Core.Config;` ; les deux paramètres `Config cfg` → `AppConfig cfg`. Rien d'autre.
- `Ui.cs`, `Glyphs.cs`, `Native.cs`, `Screens.cs` : seul le `namespace` change.

`src/CustomNotch.App/Styles.xaml` (les trois styles de bouton que `Ui.cs` cherche par `FindResource`, plus le texte) :
```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Style TargetType="TextBlock">
        <Setter Property="FontFamily" Value="{DynamicResource UiFont}" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="Foreground" Value="{DynamicResource Fg}" />
    </Style>
    <Style x:Key="ButtonBase" TargetType="Button">
        <Setter Property="FontFamily" Value="{DynamicResource UiFont}" />
        <Setter Property="FontSize" Value="12" />
        <Setter Property="Padding" Value="10,5" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="7" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Opacity" Value="0.85" /></Trigger>
                        <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5" /></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key="Primary" TargetType="Button" BasedOn="{StaticResource ButtonBase}">
        <Setter Property="Background" Value="{DynamicResource Accent}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Accent}" />
        <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
    </Style>
    <Style x:Key="Secondary" TargetType="Button" BasedOn="{StaticResource ButtonBase}">
        <Setter Property="Background" Value="{DynamicResource SurfaceHover}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Border}" />
        <Setter Property="Foreground" Value="{DynamicResource Fg}" />
    </Style>
    <Style x:Key="GhostButton" TargetType="Button" BasedOn="{StaticResource ButtonBase}">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderBrush" Value="Transparent" />
        <Setter Property="Foreground" Value="{DynamicResource Muted}" />
    </Style>
    <!-- Les boutons de la carte hover : fond #252525, bord #555, texte blanc (codenotch .c-auth button). -->
    <Style x:Key="CardButton" TargetType="Button" BasedOn="{StaticResource ButtonBase}">
        <Setter Property="FontSize" Value="11" />
        <Setter Property="Padding" Value="9,6" />
        <Setter Property="Background" Value="#252525" />
        <Setter Property="BorderBrush" Value="#555555" />
        <Setter Property="Foreground" Value="#FFFFFF" />
    </Style>
</ResourceDictionary>
```

`src/CustomNotch.App/App.xaml` :
```xml
<Application x:Class="CustomNotch.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Styles.xaml" />
            </ResourceDictionary.MergedDictionaries>
            <FontFamily x:Key="UiFont">Segoe UI</FontFamily>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Contrat entre les pilules et le contrôleur**

`src/CustomNotch.App/IPillHost.cs` :
```csharp
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.App;

/// <summary>Ce qu'une PillWindow demande au contrôleur : les vues à dessiner, les actions, la sauvegarde de sa position.
/// Une interface plutôt que le contrôleur lui-même, pour que les fenêtres se testent avec un hôte factice.</summary>
public interface IPillHost
{
    CellView? View(string cellId);
    /// <summary>Les vues des enfants d'une cellule-groupe, dans l'ordre de la config.</summary>
    IReadOnlyList<CellView> Children(CellConfig group);
    Task RunActionAsync(string cellId, ActionConfig action);
    /// <summary>Une action déclarée par la source elle-même (Reading.Actions) : play, stop, open…</summary>
    Task InvokeSourceAsync(string cellId, string action);
    void SavePosition(string pillId, double along, string? screen);
    void RequestRefresh(string cellId);
    void HidePill(string pillId);
    void ShowSettings();
}
```

- [ ] **Step 4: TrayIcon**

`src/CustomNotch.App/TrayIcon.cs` :
```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CustomNotch.Core;
using WinForms = System.Windows.Forms;

namespace CustomNotch.App;

/// <summary>L'icône de la zone de notification : afficher/masquer les pilules, rafraîchir, réglages, quitter.
/// L'icône est dessinée au lancement (un anneau blanc sur fond noir) : pas de fichier .ico à ce stade.</summary>
public sealed class TrayIcon : IDisposable
{
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;
    public event Action? ToggleAllRequested;
    public event Action<string>? PillToggleRequested;

    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _pills;

    public TrayIcon()
    {
        _menu = new WinForms.ContextMenuStrip { Font = new System.Drawing.Font("Segoe UI", 10f) };
        _pills = new WinForms.ToolStripMenuItem("Pilules");
        _menu.Items.AddRange(new WinForms.ToolStripItem[]
        {
            new WinForms.ToolStripMenuItem($"{App_.Name} {App_.Version}") { Enabled = false },
            new WinForms.ToolStripSeparator(),
            _pills,
            Item("Afficher / masquer tout", () => ToggleAllRequested?.Invoke()),
            Item("Rafraîchir maintenant", () => RefreshRequested?.Invoke()),
            new WinForms.ToolStripSeparator(),
            Item("Réglages…", () => SettingsRequested?.Invoke()),
            Item("Quitter", () => QuitRequested?.Invoke()),
        });
        _icon = new WinForms.NotifyIcon { ContextMenuStrip = _menu, Text = App_.Name, Icon = DrawIcon(), Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == WinForms.MouseButtons.Left) ToggleAllRequested?.Invoke(); };
    }

    private static class App_ { public const string Name = CustomNotch.Core.App.Name; public static readonly string Version = CustomNotch.Core.App.Version; }

    private static WinForms.ToolStripMenuItem Item(string text, Action action)
    {
        var item = new WinForms.ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }

    public void SetPills(IEnumerable<(string Id, bool Visible)> pills)
    {
        _pills.DropDownItems.Clear();
        foreach (var (id, visible) in pills)
        {
            var item = new WinForms.ToolStripMenuItem(id) { Checked = visible };
            item.Click += (_, _) => PillToggleRequested?.Invoke(id);
            _pills.DropDownItems.Add(item);
        }
        _pills.Enabled = _pills.DropDownItems.Count > 0;
    }

    public void Notify(string title, string body) => _icon.ShowBalloonTip(5000, title, body, WinForms.ToolTipIcon.None);

    /// <summary>Un carré noir arrondi et un anneau blanc : la pilule vue de loin.</summary>
    private static System.Drawing.Icon DrawIcon()
    {
        const int size = 64;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(Brushes.Black, null, new Rect(4, 4, 56, 56), 16, 16);
            dc.DrawEllipse(null, new Pen(Brushes.White, 7), new Point(32, 32), 15, 15);
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        using var png = new System.Drawing.Bitmap(stream);
        return System.Drawing.Icon.FromHandle(png.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
```

- [ ] **Step 5: PillWindow provisoire et Controller**

`src/CustomNotch.App/Notch/PillWindow.cs` (provisoire — la tâche 9 le remplace entièrement) :
```csharp
using System.Windows;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Notch;

public sealed class PillWindow : Window
{
    public PillWindow(PillConfig pill, IPillHost host)
    {
        Pill = pill;
        Title = $"customNotch - {pill.Id}";
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = 100; Height = 300; Left = 200; Top = 200;
        Content = new System.Windows.Shapes.Rectangle { Fill = System.Windows.Media.Brushes.Black, RadiusX = 20, RadiusY = 20, Width = 70, Height = 260 };
    }

    public PillConfig Pill { get; private set; }
    public void Apply(PillConfig pill) => Pill = pill;
    public void UpdateCell(string cellId) { }
    public void Tick() { }
}
```

`src/CustomNotch.App/Controller.cs` :
```csharp
using System.Windows;
using System.Windows.Threading;
using CustomNotch.App.Notch;
using CustomNotch.Core;
using CustomNotch.Core.Actions;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;

namespace CustomNotch.App;

/// <summary>Assemblage et cycle de vie : config → ordonnanceur → pilules, tray, tic d'une seconde. Tout ce qui vient
/// d'un autre thread (lectures, rechargement de config) repasse par le Dispatcher ici et nulle part ailleurs.</summary>
public sealed class Controller : IPillHost
{
    private readonly string _home;
    private readonly SourceRegistry _registry = CoreSources.Build();
    private readonly ReadingStore _readings = new();
    private readonly ConfigStore _config;
    private readonly Scheduler _scheduler;
    private readonly Dictionary<string, PillWindow> _pills = new();
    private readonly TrayIcon _tray = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _allHidden;

    public Controller(string home)
    {
        _home = home;
        _config = new ConfigStore(home, _registry.Types);
        _scheduler = new Scheduler(_registry, _readings, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Core.Platform.Idle.Ms);
    }

    private static Dispatcher Ui => Application.Current.Dispatcher;

    public void Start()
    {
        Theme.Apply(Application.Current, _config.App);
        _config.Changed += file => Ui.BeginInvoke(() => ApplyConfig(file));
        _config.Rejected += message => Ui.BeginInvoke(() => _tray.Notify("Configuration refusée", message));
        _readings.Changed += id => Ui.BeginInvoke(() => OnReadingChanged(id));
        _tray.RefreshRequested += RefreshAll;
        _tray.SettingsRequested += ShowSettings;
        _tray.QuitRequested += Quit;
        _tray.ToggleAllRequested += ToggleAll;
        _tray.PillToggleRequested += TogglePill;
        _tick.Tick += (_, _) => { foreach (var p in _pills.Values) p.Tick(); };
        _tick.Start();
        if (!_config.Load()) _tray.Notify("Configuration refusée", string.Join(" ; ", _config.LastErrors));
        _config.StartWatching();
        Log.Info("app", $"{App.Name} {App.Version} démarré, cells.json : {_config.CellsPath}");
    }

    private void ApplyConfig(CellsFile file)
    {
        _scheduler.Apply(file);
        var wanted = file.Pills.ToDictionary(p => p.Id);
        foreach (var id in _pills.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
        {
            _pills[id].Close();
            _pills.Remove(id);
        }
        foreach (var pill in file.Pills)
        {
            if (_pills.TryGetValue(pill.Id, out var window)) window.Apply(pill);
            else
            {
                window = new PillWindow(pill, this);
                _pills[pill.Id] = window;
            }
            if (pill.Visible && !_allHidden) window.Show(); else window.Hide();
        }
        _tray.SetPills(file.Pills.Select(p => (p.Id, p.Visible)));
    }

    private void OnReadingChanged(string cellId)
    {
        foreach (var pill in _pills.Values) pill.UpdateCell(cellId);
    }

    public void RefreshAll()
    {
        foreach (var cell in _config.Current.AllCells()) _scheduler.RefreshNow(cell.Id);
    }

    public void ToggleAll()
    {
        _allHidden = !_allHidden;
        foreach (var (id, window) in _pills)
        {
            var visible = _config.Current.Pills.First(p => p.Id == id).Visible;
            if (visible && !_allHidden) window.Show(); else window.Hide();
        }
    }

    public void TogglePill(string id)
    {
        var current = _config.Current.Pills.FirstOrDefault(p => p.Id == id)?.Visible ?? true;
        _config.SetPillLocal(id, p => p["visible"] = !current);
    }

    /// <summary>Pas encore de fenêtre de réglages (plan 2) : on ouvre cells.json dans l'éditeur par défaut.</summary>
    public void ShowSettings() => ActionRunner.Open(_config.CellsPath);

    public void Quit()
    {
        Stop();
        Application.Current.Shutdown();
    }

    public void Stop()
    {
        _tick.Stop();
        _scheduler.Dispose();
        _config.Dispose();
        _tray.Dispose();
        foreach (var p in _pills.Values) p.Close();
    }

    // ---- IPillHost -------------------------------------------------------------------------------------------

    public CellView? View(string cellId)
    {
        var cell = _config.Current.Cell(cellId);
        if (cell is null) return null;
        var reading = _readings.Get(cellId) ?? Reading.Empty;
        return CellViews.From(cell, reading, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public IReadOnlyList<CellView> Children(CellConfig group)
        => (group.Children ?? new List<string>()).Select(View).Where(v => v is not null).Select(v => v!).ToList();

    public Task RunActionAsync(string cellId, ActionConfig action) => ActionRunner.RunAsync(action, a => _scheduler.InvokeAsync(cellId, a));
    public Task InvokeSourceAsync(string cellId, string action) => _scheduler.InvokeAsync(cellId, action);

    public void SavePosition(string pillId, double along, string? screen)
        => _config.SetPillLocal(pillId, p => { p["along"] = Math.Round(along, 4); p["screen"] = screen; });

    public void RequestRefresh(string cellId) => _scheduler.RefreshNow(cellId);
    public void HidePill(string pillId) => _config.SetPillLocal(pillId, p => p["visible"] = false);
}
```

`src/CustomNotch.App/App.xaml.cs` :
```csharp
using System.Windows;
using CustomNotch.Core;
using CustomNotch.Core.Platform;

namespace CustomNotch.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private Controller? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--version"))
        {
            Console.WriteLine($"{Core.App.Name} {Core.App.Version}");
            Shutdown();
            return;
        }
        var homeArg = Array.IndexOf(e.Args, "--home");
        if (homeArg >= 0 && homeArg + 1 < e.Args.Length) Environment.SetEnvironmentVariable(Core.App.HomeEnv, e.Args[homeArg + 1]);
        var home = Paths.Home();
        Directory.CreateDirectory(home);
        Log.Directory = Paths.LogDir(home);
        _instance = new SingleInstance(home);
        if (!_instance.Acquire())
        {
            SingleInstance.Ping(home, "show");
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("app", $"Exception non gérée : {ex.Exception}");
            ex.Handled = true;
        };
        _controller = new Controller(home);
        _instance.ShowRequested += () => Dispatcher.BeginInvoke(() => _controller.ShowSettings());
        _controller.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Stop();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 6: Build et lancement**

Run: `dotnet build CustomNotch.sln` puis `dotnet run --project src/CustomNotch.App`
Expected: une icône apparaît dans la zone de notification ; un rectangle noir arrondi apparaît à (200,200) ; `%APPDATA%\customNotch\cells.json` et `logs\journal.log` existent ; « Quitter » ferme tout. Une seconde instance lancée se ferme aussitôt.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(app): squelette WPF, instance unique, tray et contrôleur"
```

---

### Task 9: Géométrie de la pilule, placement au bord, fenêtre no-activate, drag

**Files:**
- Create: `src/CustomNotch.App/Notch/PillMetrics.cs`, `Notch/PillShape.cs`, `Notch/EdgePlacement.cs`
- Replace: `src/CustomNotch.App/Notch/PillWindow.cs`
- Create: `tests/CustomNotch.App.Tests/CustomNotch.App.Tests.csproj`, `tests/CustomNotch.App.Tests/PillShapeTests.cs`, `tests/CustomNotch.App.Tests/EdgePlacementTests.cs`

**Interfaces:**
- Consumes: `PillConfig`, `IPillHost`, `Screens`, `Native`.
- Produces: `record PillMetrics(double Scale)` avec `Width = 70·s`, `Corner = 20·s`, `Fillet = 38.7·s`, `Padding = 18·s`, `Ring = 44·s`, `Gap = 14·s`, `CardWidth = 246`, `Tail = 32`, `CardGap = 8`, `double Length(int cells)`, `double Extent(int cells) = Length + 2·Fillet` ; `PillShape.Build(PillMetrics m, string edge, int cells) → Geometry` (figé, repère de la pilule : pour un bord vertical `Width × Extent`, pour un bord horizontal `Extent × Width`) ; `EdgePlacement.IsVertical(edge)`, `EdgePlacement.Place(Rect area, string edge, double along, double extent, double thickness) → Rect` (le rectangle du canvas de la pilule sur l'écran, en DIP), `EdgePlacement.AlongFrom(Rect area, string edge, Point pillOrigin, double extent) → double` ; `PillWindow` (`ctor(PillConfig, IPillHost)`, `Pill`, `Apply(PillConfig)`, `UpdateCell(string)`, `Tick()`, `Reposition()`).
- Les cellules ne sont pas encore dessinées : la pilule est noire et vide à la bonne taille, au bon endroit ; le drag marche.

- [ ] **Step 1: Projet de tests App et tests de géométrie**

`tests/CustomNotch.App.Tests/CustomNotch.App.Tests.csproj` :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\CustomNotch.App\CustomNotch.App.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Remove="System.Windows.Forms" />
    <Using Remove="System.Drawing" />
  </ItemGroup>
</Project>
```
Puis `dotnet sln add tests/CustomNotch.App.Tests`. Les tests de géométrie WPF n'ont pas besoin de thread STA (aucune fenêtre créée).

`tests/CustomNotch.App.Tests/PillShapeTests.cs` :
```csharp
using System.Windows;
using CustomNotch.App.Notch;
namespace CustomNotch.App.Tests;

public class PillShapeTests
{
    private static readonly PillMetrics M = new(1.0);

    [Fact]
    public void Les_metriques_reprennent_codenotch()
    {
        Assert.Equal(70, M.Width);
        Assert.Equal(20, M.Corner);
        Assert.Equal(38.7, M.Fillet, 3);
        Assert.Equal(2 * 18 + 3 * 44 + 3 * 18 + 2 * 14, M.Length(3), 3);   // padding, 3 anneaux + 3 légendes, 2 espaces
        Assert.Equal(M.Length(3) + 2 * M.Fillet, M.Extent(3), 3);
    }

    [Theory]
    [InlineData("right")]
    [InlineData("left")]
    public void Un_bord_vertical_donne_un_canvas_largeur_x_extent(string edge)
    {
        var g = PillShape.Build(M, edge, 2);
        var b = g.Bounds;
        Assert.Equal(0, b.X, 1); Assert.Equal(0, b.Y, 1);
        Assert.Equal(M.Width, b.Width, 1);
        Assert.Equal(M.Extent(2), b.Height, 1);
    }

    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    public void Un_bord_horizontal_est_tourne(string edge)
    {
        var b = PillShape.Build(M, edge, 2).Bounds;
        Assert.Equal(M.Extent(2), b.Width, 1);
        Assert.Equal(M.Width, b.Height, 1);
    }

    [Fact]
    public void Le_fillet_est_creux_et_l_oreille_pleine_bord_droit()
    {
        var g = PillShape.Build(M, "right", 1);
        var f = M.Fillet;
        Assert.True(g.FillContains(new Point(M.Width - 0.5, f - 0.5)), "le coin contre le bord, sous l'oreille, est noir");
        Assert.False(g.FillContains(new Point(M.Width - f + 0.5, 0.5)), "l'intérieur du quart de cercle est transparent");
        Assert.False(g.FillContains(new Point(0.5, f + 0.5)), "le coin libre est arrondi");
        Assert.True(g.FillContains(new Point(M.Width / 2, f + M.Length(1) / 2)), "le corps est plein");
    }

    [Fact]
    public void Le_bord_gauche_est_le_miroir()
    {
        var g = PillShape.Build(M, "left", 1);
        Assert.True(g.FillContains(new Point(0.5, M.Fillet - 0.5)));
        Assert.False(g.FillContains(new Point(M.Width - 0.5, M.Fillet + 0.5)));
    }
}
```

`tests/CustomNotch.App.Tests/EdgePlacementTests.cs` :
```csharp
using System.Windows;
using CustomNotch.App.Notch;
namespace CustomNotch.App.Tests;

public class EdgePlacementTests
{
    private static readonly Rect Area = new(0, 0, 1920, 1040);   // zone de travail : barre des tâches en bas

    [Fact]
    public void A_droite_le_canvas_colle_au_bord_et_suit_along()
    {
        var r = EdgePlacement.Place(Area, "right", 0.5, extent: 300, thickness: 70);
        Assert.Equal(1920 - 70, r.X);
        Assert.Equal((1040 - 300) / 2.0, r.Y);
        Assert.Equal(70, r.Width); Assert.Equal(300, r.Height);
        Assert.Equal(0, EdgePlacement.Place(Area, "right", 0, 300, 70).Y);
        Assert.Equal(1040 - 300, EdgePlacement.Place(Area, "right", 1, 300, 70).Y);
    }

    [Fact]
    public void A_gauche_en_haut_en_bas()
    {
        Assert.Equal(0, EdgePlacement.Place(Area, "left", 0.5, 300, 70).X);
        var top = EdgePlacement.Place(Area, "top", 0.25, 300, 70);
        Assert.Equal(0, top.Y); Assert.Equal((1920 - 300) * 0.25, top.X); Assert.Equal(300, top.Width); Assert.Equal(70, top.Height);
        Assert.Equal(1040 - 70, EdgePlacement.Place(Area, "bottom", 0.5, 300, 70).Y);
    }

    [Fact]
    public void Along_se_retrouve_depuis_une_position()
    {
        var r = EdgePlacement.Place(Area, "right", 0.3, 300, 70);
        Assert.Equal(0.3, EdgePlacement.AlongFrom(Area, "right", r.Location, 300), 6);
        Assert.Equal(1, EdgePlacement.AlongFrom(Area, "top", new Point(5000, 0), 300));
        Assert.Equal(0, EdgePlacement.AlongFrom(Area, "top", new Point(-50, 0), 300));
    }

    [Fact]
    public void Un_ecran_decale_est_respecte()
    {
        var second = new Rect(1920, -200, 2560, 1440);
        var r = EdgePlacement.Place(second, "right", 0, 300, 70);
        Assert.Equal(1920 + 2560 - 70, r.X); Assert.Equal(-200, r.Y);
    }
}
```

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.App.Tests`
Expected: erreurs de compilation.

- [ ] **Step 3: Métriques, forme, placement**

`src/CustomNotch.App/Notch/PillMetrics.cs` :
```csharp
namespace CustomNotch.App.Notch;

/// <summary>Les dimensions de codenotch, à l'échelle de la pilule. Tout le dessin part d'ici : un seul endroit à
/// changer pour une pilule plus dense.</summary>
public sealed record PillMetrics(double Scale)
{
    public double Width => 70 * Scale;
    public double Corner => 20 * Scale;
    public double Fillet => 38.7 * Scale;
    public double Padding => 18 * Scale;
    public double Ring => 44 * Scale;
    /// <summary>La hauteur réservée à la légende sous l'anneau (15 px de texte + son interligne).</summary>
    public double Caption => 18 * Scale;
    public double Gap => 14 * Scale;
    public double CardWidth => 246;
    public double Tail => 32;
    public double CardGap => 8;

    /// <summary>La longueur du corps pour n cellules ; une pilule vide garde la place d'une cellule.</summary>
    public double Length(int cells)
    {
        var n = Math.Max(1, cells);
        return 2 * Padding + n * (Ring + Caption) + (n - 1) * Gap;
    }

    public double Extent(int cells) => Length(cells) + 2 * Fillet;
}
```

`src/CustomNotch.App/Notch/PillShape.cs` :
```csharp
using System.Windows;
using System.Windows.Media;

namespace CustomNotch.App.Notch;

/// <summary>La silhouette : un corps aux coins arrondis côté libre, et deux fillets inversés côté écran qui la
/// raccordent au bord - le carré au-dessus du corps moins le quart de cercle, comme le ::before de codenotch.
/// Dessinée pour le bord droit, puis retournée pour les autres.</summary>
public static class PillShape
{
    public static Geometry Build(PillMetrics m, string edge, int cells)
    {
        var w = m.Width;
        var f = m.Fillet;
        var c = m.Corner;
        var length = m.Length(cells);
        var extent = m.Extent(cells);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(w, 0), isFilled: true, isClosed: true);
            // Oreille haute : du bord à la naissance du corps, en creux (petit arc, sens horaire à l'écran)
            ctx.ArcTo(new Point(w - f, f), new Size(f, f), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(c, f), true, false);
            // Coin libre haut (convexe)
            ctx.ArcTo(new Point(0, f + c), new Size(c, c), 0, false, SweepDirection.Counterclockwise, true, false);
            ctx.LineTo(new Point(0, f + length - c), true, false);
            // Coin libre bas
            ctx.ArcTo(new Point(c, f + length), new Size(c, c), 0, false, SweepDirection.Counterclockwise, true, false);
            ctx.LineTo(new Point(w - f, f + length), true, false);
            // Oreille basse
            ctx.ArcTo(new Point(w, extent), new Size(f, f), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(w, 0), true, false);
        }
        g.Transform = edge switch
        {
            "left" => new MatrixTransform(-1, 0, 0, 1, w, 0),
            "top" => new MatrixTransform(0, -1, 1, 0, 0, w),      // (x,y) → (y, w − x) : le bord x=w devient y=0
            "bottom" => new MatrixTransform(0, 1, -1, 0, extent, 0),   // (x,y) → (extent − y, x) : le bord devient y=w
            _ => Transform.Identity,
        };
        g.Freeze();
        return g;
    }
}
```

`src/CustomNotch.App/Notch/EdgePlacement.cs` :
```csharp
using System.Windows;

namespace CustomNotch.App.Notch;

/// <summary>Où poser le canvas de la pilule sur un écran : collé au bord demandé, à « along » de sa longueur.
/// Pur : l'écran est un rectangle en DIP, rien de WPF n'est touché.</summary>
public static class EdgePlacement
{
    public static bool IsVertical(string edge) => edge is "left" or "right";

    public static Rect Place(Rect area, string edge, double along, double extent, double thickness)
    {
        along = Math.Clamp(along, 0, 1);
        return edge switch
        {
            "left" => new Rect(area.Left, area.Top + along * (area.Height - extent), thickness, extent),
            "top" => new Rect(area.Left + along * (area.Width - extent), area.Top, extent, thickness),
            "bottom" => new Rect(area.Left + along * (area.Width - extent), area.Bottom - thickness, extent, thickness),
            _ => new Rect(area.Right - thickness, area.Top + along * (area.Height - extent), thickness, extent),
        };
    }

    public static double AlongFrom(Rect area, string edge, Point origin, double extent)
    {
        var room = IsVertical(edge) ? area.Height - extent : area.Width - extent;
        if (room <= 0) return 0;
        var offset = IsVertical(edge) ? origin.Y - area.Top : origin.X - area.Left;
        return Math.Clamp(offset / room, 0, 1);
    }
}
```

- [ ] **Step 4: Tests de géométrie verts**

Run: `dotnet test tests/CustomNotch.App.Tests`
Expected: PASS. Si un test de `FillContains` échoue, c'est le sens d'un arc : vérifier visuellement à l'étape 6 avant de toucher au test.

- [ ] **Step 5: PillWindow**

`src/CustomNotch.App/Notch/PillWindow.cs` (remplace la version provisoire) :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.Core.Config;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CustomNotch.App.Notch;

/// <summary>Une fenêtre par pilule : transparente, au-dessus de tout, jamais activée (WS_EX_NOACTIVATE | TOOLWINDOW),
/// hors barre des tâches et Alt-Tab. La fenêtre est plus grande que la pilule : elle réserve, côté libre, la place de
/// la carte hover et de sa queue, dessinées dans la même fenêtre pour ne jamais gérer de z-order. Les pixels
/// transparents laissent passer les clics.</summary>
public sealed class PillWindow : Window
{
    private readonly IPillHost _host;
    private readonly Canvas _canvas = new();
    private readonly Path _shape = new() { Fill = Brushes.Black, Stroke = new SolidColorBrush(Color.FromRgb(0x2e, 0x2e, 0x2e)), StrokeThickness = 1 };
    private readonly StackPanel _cells = new();
    private readonly Rectangle _grip = new() { Width = 4, Height = 28, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)) };
    private PillMetrics _m;
    private Point? _dragFrom;
    private Rect _dragArea;

    public PillWindow(PillConfig pill, IPillHost host)
    {
        _host = host;
        Pill = pill;
        _m = new PillMetrics(pill.Scale);
        Title = $"customNotch - {pill.Id}";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        _canvas.Children.Add(_shape);
        _canvas.Children.Add(_cells);
        _canvas.Children.Add(_grip);
        Content = _canvas;
        SourceInitialized += (_, _) => NoActivate();
        Loaded += (_, _) => Reposition();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        _shape.MouseEnter += (_, _) => _grip.Fill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
        _shape.MouseLeave += (_, _) => { if (_dragFrom is null) _grip.Fill = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)); };
        _grip.MouseEnter += (_, _) => _grip.Fill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
        _grip.Cursor = Cursors.SizeAll;
        _grip.MouseLeftButtonDown += OnDragStart;
        _grip.MouseMove += OnDragMove;
        _grip.MouseLeftButtonUp += OnDragEnd;
        _shape.MouseRightButtonUp += (_, e) => { ShowMenu(); e.Handled = true; };
        Layout();
    }

    public PillConfig Pill { get; private set; }
    private int CellCount => Math.Max(1, Pill.Cells.Count(c => c.Visible));
    private bool Vertical => EdgePlacement.IsVertical(Pill.Edge);

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Reposition);

    private void NoActivate()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = Native.GetWindowLong(hwnd, Native.GwlExStyle);
        Native.SetWindowLong(hwnd, Native.GwlExStyle, style | Native.WsExToolWindow | Native.WsExNoActivate);
    }

    public void Apply(PillConfig pill)
    {
        Pill = pill;
        _m = new PillMetrics(pill.Scale);
        Layout();
        if (IsLoaded) Reposition();
    }

    /// <summary>Taille de la fenêtre et position de chaque élément dans le canvas. Le canvas de la pilule est posé
    /// contre le bord ; la réserve de la carte est côté libre.</summary>
    private void Layout()
    {
        var extent = _m.Extent(CellCount);
        var reserve = _m.CardWidth + _m.Tail + _m.CardGap;
        _shape.Data = PillShape.Build(_m, Pill.Edge, CellCount);
        if (Vertical)
        {
            Width = _m.Width + reserve;
            Height = Math.Max(extent, 480);
            var top = (Height - extent) / 2;
            var left = Pill.Edge == "right" ? reserve : 0;
            Canvas.SetLeft(_shape, left); Canvas.SetTop(_shape, top);
            _cells.Orientation = Orientation.Vertical;
            Canvas.SetLeft(_cells, left); Canvas.SetTop(_cells, top + _m.Fillet + _m.Padding);
            _cells.Width = _m.Width;
            _grip.Width = 4; _grip.Height = 28;
            Canvas.SetLeft(_grip, left + (Pill.Edge == "right" ? 6 : _m.Width - 10)); Canvas.SetTop(_grip, top + _m.Fillet + 4);
        }
        else
        {
            Width = Math.Max(extent, 480);
            Height = _m.Width + reserve;
            var left = (Width - extent) / 2;
            var top = Pill.Edge == "bottom" ? reserve : 0;
            Canvas.SetLeft(_shape, left); Canvas.SetTop(_shape, top);
            _cells.Orientation = Orientation.Horizontal;
            Canvas.SetLeft(_cells, left + _m.Fillet + _m.Padding); Canvas.SetTop(_cells, top);
            _cells.Height = _m.Width;
            _grip.Width = 28; _grip.Height = 4;
            Canvas.SetLeft(_grip, left + _m.Fillet + 4); Canvas.SetTop(_grip, top + (Pill.Edge == "bottom" ? 6 : _m.Width - 10));
        }
    }

    /// <summary>L'écran demandé (ou le principal), sa zone de travail en DIP, et la position qui en découle.</summary>
    public void Reposition()
    {
        var screen = Screen();
        var area = Screens.ToDip(this, screen.WorkingArea);
        var extent = _m.Extent(CellCount);
        var pill = EdgePlacement.Place(area, Pill.Edge, Pill.Along, extent, _m.Width);
        MoveTo(pill);
    }

    private void MoveTo(Rect pill)
    {
        var reserve = _m.CardWidth + _m.Tail + _m.CardGap;
        var extent = _m.Extent(CellCount);
        (Left, Top) = Pill.Edge switch
        {
            "right" => (pill.X - reserve, pill.Y - (Height - extent) / 2),
            "left" => (pill.X, pill.Y - (Height - extent) / 2),
            "top" => (pill.X - (Width - extent) / 2, pill.Y),
            _ => (pill.X - (Width - extent) / 2, pill.Y - reserve),
        };
    }

    private System.Windows.Forms.Screen Screen()
    {
        var all = System.Windows.Forms.Screen.AllScreens;
        return all.FirstOrDefault(s => s.DeviceName == Pill.Screen) ?? System.Windows.Forms.Screen.PrimaryScreen ?? all[0];
    }

    // ---- drag le long du bord (et d'un écran à l'autre) ----------------------------------------------------------

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        _dragFrom = e.GetPosition(this);
        _grip.CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (_dragFrom is not { } from) return;
        var cursor = Screens.CursorDip(this);
        var (sx, sy) = Screens.Scale(this);
        var device = new System.Drawing.Point((int)Math.Round(cursor.X * sx), (int)Math.Round(cursor.Y * sy));
        _dragArea = Screens.WorkArea(this, device);
        var extent = _m.Extent(CellCount);
        // La pilule suit le curseur le long du bord de l'écran sous la souris ; l'autre axe reste collé au bord.
        var origin = Vertical ? new Point(0, cursor.Y - from.Y + (Height - extent) / 2) : new Point(cursor.X - from.X + (Width - extent) / 2, 0);
        var along = EdgePlacement.AlongFrom(_dragArea, Pill.Edge, origin, extent);
        MoveTo(EdgePlacement.Place(_dragArea, Pill.Edge, along, extent, _m.Width));
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (_dragFrom is null) return;
        _dragFrom = null;
        _grip.ReleaseMouseCapture();
        var extent = _m.Extent(CellCount);
        var origin = Vertical ? new Point(0, Top + (Height - extent) / 2) : new Point(Left + (Width - extent) / 2, 0);
        var along = EdgePlacement.AlongFrom(_dragArea, Pill.Edge, origin, extent);
        var (sx, sy) = Screens.Scale(this);
        var center = new System.Drawing.Point((int)Math.Round((_dragArea.X + _dragArea.Width / 2) * sx), (int)Math.Round((_dragArea.Y + _dragArea.Height / 2) * sy));
        var screen = System.Windows.Forms.Screen.FromPoint(center).DeviceName;
        _host.SavePosition(Pill.Id, along, screen);
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("Rafraîchir cette pilule", () => { foreach (var c in Pill.Cells) _host.RequestRefresh(c.Id); });
        Add("Masquer cette pilule", () => _host.HidePill(Pill.Id));
        menu.Items.Add(new Separator());
        Add("Réglages…", _host.ShowSettings);
        menu.IsOpen = true;
    }

    public void UpdateCell(string cellId) { }
    public void Tick() { }
}
```

- [ ] **Step 6: Vérification à l'écran**

Run: `dotnet run --project src/CustomNotch.App`
Expected : une pilule noire vide, liseré gris, coins inversés côté bord, collée au bord droit, centrée verticalement. Survol : un petit grip blanc apparaît en haut ; le tirer déplace la pilule le long du bord, y compris vers un second écran ; au relâcher `cells.<machine>.json` contient `along` et `screen`, et la pilule y revient au relancement. Cliquer sur le fond transparent atteint la fenêtre dessous. Clic droit sur la pilule → menu. Modifier `"edge": "top"` dans `cells.json` : la pilule se replace en haut, horizontale, sans relancer.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(app): pilule aux fillets inversés, placement au bord, drag et menu"
```

---

### Task 10: Rendu des cellules — anneau, valeur, statut, sparkline

**Files:**
- Create: `src/CustomNotch.App/Cells/StatusPalette.cs`, `Cells/GlyphLibrary.cs`, `Cells/RingArc.cs`, `Cells/RingCell.cs`, `Cells/ValueCell.cs`, `Cells/StatusCell.cs`, `Cells/SparklineCell.cs`, `Cells/CellHost.cs`
- Modify: `src/CustomNotch.App/Notch/PillWindow.cs` (`Layout`, `UpdateCell`, `Tick`)
- Test: `tests/CustomNotch.App.Tests/RingArcTests.cs`, `tests/CustomNotch.App.Tests/GlyphLibraryTests.cs`

**Interfaces:**
- Consumes: `CellView`, `CellKind`, `Status`, `PillMetrics`, `IPillHost`, `Glyphs.Frozen`-style parsing.
- Produces: `StatusPalette.Color(Status)`, `StatusPalette.Brush(Status)` ; `GlyphLibrary.Get(string? name) → (Geometry Data, bool Filled)` ; `RingArc.Geometry(double fraction, double radius, double startDeg = -90) → Geometry` ; `abstract class CellFace : Grid { abstract void Render(CellView view, PillMetrics m); }` avec `RingCell`, `ValueCell`, `StatusCell`, `SparklineCell` ; `CellHost : StackPanel` (`ctor(CellConfig, PillMetrics)`, `Cell`, `Render(CellView)`, `Tick()`, events `Hovered(CellHost)`, `Unhovered(CellHost)`, `Clicked(CellHost)`, `RightClicked(CellHost)`).

- [ ] **Step 1: Tests de l'arc et des glyphes**

`tests/CustomNotch.App.Tests/RingArcTests.cs` :
```csharp
using System.Windows;
using CustomNotch.App.Cells;
namespace CustomNotch.App.Tests;

public class RingArcTests
{
    [Fact]
    public void Zero_ne_dessine_rien_et_un_dessine_presque_le_tour()
    {
        Assert.True(RingArc.Geometry(0, 20).IsEmpty());
        var full = RingArc.Geometry(1, 20).Bounds;
        Assert.Equal(40, full.Width, 0.5);
        Assert.Equal(40, full.Height, 0.5);
    }

    [Fact]
    public void Un_quart_part_de_midi_vers_trois_heures()
    {
        var b = RingArc.Geometry(0.25, 20).Bounds;
        Assert.Equal(20, b.Width, 0.5);   // de x=20 (midi) à x=40 (3 h)
        Assert.Equal(20, b.Height, 0.5);
        Assert.Equal(20, b.X, 0.5); Assert.Equal(0, b.Y, 0.5);
    }
}
```

`tests/CustomNotch.App.Tests/GlyphLibraryTests.cs` :
```csharp
using CustomNotch.App.Cells;
namespace CustomNotch.App.Tests;

public class GlyphLibraryTests
{
    [Theory]
    [InlineData("cpu")] [InlineData("memory")] [InlineData("disk")] [InlineData("network")] [InlineData("battery")]
    [InlineData("link")] [InlineData("globe")] [InlineData("terminal")] [InlineData("folder")] [InlineData("claude")]
    public void Les_glyphes_nommes_existent(string name) => Assert.False(GlyphLibrary.Get(name).Data.IsEmpty());

    [Fact]
    public void Un_trace_brut_est_accepte_et_plein()
    {
        var (data, filled) = GlyphLibrary.Get("M0,0 L10,5 L0,10 Z");
        Assert.True(filled);
        Assert.Equal(10, data.Bounds.Width, 0.1);
    }

    [Fact]
    public void Inconnu_ou_vide_donne_le_point()
    {
        Assert.Equal(GlyphLibrary.Get("dot").Data.ToString(), GlyphLibrary.Get("zzz").Data.ToString());
        Assert.Equal(GlyphLibrary.Get("dot").Data.ToString(), GlyphLibrary.Get(null).Data.ToString());
    }
}
```

- [ ] **Step 2: Palette, glyphes, arc**

`src/CustomNotch.App/Cells/StatusPalette.cs` :
```csharp
using System.Windows.Media;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Les couleurs de la pilule, fixes quel que soit le thème : c'est son identité. Vert, jaune, rouge-orange
/// comme les anneaux de codenotch ; bleu pour un travail en cours, ambre pour « on t'attend ».</summary>
public static class StatusPalette
{
    public static readonly Color Track = Color.FromRgb(0x30, 0x30, 0x30);
    public static readonly Color Ink = Color.FromRgb(0xe8, 0xe8, 0xea);
    public static readonly Color Dim = Color.FromRgb(0x80, 0x80, 0x80);
    public static readonly Color CardBg = Color.FromRgb(0x0a, 0x0a, 0x0a);

    public static Color Color(Status status) => status switch
    {
        Status.Ok => System.Windows.Media.Color.FromRgb(0x30, 0xd1, 0x58),
        Status.Warn => System.Windows.Media.Color.FromRgb(0xff, 0xd6, 0x0a),
        Status.Crit => System.Windows.Media.Color.FromRgb(0xff, 0x45, 0x3a),
        Status.Busy => System.Windows.Media.Color.FromRgb(0x0a, 0x84, 0xff),
        Status.Attention => System.Windows.Media.Color.FromRgb(0xff, 0x9f, 0x0a),
        _ => Dim,
    };

    public static SolidColorBrush Brush(Status status) => Frozen(Color(status));

    public static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
```

`src/CustomNotch.App/Cells/GlyphLibrary.cs` :
```csharp
using System.Windows.Media;

namespace CustomNotch.App.Cells;

/// <summary>Les icônes des cellules par nom ; un tracé SVG brut (« M… ») est accepté tel quel dans cells.json. Les
/// icônes au trait sont dans une grille 16 × 16 comme Glyphs.cs ; « claude » est le logo plein (codenotch, MIT),
/// grille 24 × 24.</summary>
public static class GlyphLibrary
{
    private static readonly Dictionary<string, (string Path, bool Filled)> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dot"] = ("M8,8 L8,8.05", false),
        ["cpu"] = ("M4.5,4.5 L11.5,4.5 L11.5,11.5 L4.5,11.5 Z M6.5,6.5 L9.5,6.5 L9.5,9.5 L6.5,9.5 Z M6,2 L6,4.5 M10,2 L10,4.5 M6,11.5 L6,14 M10,11.5 L10,14 M2,6 L4.5,6 M2,10 L4.5,10 M11.5,6 L14,6 M11.5,10 L14,10", false),
        ["memory"] = ("M2,5 L14,5 L14,11 L2,11 Z M4,11 L4,13.5 M7,11 L7,13.5 M9,11 L9,13.5 M12,11 L12,13.5 M4.5,7 L4.5,9 M7,7 L7,9 M9.5,7 L9.5,9 M12,7 L12,9", false),
        ["disk"] = ("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,6.5 A1.5,1.5 0 1 1 7.99,6.5 Z M13,10 L10.5,8.5", false),
        ["network"] = ("M8,2.5 L8,13.5 M4.5,6 L8,2.5 L11.5,6 M4.5,10 L8,13.5 L11.5,10", false),
        ["battery"] = ("M2.5,5 L12.5,5 L12.5,11 L2.5,11 Z M12.5,7 L14,7 L14,9 L12.5,9 M4.5,7 L8,7 L8,9 L4.5,9 Z", false),
        ["link"] = ("M6.5,9.5 L9.5,6.5 M7,4.8 L8.4,3.4 A2.6,2.6 0 0 1 12.6,7.6 L11.2,9 M9,11.2 L7.6,12.6 A2.6,2.6 0 0 1 3.4,8.4 L4.8,7", false),
        ["globe"] = ("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M2.5,8 L13.5,8 M8,2.5 C5.5,5 5.5,11 8,13.5 M8,2.5 C10.5,5 10.5,11 8,13.5", false),
        ["terminal"] = ("M2.5,3 L13.5,3 L13.5,13 L2.5,13 Z M5,6 L7.5,8 L5,10 M8.5,10.5 L11,10.5", false),
        ["folder"] = ("M2.5,4 L6.5,4 L8,5.5 L13.5,5.5 L13.5,12.5 L2.5,12.5 Z", false),
        ["open"] = ("M6.5,3 L3,3 L3,13 L13,13 L13,9.5 M9,3 L13,3 L13,7 M13,3 L7.5,8.5", false),
        ["play"] = ("M0,0 L10,5 L0,10 Z", true),
        ["pause"] = ("M0,0 L3,0 L3,10 L0,10 Z M6,0 L9,0 L9,10 L6,10 Z", true),
        ["refresh"] = ("M13,8 A5,5 0 1 1 11.6,4.4 M11.8,2.2 L11.8,4.8 L9.2,4.8", false),
        ["gear"] = ("M8,5.6 A2.4,2.4 0 1 1 7.99,5.6 Z M8,1.5 L8,3.4 M8,12.6 L8,14.5 M1.5,8 L3.4,8 M12.6,8 L14.5,8 M3.4,3.4 L4.75,4.75 M11.25,11.25 L12.6,12.6 M3.4,12.6 L4.75,11.25 M11.25,4.75 L12.6,3.4", false),
        ["bell"] = ("M4,11.5 L12,11.5 L11,10 L11,7 A3,3 0 0 0 5,7 L5,10 Z M6.8,13.5 L9.2,13.5", false),
        ["clock"] = ("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,4.8 L8,8.2 L10.6,9.6", false),
        ["chart"] = ("M3,13.5 L13,13.5 M5,13.5 L5,8.5 M8,13.5 L8,4.5 M11,13.5 L11,10", false),
        ["music"] = ("M6,12 A2,2 0 1 1 5.99,12 Z M12,10.5 A2,2 0 1 1 11.99,10.5 Z M8,12 L8,4 L14,2.5 L14,10.5", false),
        ["check"] = ("M2.5,8.5 L6.5,12.5 L13.5,4", false),
        ["claude"] = ("M4.709 15.955l4.72-2.647.08-.23-.08-.128H9.2l-.79-.048-2.698-.073-2.339-.097-2.266-.122-.571-.121L0 11.784l.055-.352.48-.321.686.06 1.52.103 2.278.158 1.652.097 2.449.255h.389l.055-.157-.134-.098-.103-.097-2.358-1.596-2.552-1.688-1.336-.972-.724-.491-.364-.462-.158-1.008.656-.722.881.06.225.061.893.686 1.908 1.476 2.491 1.833.365.304.145-.103.019-.073-.164-.274-1.355-2.446-1.446-2.49-.644-1.032-.17-.619a2.97 2.97 0 01-.104-.729L6.283.134 6.696 0l.996.134.42.364.62 1.414 1.002 2.229 1.555 3.03.456.898.243.832.091.255h.158V9.01l.128-1.706.237-2.095.23-2.695.08-.76.376-.91.747-.492.584.28.48.685-.067.444-.286 1.851-.559 2.903-.364 1.942h.212l.243-.242.985-1.306 1.652-2.064.73-.82.85-.904.547-.431h1.033l.76 1.129-.34 1.166-1.064 1.347-.881 1.142-1.264 1.7-.79 1.36.073.11.188-.02 2.856-.606 1.543-.28 1.841-.315.833.388.091.395-.328.807-1.969.486-2.309.462-3.439.813-.042.03.049.061 1.549.146.662.036h1.622l3.02.225.79.522.474.638-.079.485-1.215.62-1.64-.389-3.829-.91-1.312-.329h-.182v.11l1.093 1.068 2.006 1.81 2.509 2.33.127.578-.322.455-.34-.049-2.205-1.657-.851-.747-1.926-1.62h-.128v.17l.444.649 2.345 3.521.122 1.08-.17.353-.608.213-.668-.122-1.374-1.925-1.415-2.167-1.143-1.943-.14.08-.674 7.254-.316.37-.729.28-.607-.461-.322-.747.322-1.476.389-1.924.315-1.53.286-1.9.17-.632-.012-.042-.14.018-1.434 1.967-2.18 2.945-1.726 1.845-.414.164-.717-.37.067-.662.401-.589 2.388-3.036 1.44-1.882.93-1.086-.006-.158h-.055L4.132 18.56l-1.13.146-.487-.456.061-.746.231-.243 1.908-1.312-.006.006z", true),
    };

    private static readonly Dictionary<string, (Geometry, bool)> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static (Geometry Data, bool Filled) Get(string? name)
    {
        var key = string.IsNullOrWhiteSpace(name) ? "dot" : name.Trim();
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var hit)) return hit;
            (Geometry, bool) result;
            if (Named.TryGetValue(key, out var named)) result = (Parse(named.Path), named.Filled);
            else if (key.StartsWith('M') || key.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
            {
                try { result = (Parse(key.StartsWith("path:", StringComparison.OrdinalIgnoreCase) ? key[5..] : key), true); }
                catch (FormatException) { result = (Parse(Named["dot"].Path), false); }
            }
            else result = (Parse(Named["dot"].Path), false);
            Cache[key] = result;
            return result;
        }
    }

    private static Geometry Parse(string path)
    {
        var g = Geometry.Parse(path);
        g.Freeze();
        return g;
    }
}
```

`src/CustomNotch.App/Cells/RingArc.cs` :
```csharp
using System.Windows;
using System.Windows.Media;

namespace CustomNotch.App.Cells;

/// <summary>L'arc d'un anneau : part de midi, tourne dans le sens horaire, dans un carré de côté 2·radius.
/// À 100 % on s'arrête à 359,9° : un arc complet a le même point de départ et d'arrivée et WPF ne le trace pas.</summary>
public static class RingArc
{
    public static Geometry Geometry(double fraction, double radius, double startDeg = -90)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0) return System.Windows.Media.Geometry.Empty;
        var sweep = Math.Min(fraction * 360, 359.9);
        var center = new Point(radius, radius);
        Point At(double deg)
        {
            var rad = deg * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(At(startDeg), false, false);
            ctx.ArcTo(At(startDeg + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }
}
```

- [ ] **Step 3: Tests verts**

Run: `dotnet test tests/CustomNotch.App.Tests`
Expected: PASS.

- [ ] **Step 4: Les faces de cellule**

`src/CustomNotch.App/Cells/CellFace.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Le carré de la cellule (Ring × Ring) : chaque type dessine dedans. Les deux animations d'activité sont
/// communes : un arc fin qui tourne (Busy) et une pulsation ambre (Attention).</summary>
public abstract class CellFace : Grid
{
    private readonly Path _activity = new() { StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly RotateTransform _spin = new();
    private bool _spinning;

    protected CellFace()
    {
        _activity.RenderTransform = _spin;
        Children.Add(_activity);
        Panel.SetZIndex(_activity, 10);
    }

    public abstract void Render(CellView view, PillMetrics m);

    /// <summary>Le glyph au centre, 26/44 de la cellule, blanc cassé (ou grisé si périmé).</summary>
    protected static Path Glyph(string? name, PillMetrics m, bool stale)
    {
        var (data, filled) = GlyphLibrary.Get(name);
        var size = m.Ring * 26 / 44;
        var brush = StatusPalette.Frozen(stale ? StatusPalette.Dim : StatusPalette.Ink);
        var path = filled ? Glyphs.Fill(data, size, brush) : Glyphs.Stroke(data, size, brush, 1.6);
        path.Width = size; path.Height = size;
        return path;
    }

    protected void Activity(Status status, PillMetrics m)
    {
        Width = m.Ring; Height = m.Ring;
        if (status is Status.Busy or Status.Attention)
        {
            var r = m.Ring / 2 - 1.25;
            _activity.Data = RingArc.Geometry(status == Status.Busy ? 0.25 : 1, r);
            _activity.Width = 2 * r; _activity.Height = 2 * r;
            _activity.Stroke = StatusPalette.Brush(status);
            _activity.Visibility = Visibility.Visible;
            if (status == Status.Busy && !_spinning)
            {
                _spinning = true;
                _spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = RepeatBehavior.Forever });
                _activity.BeginAnimation(OpacityProperty, null);
                _activity.Opacity = 1;
            }
            else if (status == Status.Attention)
            {
                StopSpin();
                _activity.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromSeconds(0.55)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            }
        }
        else
        {
            StopSpin();
            _activity.BeginAnimation(OpacityProperty, null);
            _activity.Visibility = Visibility.Collapsed;
        }
    }

    private void StopSpin()
    {
        if (!_spinning) return;
        _spinning = false;
        _spin.BeginAnimation(RotateTransform.AngleProperty, null);
        _spin.Angle = 0;
    }
}
```

`src/CustomNotch.App/Cells/RingCell.cs` :
```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>L'anneau de codenotch : piste grise, arc coloré par le statut, glyph au centre.</summary>
public sealed class RingCell : CellFace
{
    private readonly Ellipse _track = new() { Stroke = StatusPalette.Frozen(StatusPalette.Track), StrokeThickness = 5 };
    private readonly Path _arc = new() { StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private Path? _glyph;

    public RingCell()
    {
        Children.Add(_track);
        Children.Add(_arc);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        var stroke = 5 * m.Scale;
        var r = m.Ring / 2 - stroke / 2;
        _track.Width = _track.Height = 2 * r + stroke; _track.StrokeThickness = stroke;
        _arc.StrokeThickness = stroke;
        _arc.Width = _arc.Height = 2 * r;
        _arc.Data = RingArc.Geometry(view.Fraction ?? 0, r);
        _arc.Stroke = StatusPalette.Brush(view.Status is Status.Busy or Status.Attention ? Status.Ok : view.Status);
        _arc.Opacity = view.Stale ? 0.55 : 1;
        _track.Opacity = view.Stale ? 0.55 : 1;
        if (_glyph is not null) Children.Remove(_glyph);
        _glyph = Glyph(view.Glyph, m, view.Stale);
        Children.Add(_glyph);
        Activity(view.Status, m);
    }
}
```

`src/CustomNotch.App/Cells/ValueCell.cs` :
```csharp
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Un disque à piste fine et le glyph : la valeur, elle, est dans la légende sous la cellule.</summary>
public sealed class ValueCell : CellFace
{
    private readonly Ellipse _disc = new() { Fill = StatusPalette.Frozen(StatusPalette.Track) };
    private Path? _glyph;

    public ValueCell() => Children.Add(_disc);

    public override void Render(CellView view, PillMetrics m)
    {
        _disc.Width = _disc.Height = m.Ring;
        _disc.Opacity = view.Stale ? 0.55 : 1;
        if (_glyph is not null) Children.Remove(_glyph);
        _glyph = Glyph(view.Glyph, m, view.Stale);
        Children.Add(_glyph);
        Activity(view.Status, m);
    }
}
```

`src/CustomNotch.App/Cells/StatusCell.cs` :
```csharp
using System.Windows;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Le glyph et une pastille de statut en bas à droite ; Off = glyph seul, gris.</summary>
public sealed class StatusCell : CellFace
{
    private readonly Ellipse _disc = new() { Fill = StatusPalette.Frozen(StatusPalette.Track) };
    private readonly Ellipse _dot = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, StrokeThickness = 2, Stroke = System.Windows.Media.Brushes.Black };
    private Path? _glyph;

    public StatusCell()
    {
        Children.Add(_disc);
        Children.Add(_dot);
        Panel.SetZIndex(_dot, 5);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        _disc.Width = _disc.Height = m.Ring;
        _dot.Width = _dot.Height = 11 * m.Scale;
        _dot.Fill = StatusPalette.Brush(view.Status);
        _dot.Visibility = view.Status == Status.Off ? Visibility.Collapsed : Visibility.Visible;
        if (_glyph is not null) Children.Remove(_glyph);
        _glyph = Glyph(view.Glyph, m, view.Stale || view.Status == Status.Off);
        Children.Add(_glyph);
        Activity(view.Status, m);
    }
}
```

`src/CustomNotch.App/Cells/SparklineCell.cs` :
```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Les 30 derniers points, mis à l'échelle du maximum observé, dans le carré de la cellule.</summary>
public sealed class SparklineCell : CellFace
{
    private readonly Ellipse _disc = new() { Fill = StatusPalette.Frozen(StatusPalette.Track) };
    private readonly Polyline _line = new() { StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };

    public SparklineCell()
    {
        Children.Add(_disc);
        Children.Add(_line);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        _disc.Width = _disc.Height = m.Ring;
        _line.Stroke = StatusPalette.Brush(view.Status == Status.Off ? Status.Ok : view.Status);
        _line.Opacity = view.Stale ? 0.55 : 1;
        var points = (view.Reading.History ?? Array.Empty<(long, double)>()).TakeLast(30).Select(p => p.V).ToList();
        var inset = 9 * m.Scale;
        var w = m.Ring - 2 * inset;
        var h = m.Ring - 2 * inset;
        var max = Math.Max(points.Count > 0 ? points.Max() : 1, 1e-9);
        _line.Points = new PointCollection(points.Select((v, i) => new Point(inset + (points.Count > 1 ? i * w / (points.Count - 1) : w / 2), inset + h - v / max * h)));
        Activity(view.Status, m);
    }
}
```

- [ ] **Step 5: CellHost — face + légende + événements**

`src/CustomNotch.App/Cells/CellHost.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CustomNotch.App.Notch;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Une cellule dans la pilule : la face (anneau, valeur, statut, sparkline) et la légende dessous. Change de
/// face si le type déduit change. Pression : la face se rétracte à 93 % puis revient (codenotch .pressed).</summary>
public sealed class CellHost : StackPanel
{
    public event Action<CellHost>? Hovered;
    public event Action<CellHost>? Unhovered;
    public event Action<CellHost>? Clicked;
    public event Action<CellHost>? RightClicked;

    private readonly PillMetrics _m;
    private readonly Grid _faceSlot = new() { RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly ScaleTransform _press = new(1, 1);
    private readonly TextBlock _caption = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
    private CellFace? _face;
    private CellKind? _kind;

    public CellHost(CellConfig cell, PillMetrics m)
    {
        Cell = cell;
        _m = m;
        Orientation = Orientation.Vertical;
        HorizontalAlignment = HorizontalAlignment.Center;
        Margin = new Thickness(0, 0, 0, m.Gap);
        Width = m.Width;
        _caption.FontSize = 15 * m.Scale;
        _caption.Height = m.Caption;
        _caption.Margin = new Thickness(0, 6 * m.Scale - 6, 0, 0);
        TextOptions.SetTextFormattingMode(_caption, TextFormattingMode.Display);
        _caption.SetValue(Typography.NumeralAlignmentProperty, FontNumeralAlignment.Tabular);
        _faceSlot.RenderTransform = _press;
        _faceSlot.Width = m.Ring; _faceSlot.Height = m.Ring;
        _faceSlot.HorizontalAlignment = HorizontalAlignment.Center;
        _faceSlot.Background = Brushes.Transparent;   // pour recevoir la souris sur toute la face
        Children.Add(_faceSlot);
        Children.Add(_caption);
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;
        MouseEnter += (_, _) => Hovered?.Invoke(this);
        MouseLeave += (_, _) => Unhovered?.Invoke(this);
        MouseLeftButtonDown += (_, e) => { Press(0.93); e.Handled = true; };
        MouseLeftButtonUp += (_, e) => { Press(1); Clicked?.Invoke(this); e.Handled = true; };
        MouseRightButtonUp += (_, e) => { RightClicked?.Invoke(this); e.Handled = true; };
    }

    public CellConfig Cell { get; private set; }
    public CellView? Last { get; private set; }

    public void Rebind(CellConfig cell) => Cell = cell;

    /// <summary>Pour un bord horizontal : la cellule ne change pas, seule sa marge passe à droite.</summary>
    public void SetHorizontal(bool horizontal) => Margin = horizontal ? new Thickness(0, 0, _m.Gap, 0) : new Thickness(0, 0, 0, _m.Gap);

    private void Press(double to)
        => _press.BeginAnimation(ScaleTransform.ScaleXProperty, Spring(to)); _press.BeginAnimation(ScaleTransform.ScaleYProperty, Spring(to));

    private static DoubleAnimation Spring(double to)
        => new(to, TimeSpan.FromMilliseconds(300)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 } };

    public void Render(CellView view)
    {
        Last = view;
        if (_face is null || _kind != view.Kind)
        {
            _faceSlot.Children.Clear();
            _kind = view.Kind;
            _face = view.Kind switch
            {
                CellKind.Value => new ValueCell(),
                CellKind.Status => new StatusCell(),
                CellKind.Sparkline => new SparklineCell(),
                _ => new RingCell(),   // Group : l'anneau de l'enfant en tête
            };
            _faceSlot.Children.Add(_face);
        }
        _face.Render(view, _m);
        _caption.Text = view.Caption ?? "";
        _caption.Opacity = view.Stale ? 0.55 : 1;
        ToolTip = null;   // la carte hover remplace l'infobulle
    }
}
```
Note : la ligne `Press` ci-dessus tient sur deux instructions ; l'écrire avec des accolades.

- [ ] **Step 6: Brancher les cellules dans PillWindow**

Dans `src/CustomNotch.App/Notch/PillWindow.cs` :
- champ : `private readonly Dictionary<string, Cells.CellHost> _hosts = new();`
- dans `Layout()`, avant le calcul des tailles, reconstruire `_cells.Children` :
```csharp
    private void RebuildCells()
    {
        _cells.Children.Clear();
        var horizontal = !Vertical;
        foreach (var cell in Pill.Cells.Where(c => c.Visible))
        {
            if (!_hosts.TryGetValue(cell.Id, out var host) || host.Cell.Source != cell.Source)
            {
                host = new Cells.CellHost(cell, _m);
                host.Hovered += OnCellHovered;
                host.Unhovered += OnCellUnhovered;
                host.Clicked += OnCellClicked;
                host.RightClicked += h => { ShowMenu(); };
                _hosts[cell.Id] = host;
            }
            host.Rebind(cell);
            host.SetHorizontal(horizontal);
            _cells.Children.Add(host);
            if (_host.View(cell.Id) is { } view) host.Render(view);
        }
        foreach (var id in _hosts.Keys.Where(id => Pill.Cells.All(c => c.Id != id)).ToList()) _hosts.Remove(id);
    }
```
  Appeler `RebuildCells()` en tête de `Layout()`. Pour un bord horizontal, `_cells` doit être un `StackPanel` horizontal et chaque `CellHost` centré verticalement : mettre `_cells.VerticalAlignment = VerticalAlignment.Center` et, dans `Layout()`, `Canvas.SetTop(_cells, top + (_m.Width - (_m.Ring + _m.Caption)) / 2)` dans la branche horizontale.
- `UpdateCell` :
```csharp
    public void UpdateCell(string cellId)
    {
        if (_hosts.TryGetValue(cellId, out var host) && _host.View(cellId) is { } view) host.Render(view);
        foreach (var group in Pill.Cells.Where(c => c.IsGroup && c.Children!.Contains(cellId)))
            if (_hosts.TryGetValue(group.Id, out var g) && _host.View(group.Id) is { } gv) g.Render(gv);
    }
```
- `Tick` : `foreach (var (id, host) in _hosts) if (host.Last is { Stale: true } && _host.View(id) is { } v) host.Render(v);` (l'âge affiché vieillit).
- Stubs pour la tâche 11 : `private void OnCellHovered(Cells.CellHost h) { }`, `private void OnCellUnhovered(Cells.CellHost h) { }`, `private void OnCellClicked(Cells.CellHost h) { }`.

- [ ] **Step 7: Vérification à l'écran**

Run: `dotnet run --project src/CustomNotch.App`
Expected : cinq cellules — CPU, mémoire, disque en anneaux colorés (vert/jaune/rouge selon les seuils) avec `NN%` dessous ; réseau en sparkline avec `N Ko/s` ; ClickUp en statut gris (glyph lien, pas de pastille). Les valeurs bougent. Cliquer une cellule : elle se rétracte et revient. Débrancher le réseau ou mettre `"drive": "Q:"` : la cellule passe grisée (périmée), pas vide.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "feat(app): rendu des cellules — anneau, valeur, statut, sparkline, activité"
```

---

### Task 11: Carte hover — détails, barres, actions, queue

**Files:**
- Create: `src/CustomNotch.App/Notch/HoverCard.cs`, `src/CustomNotch.App/Notch/CardContent.cs`
- Modify: `src/CustomNotch.App/Notch/PillWindow.cs` (`OnCellHovered`, `OnCellUnhovered`, `OnCellClicked`, placement de la carte)
- Test: `tests/CustomNotch.App.Tests/CardContentTests.cs`

**Interfaces:**
- Consumes: `CellView`, `DetailRow`, `ActionSpec`, `ActionConfig`, `IPillHost`, `PillMetrics`, `StatusPalette`, styles `CardButton`.
- Produces: `record CardRow(string Label, string? Text, double? Fraction, string? Hint, Status Tone)`, `record CardAction(string Label, string? Icon, ActionConfig? Config, string? SourceAction)`, `record CardModel(string Title, string? Glyph, string? Subtitle, IReadOnlyList<CardRow> Rows, IReadOnlyList<CardAction> Actions, string? Note)` ; `CardContent.Build(CellView view, CellConfig cell, IReadOnlyList<CellView> children) → CardModel` (pur) ; `HoverCard : Border` (`ctor(IPillHost, PillMetrics)`, `Show(CardModel model, string cellId)`, `HideLater()`, `CancelHide()`, `IsPinned`, `Tail` (un `Path` posé par la fenêtre)).

- [ ] **Step 1: Tests du modèle de carte**

`tests/CustomNotch.App.Tests/CardContentTests.cs` :
```csharp
using CustomNotch.App.Notch;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
namespace CustomNotch.App.Tests;

public class CardContentTests
{
    private static CellView View(string id, Reading r, string? label = null, bool stale = false)
        => CellViews.From(new CellConfig { Id = id, Label = label ?? id, Glyph = "cpu" }, stale ? r.AsStale(0, "réseau") : r, 60_000);

    [Fact]
    public void Une_cellule_simple_liste_ses_details_et_actions()
    {
        var reading = new Reading(Value: 73, Max: 100, Detail: new[] { new DetailRow("Session", "73 %", 0.73, "reset dans 51 min") }, Actions: new[] { new ActionSpec("refresh", "Rafraîchir") });
        var cell = new CellConfig { Id = "c", Label = "Claude", Actions = new CellActions { Card = new() { new ActionConfig { Label = "Ouvrir", Open = "https://claude.ai" } } } };
        var model = CardContent.Build(View("c", reading, "Claude"), cell, Array.Empty<CellView>());
        Assert.Equal("Claude", model.Title);
        Assert.Single(model.Rows);
        Assert.Equal(0.73, model.Rows[0].Fraction);
        Assert.Equal(Status.Warn, model.Rows[0].Tone);   // 73 % : seuils par défaut
        Assert.Equal(2, model.Actions.Count);
        Assert.Equal("Ouvrir", model.Actions[0].Label);
        Assert.Equal("refresh", model.Actions[1].SourceAction);
        Assert.Null(model.Note);
    }

    [Fact]
    public void Sans_detail_la_valeur_fait_une_ligne()
    {
        var model = CardContent.Build(View("t", new Reading(Value: 42, Unit: "°C"), "Temp"), new CellConfig { Id = "t" }, Array.Empty<CellView>());
        Assert.Single(model.Rows);
        Assert.Equal("42 °C", model.Rows[0].Text);
    }

    [Fact]
    public void Un_groupe_empile_ses_enfants()
    {
        var a = View("a", new Reading(Value: 10, Max: 100), "CPU");
        var b = View("b", new Reading(Value: 3, Unit: "Mo/s", History: new[] { (1L, 1.0), (2L, 3.0) }), "Réseau");
        var group = new CellConfig { Id = "g", Label = "Système", Children = new() { "a", "b" } };
        var model = CardContent.Build(CellViews.From(group, Reading.Empty, 0), group, new[] { a, b });
        Assert.Equal(2, model.Rows.Count);
        Assert.Equal("CPU", model.Rows[0].Label); Assert.Equal("10%", model.Rows[0].Text); Assert.Equal(0.1, model.Rows[0].Fraction);
        Assert.Equal("Réseau", model.Rows[1].Label); Assert.Equal("3 Mo/s", model.Rows[1].Text); Assert.Null(model.Rows[1].Fraction);
    }

    [Fact]
    public void Perime_ajoute_une_note_avec_l_age()
    {
        var model = CardContent.Build(View("c", new Reading(Value: 1, Max: 2), stale: true), new CellConfig { Id = "c" }, Array.Empty<CellView>());
        Assert.Contains("il y a 1 min", model.Note);
        Assert.Contains("réseau", model.Note);
    }
}
```

- [ ] **Step 2: Modèle de carte (pur)**

`src/CustomNotch.App/Notch/CardContent.cs` :
```csharp
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Notch;

public sealed record CardRow(string Label, string? Text, double? Fraction, string? Hint, Status Tone);
public sealed record CardAction(string Label, string? Icon, ActionConfig? Config, string? SourceAction);
public sealed record CardModel(string Title, string? Glyph, string? Subtitle, IReadOnlyList<CardRow> Rows, IReadOnlyList<CardAction> Actions, string? Note);

/// <summary>Ce que la carte affiche, calculé sans WPF : les lignes de détail de la source (ou la valeur seule), les
/// enfants pour un groupe, les boutons déclarés dans la config puis ceux de la source, et la note de péremption.</summary>
public static class CardContent
{
    public static CardModel Build(CellView view, CellConfig cell, IReadOnlyList<CellView> children)
    {
        var rows = new List<CardRow>();
        var actions = new List<CardAction>();
        if (view.Kind == CellKind.Group)
        {
            foreach (var child in children)
            {
                var hint = child.Reading.Detail is { Count: > 0 } d ? d[0].Hint : null;
                rows.Add(new CardRow(child.Label, child.Caption ?? child.Reading.Text, child.Fraction, hint, child.Status));
                foreach (var a in child.Reading.Actions ?? Array.Empty<ActionSpec>())
                    actions.Add(new CardAction($"{child.Label} · {a.Label}", a.Icon, null, $"{child.Id}:{a.Id}"));
            }
        }
        else if (view.Reading.Detail is { Count: > 0 } detail)
        {
            foreach (var d in detail)
                rows.Add(new CardRow(d.Label, d.Text, d.Fraction, d.Hint, d.Tone ?? (d.Fraction is { } f ? (cell.Thresholds ?? Thresholds.RingDefault).Judge(f * 100) : view.Status)));
        }
        else if (view.Caption is not null || view.Reading.Text is not null)
        {
            rows.Add(new CardRow(view.Label, view.Caption ?? view.Reading.Text, view.Fraction, null, view.Status));
        }
        foreach (var a in cell.Actions?.Card ?? new List<ActionConfig>())
            actions.Add(new CardAction(a.Label ?? a.Open ?? a.Shell ?? a.Source ?? "Action", a.Icon, a, null));
        if (view.Kind != CellKind.Group)
            foreach (var a in view.Reading.Actions ?? Array.Empty<ActionSpec>())
                actions.Add(new CardAction(a.Label, a.Icon, null, a.Id));
        var note = view.Stale ? $"Lecture {view.StaleAge} · {view.Reading.Error ?? "source injoignable"}" : null;
        return new CardModel(view.Label, view.Glyph, view.Reading.Text is { } t && view.Kind != CellKind.Value ? t : null, rows, actions, note);
    }
}
```

- [ ] **Step 3: Tests verts**

Run: `dotnet test tests/CustomNotch.App.Tests`
Expected: PASS.

- [ ] **Step 4: HoverCard**

`src/CustomNotch.App/Notch/HoverCard.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CustomNotch.App.Cells;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Notch;

/// <summary>La carte de codenotch : fond #0a0a0a, rayon 16, padding 16, largeur max 246 ; en-tête glyph + titre,
/// lignes label / indication à droite, barre 4 px, boutons. Ouverte 150 ms après l'entrée, fermée 250 ms après la
/// sortie, et gardée tant que le pointeur est dedans.</summary>
public sealed class HoverCard : Border
{
    private readonly IPillHost _host;
    private readonly PillMetrics _m;
    private readonly StackPanel _stack = new();
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string? _cellId;

    public HoverCard(IPillHost host, PillMetrics m)
    {
        _host = host;
        _m = m;
        Background = StatusPalette.Frozen(StatusPalette.CardBg);
        CornerRadius = new CornerRadius(16);
        Padding = new Thickness(16);
        MaxWidth = m.CardWidth;
        MinWidth = 160;
        Visibility = Visibility.Collapsed;
        Child = new ScrollViewer { Content = _stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 400 };
        Tail = new Path { Fill = Background, Visibility = Visibility.Collapsed };
        MouseEnter += (_, _) => CancelHide();
        MouseLeave += (_, _) => HideLater();
        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
    }

    /// <summary>La queue vers la cellule : un triangle 32 × 36, posé par la fenêtre à côté de la carte.</summary>
    public Path Tail { get; }
    public string? CellId => _cellId;
    public bool IsOpen => Visibility == Visibility.Visible;

    public void Show(CardModel model, string cellId)
    {
        _cellId = cellId;
        CancelHide();
        _stack.Children.Clear();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var (glyph, filled) = GlyphLibrary.Get(model.Glyph);
        head.Children.Add(filled ? Glyphs.Fill(glyph, 16, StatusPalette.Frozen(StatusPalette.Ink)) : Glyphs.Stroke(glyph, 16, StatusPalette.Frozen(StatusPalette.Ink), 1.6));
        head.Children.Add(Text(model.Title, 15, FontWeights.SemiBold, Colors.White, new Thickness(8, 0, 0, 0)));
        _stack.Children.Add(head);
        if (model.Subtitle is not null) _stack.Children.Add(Text(model.Subtitle, 11, FontWeights.Normal, StatusPalette.Dim, new Thickness(0, -6, 0, 10)));
        foreach (var row in model.Rows) _stack.Children.Add(Row(row));
        if (model.Actions.Count > 0)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var action in model.Actions)
            {
                var button = new Button { Content = action.Label, Style = (Style)FindResource("CardButton"), Margin = new Thickness(0, 0, 6, 6) };
                var a = action;
                button.Click += async (_, _) =>
                {
                    if (a.Config is not null) await _host.RunActionAsync(cellId, a.Config);
                    else if (a.SourceAction is { } sa)
                    {
                        var parts = sa.Split(':', 2);
                        await _host.InvokeSourceAsync(parts.Length == 2 ? parts[0] : cellId, parts[^1]);
                    }
                };
                wrap.Children.Add(button);
            }
            _stack.Children.Add(wrap);
        }
        if (model.Note is not null) _stack.Children.Add(Text(model.Note, 11, FontWeights.Normal, Color.FromRgb(0xc8, 0xc8, 0xc8), new Thickness(0, 8, 0, 0), wrap: true));
        Visibility = Visibility.Visible;
        Tail.Visibility = Visibility.Visible;
    }

    private static TextBlock Text(string text, double size, FontWeight weight, Color color, Thickness margin, bool wrap = false)
        => new() { Text = text, FontSize = size, FontWeight = weight, Foreground = StatusPalette.Frozen(color), Margin = margin, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Une ligne : libellé à gauche (gras 12), indication à droite (gris 11), barre 4 px si fraction, texte dessous.</summary>
    private static UIElement Row(CardRow row)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var top = new DockPanel();
        var hint = Text(row.Hint ?? "", 11, FontWeights.Normal, StatusPalette.Dim, new Thickness(8, 0, 0, 0));
        DockPanel.SetDock(hint, Dock.Right);
        top.Children.Add(hint);
        top.Children.Add(Text(row.Label, 12, FontWeights.SemiBold, StatusPalette.Ink, new Thickness(0)));
        panel.Children.Add(top);
        if (row.Fraction is { } f)
        {
            var track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = StatusPalette.Frozen(Color.FromRgb(0x2d, 0x2d, 0x2d)), Margin = new Thickness(0, 6, 0, 4) };
            var fill = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = StatusPalette.Brush(row.Tone), HorizontalAlignment = HorizontalAlignment.Left };
            track.SizeChanged += (_, e) => fill.Width = Math.Max(0, e.NewSize.Width * Math.Clamp(f, 0, 1));
            track.Child = fill;
            panel.Children.Add(track);
        }
        if (row.Text is not null) panel.Children.Add(Text(row.Text, 11, FontWeights.Normal, StatusPalette.Dim, new Thickness(0, row.Fraction is null ? 2 : 0, 0, 0)));
        return panel;
    }

    public void HideLater() { _hide.Stop(); _hide.Start(); }
    public void CancelHide() => _hide.Stop();

    public void Hide()
    {
        _cellId = null;
        Visibility = Visibility.Collapsed;
        Tail.Visibility = Visibility.Collapsed;
    }
}
```

- [ ] **Step 5: Brancher la carte dans PillWindow**

Dans `src/CustomNotch.App/Notch/PillWindow.cs` :
- champs : `private readonly HoverCard _card;` `private readonly DispatcherTimer _open = new() { Interval = TimeSpan.FromMilliseconds(150) };` `private Cells.CellHost? _pending;`
- constructeur, après `_canvas.Children.Add(_grip)` : `_card = new HoverCard(host, _m); _canvas.Children.Add(_card.Tail); _canvas.Children.Add(_card); _open.Tick += (_, _) => { _open.Stop(); if (_pending is { } p) OpenCard(p); };`
- `Apply` : recréer `_card` si l'échelle change n'est pas nécessaire (la carte n'est pas mise à l'échelle).
- remplacer les trois stubs :
```csharp
    private void OnCellHovered(Cells.CellHost host)
    {
        _pending = host;
        _card.CancelHide();
        if (_card.IsOpen && _card.CellId != host.Cell.Id) OpenCard(host); else _open.Start();
    }

    private void OnCellUnhovered(Cells.CellHost host)
    {
        _open.Stop();
        _pending = null;
        _card.HideLater();
    }

    private async void OnCellClicked(Cells.CellHost host)
    {
        var click = host.Cell.Actions?.Click;
        if (click is not null) await _host.RunActionAsync(host.Cell.Id, click);
        else if (host.Cell.Source == "launcher") await _host.InvokeSourceAsync(host.Cell.Id, "open");
        else OpenCard(host);
    }

    private void OpenCard(Cells.CellHost host)
    {
        if (_host.View(host.Cell.Id) is not { } view) return;
        var model = CardContent.Build(view, host.Cell, _host.Children(host.Cell));
        _card.Show(model, host.Cell.Id);
        _card.UpdateLayout();
        PlaceCard(host);
    }

    /// <summary>La carte côté libre, centrée sur la cellule (bornée à la fenêtre), la queue entre les deux.</summary>
    private void PlaceCard(Cells.CellHost host)
    {
        var cellCenter = host.TranslatePoint(new Point(host.ActualWidth / 2, _m.Ring / 2), _canvas);
        var w = _card.ActualWidth > 0 ? _card.ActualWidth : _m.CardWidth;
        var h = _card.ActualHeight > 0 ? _card.ActualHeight : 100;
        var pillLeft = Canvas.GetLeft(_shape);
        var pillTop = Canvas.GetTop(_shape);
        double x, y;
        Geometry tail;
        switch (Pill.Edge)
        {
            case "right":
                x = pillLeft - _m.CardGap - _m.Tail + 4 - w;   // la queue chevauche la carte de 4 px pour ne laisser aucun jour
                y = Math.Clamp(cellCenter.Y - h / 2, 0, Math.Max(0, Height - h));
                tail = Geometry.Parse($"M{x + w - 4},{cellCenter.Y - 18} L{pillLeft - _m.CardGap},{cellCenter.Y} L{x + w - 4},{cellCenter.Y + 18} Z");
                break;
            case "left":
                x = pillLeft + _m.Width + _m.CardGap + _m.Tail - 4;
                y = Math.Clamp(cellCenter.Y - h / 2, 0, Math.Max(0, Height - h));
                tail = Geometry.Parse($"M{x + 4},{cellCenter.Y - 18} L{pillLeft + _m.Width + _m.CardGap},{cellCenter.Y} L{x + 4},{cellCenter.Y + 18} Z");
                break;
            case "top":
                y = pillTop + _m.Width + _m.CardGap + _m.Tail - 4;
                x = Math.Clamp(cellCenter.X - w / 2, 0, Math.Max(0, Width - w));
                tail = Geometry.Parse($"M{cellCenter.X - 18},{y + 4} L{cellCenter.X},{pillTop + _m.Width + _m.CardGap} L{cellCenter.X + 18},{y + 4} Z");
                break;
            default:
                y = pillTop - _m.CardGap - _m.Tail + 4 - h;
                x = Math.Clamp(cellCenter.X - w / 2, 0, Math.Max(0, Width - w));
                tail = Geometry.Parse($"M{cellCenter.X - 18},{y + h - 4} L{cellCenter.X},{pillTop - _m.CardGap} L{cellCenter.X + 18},{y + h - 4} Z");
                break;
        }
        Canvas.SetLeft(_card, x); Canvas.SetTop(_card, y);
        _card.Tail.Data = tail;
        Canvas.SetLeft(_card.Tail, 0); Canvas.SetTop(_card.Tail, 0);
    }
```
  Les nombres dans `Geometry.Parse` doivent être formatés en culture invariante : écrire `string.Create(CultureInfo.InvariantCulture, $"…")` (ajouter `using System.Globalization;`).
- `Tick()` : si `_card.IsOpen && _card.CellId is { } id && _hosts.TryGetValue(id, out var h)` → `OpenCard(h)` (la carte suit les valeurs) — seulement si la souris n'est pas sur un bouton : tester `!_card.IsMouseOver`.
- `UpdateCell(cellId)` : si `_card.IsOpen && _card.CellId == cellId && !_card.IsMouseOver` → `OpenCard(_hosts[cellId])`.

- [ ] **Step 6: Vérification à l'écran**

Run: `dotnet run --project src/CustomNotch.App`
Expected : survoler une cellule 150 ms ouvre la carte à gauche de la pilule, queue pointée sur la cellule, avec le titre, les lignes (« Utilisée 12,3 Go » + barre, « Libre … »), les boutons (« Ouvrir » sur le disque). Passer d'une cellule à l'autre change la carte sans clignoter ; sortir la ferme après 250 ms ; entrer dans la carte la garde. Cliquer « Ouvrir » sur le disque ouvre l'explorateur. Cliquer la cellule ClickUp ouvre le navigateur. La carte d'une cellule périmée montre « Lecture il y a N min · raison ».

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(app): carte hover avec détails, barres, actions et queue"
```

---

### Task 12: Cellules-groupes, diff de config, plein écran

**Files:**
- Modify: `src/CustomNotch.Core/Model/CellView.cs` (`CellViews.FromGroup`)
- Modify: `src/CustomNotch.App/Controller.cs` (`View` pour un groupe, `FullScreenDetector`)
- Create: `src/CustomNotch.App/Notch/FullScreenDetector.cs`
- Test: `tests/CustomNotch.Core.Tests/Model/GroupViewTests.cs`

**Interfaces:**
- Consumes: `CellView`, `CellConfig`, `Status`, `PillWindow`, `Screens`.
- Produces: `CellViews.FromGroup(CellConfig group, IReadOnlyList<CellView> children, long nowMs) → CellView` (anneau de l'enfant `headline`, sinon de l'enfant au pire statut ; statut = pire des enfants ; périmé si tous les enfants le sont) ; `FullScreenDetector` (`static bool IsFullScreenOn(System.Windows.Forms.Screen screen)`), branché dans le tic du contrôleur : une pilule dont l'écran est couvert par la fenêtre au premier plan se cache.

- [ ] **Step 1: Tests du groupe**

`tests/CustomNotch.Core.Tests/Model/GroupViewTests.cs` :
```csharp
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
namespace CustomNotch.Core.Tests.Model;

public class GroupViewTests
{
    private static CellView Child(string id, Reading r, long now = 0)
        => CellViews.From(new CellConfig { Id = id, Label = id.ToUpperInvariant(), Glyph = id }, r, now);

    private static readonly CellConfig Group = new() { Id = "sys", Label = "Système", Glyph = "cpu", Children = new() { "cpu", "mem" } };

    [Fact]
    public void Sans_headline_le_pire_enfant_donne_l_anneau_et_le_statut()
    {
        var cpu = Child("cpu", new Reading(Value: 10, Max: 100));
        var mem = Child("mem", new Reading(Value: 90, Max: 100));
        var view = CellViews.FromGroup(Group, new[] { cpu, mem }, 0);
        Assert.Equal(CellKind.Group, view.Kind);
        Assert.Equal(Status.Crit, view.Status);
        Assert.Equal(0.9, view.Fraction);
        Assert.Equal("90%", view.Caption);
        Assert.Equal("cpu", view.Glyph);   // le glyph du groupe, pas celui de l'enfant
        Assert.Equal("Système", view.Label);
    }

    [Fact]
    public void Headline_choisit_l_enfant_de_l_anneau_mais_le_statut_reste_le_pire()
    {
        var cpu = Child("cpu", new Reading(Value: 10, Max: 100));
        var mem = Child("mem", new Reading(Value: 90, Max: 100));
        var g = new CellConfig { Id = "sys", Children = new() { "cpu", "mem" }, Headline = "cpu" };
        var view = CellViews.FromGroup(g, new[] { cpu, mem }, 0);
        Assert.Equal(0.1, view.Fraction);
        Assert.Equal(Status.Crit, view.Status);
    }

    [Fact]
    public void Busy_et_Attention_passent_devant_les_seuils()
    {
        var a = Child("a", new Reading(Value: 99, Max: 100));
        var b = Child("b", new Reading(Status: Status.Attention));
        Assert.Equal(Status.Attention, CellViews.FromGroup(Group, new[] { a, b }, 0).Status);
    }

    [Fact]
    public void Perime_seulement_si_tous_les_enfants_le_sont()
    {
        var fresh = Child("a", new Reading(Value: 1, Max: 2));
        var stale = Child("b", new Reading(Value: 1, Max: 2).AsStale(0, "x"), now: 120_000);
        Assert.False(CellViews.FromGroup(Group, new[] { fresh, stale }, 120_000).Stale);
        Assert.True(CellViews.FromGroup(Group, new[] { stale }, 120_000).Stale);
    }

    [Fact]
    public void Sans_enfant_le_groupe_est_off()
    {
        var view = CellViews.FromGroup(Group, Array.Empty<CellView>(), 0);
        Assert.Equal(Status.Off, view.Status);
        Assert.Null(view.Fraction);
    }
}
```

- [ ] **Step 2: FromGroup**

Ajouter dans `src/CustomNotch.Core/Model/CellView.cs`, classe `CellViews` :
```csharp
    /// <summary>L'ordre d'alarme : ce qui réclame l'utilisateur d'abord, puis ce qui est critique, puis le travail en cours.</summary>
    private static int Rank(Status s) => s switch
    {
        Status.Attention => 0, Status.Crit => 1, Status.Busy => 2, Status.Warn => 3, Status.Ok => 4, _ => 5,
    };

    public static CellView FromGroup(CellConfig group, IReadOnlyList<CellView> children, long nowMs)
    {
        if (children.Count == 0)
            return new CellView(group.Id, CellKind.Group, Status.Off, group.Label ?? group.Id, group.Glyph, null, null, false, null, Reading.Empty);
        var worst = children.MinBy(c => Rank(c.Status))!;
        var headline = (group.Headline is { } h ? children.FirstOrDefault(c => c.Id == h) : null) ?? children.FirstOrDefault(c => c.Fraction is not null) ?? worst;
        var stale = children.All(c => c.Stale);
        var oldest = stale ? children.Min(c => c.Reading.StaleSinceMs ?? nowMs) : (long?)null;
        return new CellView(group.Id, CellKind.Group, worst.Status, group.Label ?? group.Id, group.Glyph, headline.Caption, headline.Fraction, stale,
            stale ? Age(nowMs - oldest!.Value) : null, headline.Reading);
    }
```

Dans `Controller.View` : si `cell.IsGroup` → `return CellViews.FromGroup(cell, Children(cell), now);` (avant le calcul standard).

- [ ] **Step 3: Tests verts et essai**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: PASS.

Ajouter dans `%APPDATA%\customNotch\cells.json` une cellule `{ "id": "sys", "label": "Système", "glyph": "cpu", "children": ["cpu", "mem", "disk", "net"] }` et passer `"visible": false` sur `cpu`, `mem`, `disk`, `net`. Expected : une seule cellule « Système » dont l'anneau montre le pire des quatre, et dont la carte liste CPU / Mémoire / Disque / Réseau avec leurs barres et le bouton « Disque · Ouvrir ». Un enfant masqué (`visible: false`) reste lu par l'ordonnanceur et apparaît dans la carte.

- [ ] **Step 4: Plein écran**

`src/CustomNotch.App/Notch/FullScreenDetector.cs` :
```csharp
using System.Runtime.InteropServices;

namespace CustomNotch.App.Notch;

/// <summary>La fenêtre au premier plan couvre-t-elle l'écran entier ? Un jeu ou une vidéo en plein écran ne veut pas
/// d'une pilule par-dessus. Le bureau et le shell (Progman, WorkerW) ne comptent pas : ils couvrent toujours tout.</summary>
public static partial class FullScreenDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out NativeRect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, System.Text.StringBuilder name, int max);

    public static bool IsFullScreenOn(System.Windows.Forms.Screen screen)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0 || !GetWindowRect(hwnd, out var r)) return false;
        var name = new System.Text.StringBuilder(64);
        GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        var b = screen.Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }
}
```

Dans `PillWindow` : propriété `public System.Windows.Forms.Screen CurrentScreen => Screen();` (rendre `Screen()` accessible) et une méthode :
```csharp
    private bool _fullScreenHidden;
    public void SetFullScreen(bool covered)
    {
        if (covered == _fullScreenHidden) return;
        _fullScreenHidden = covered;
        if (covered) Hide(); else if (Pill.Visible) Show();
    }
```
Dans `Controller`, le tic d'une seconde devient :
```csharp
        _tick.Tick += (_, _) =>
        {
            foreach (var (id, p) in _pills)
            {
                var visible = _config.Current.Pills.FirstOrDefault(x => x.Id == id)?.Visible ?? false;
                if (visible && !_allHidden) p.SetFullScreen(FullScreenDetector.IsFullScreenOn(p.CurrentScreen));
                p.Tick();
            }
        };
```
et `ApplyConfig` / `ToggleAll` continuent d'appeler `Show()`/`Hide()` selon `Visible` et `_allHidden` — `SetFullScreen` ne fait que passer par-dessus tant qu'une fenêtre couvre l'écran.

- [ ] **Step 5: Vérification à l'écran**

Run: `dotnet run --project src/CustomNotch.App`
Expected : ouvrir une vidéo YouTube en plein écran (F) sur l'écran de la pilule : elle disparaît en moins d'une seconde ; quitter le plein écran : elle revient. Un jeu fenêtré maximisé (barre de titre) ne la cache pas.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat: cellules-groupes et masquage en plein écran"
```

---

### Task 13: Documentation et lisibilité du dépôt

**Files:**
- Create: `docs/ARCHITECTURE.md`, `README.md`, `CHANGELOG.md`, `docs/cells.example.json`

- [ ] **Step 1: docs/ARCHITECTURE.md**

Même patron que `ClickUp-Extended/docs/ARCHITECTURE.md` : sections **1. Stack** (tableau besoin / choix / pourquoi, repris de la spec §2), **2. Arborescence** (l'arbre réel du dépôt à la fin de la tâche 12, avec une ligne de commentaire par fichier), **3. Flux de données** (schéma texte : cells.json + cells.<machine>.json + secrets.json → ConfigStore → Scheduler → ReadingStore → Controller (Dispatcher) → PillWindow → CellHost / HoverCard ; actions dans l'autre sens), **4. Le contrat Reading / CellView** (les règles de déduction de `CellViews`, les seuils, les groupes), **5. Sources** (le tableau de la spec §4.3 limité à celles livrées ici, avec leurs params), **6. Fenêtre et rendu** (les valeurs de `PillMetrics`, la construction de `PillShape`, le placement, le drag, la carte, le plein écran), **7. Stratégie de gestion des erreurs** (spec §9), **8. Ce qui vient ensuite** (Claude Code, média, ClickUp, Settings, updater, installateur : plan 2). Écrire en français, phrases courtes, chaque choix avec son pourquoi.

- [ ] **Step 2: README.md, CHANGELOG.md, exemple**

`README.md` : une phrase de présentation, une capture (à faire à la main : `docs/apercu-pilule.png`, la pilule + carte ouverte), **Installation** (« en développement : `dotnet run --project src/CustomNotch.App` ; l'installateur arrive avec le plan 2 »), **Configuration** (où est `cells.json`, les trois fichiers, les placeholders, un exemple de cellule de chaque source, le lien vers `docs/cells.example.json`), **Sources disponibles** (tableau), **Développement** (`dotnet build`, `dotnet test`), lien vers `docs/ARCHITECTURE.md` et `NOTICE.md`.

`docs/cells.example.json` : deux pilules (droite : groupe Système + lanceur ClickUp + cellule `http` d'exemple commentée ; bas : `shell` d'exemple `echo 42`, batterie), chaque champ commenté.

`CHANGELOG.md` :
```markdown
# Changelog

## 0.1.0 - socle
- Pilules multiples ancrées à un bord, drag, multi-écrans, masquage en plein écran.
- Cellules anneau / valeur / statut / sparkline / groupe ; carte hover avec détails, barres et actions.
- Configuration cells.json portable + surcharge locale + secrets DPAPI, rechargée à chaud.
- Sources : system.cpu, system.memory, system.disk, system.network, system.battery, launcher, http, shell.
```

- [ ] **Step 3: Commit et push**

```powershell
git add -A
git commit -m "docs: architecture, README, changelog et exemple de configuration"
git push
```

---

## Auto-revue du plan (faite à l'écriture)

- **Couverture de la spec** — §4.1 modèle : T2, T12 ; §4.2 fichiers : T3, T4 ; §4.3 sources livrées ici : T6, T7 (claude, media, clickup → plan 2) ; §4.4 ordonnanceur : T5 ; §5 fenêtre et rendu : T9, T10, T11, T12 ; §8 tray : T8 (Settings, identité complète, `--report` → plan 2) ; §9 erreurs : T4, T5, T7 ; §10 tests : chaque tâche ; §11 installation → plan 2.
- **Types** — `Reading` (T2) est consommé tel quel par T5–T12 ; `CellView.From` (T2) et `FromGroup` (T12) partagent la signature `(…, long nowMs)` ; `IPillHost` (T8) est ce que `PillWindow` (T9), `HoverCard` (T11) et `Controller` (T8/T12) utilisent ; `ConfigStore.SetPillLocal` (T4) est appelé par `Controller.SavePosition/HidePill/TogglePill` (T8) ; `ActionRunner.Open` existe dès T6 pour `DiskSource`, complété en T7.
- **Points d'attention à l'exécution** — les sens d'arc de `PillShape` (T9) et les couleurs `StatusPalette` (T10) se valident à l'œil : ne pas « corriger » un test de géométrie sans avoir regardé la pilule. Les versions de paquets NuGet (T1, T9) sont à confirmer avec `dotnet package search`.

## Plan 2 (à écrire après ce plan)

Claude Code (identifiants, endpoint usage, renouvellement `claude -p`, hooks, serveur d'événements, `ActivityStore`, exe hook, connexion), source média (WinRT), source ClickUp (endpoint local de ClickUp-Extended + repli lanceur), fenêtre Settings (sidebar, formulaires générés par `SourceSchema`), updater, diagnostic `--report`, autostart, installateur Inno Setup, icône `.ico`.
