# customNotch — Plan d'implémentation 2 : réglages par l'UI, média, groupe Système

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Une fenêtre Réglages qui règle tout (pilules, cellules, sources, secrets, général) sans ouvrir `cells.json`, une source média (lecture en cours, play/pause au clic), et un groupe « Système » par défaut.

**Architecture:** Core gagne `ConfigEditor` (seule porte d'écriture de l'UI : lit, mute, écrit atomiquement, recharge, annule si refusé), un schéma de source enrichi (`Group`, `Description`, `DefaultAction`) avec validation typée des params, `CellClickResolver` (pur), et `IMediaSession`/`MediaSource`. App gagne `WindowsMediaSession` (WinRT), la fenêtre `Settings/` (sidebar + trois pages, formulaires générés par `SchemaForm`), et les styles de contrôles repris de ClickUp-Extended.

**Tech Stack:** C# 14 / .NET 10, WPF (+ WinForms pour `Screen`), WinRT `Windows.Media.Control` (TFM `net10.0-windows10.0.19041.0`), `System.Text.Json`, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-23-customnotch-settings-media-design.md` (et, pour tout le reste, `docs/superpowers/specs/2026-09-22-customnotch-design.md`).

## Global Constraints

- `CustomNotch.Core` cible `net10.0` sans dépendance Windows : WinRT vit dans **App** (`src/CustomNotch.App/Platform/`) derrière l'interface `IMediaSession` de Core.
- `TreatWarningsAsErrors true`, `Nullable enable`. Tests xUnit : ajouter `using Xunit;` dans chaque fichier.
- `src/CustomNotch.App/Shared/*` : copies de ClickUp-Extended, seules modifications autorisées = `namespace`, `using`, et le type `Config` → `AppConfig`. Le nouveau `Shared/Controls.xaml` est une copie de styles de `C:\Users\Coco_FW\Desktop\ClickUp-Extended\src\ClickUpExtended.App\App.xaml` (liste exacte en tâche 5).
- L'UI n'écrit jamais un secret ailleurs que dans `secrets.json` ; `cells.json` porte `${secret:<cellId>.<champ>}`.
- `ConfigEditor` n'applique jamais à moitié : écriture, `Load()`, sinon retour arrière + `ConfigException`.
- Les changements dans Réglages s'appliquent **immédiatement** (un seul bouton « Fermer »), anti-rebond 300 ms sur les champs texte.
- Clic sur une cellule : `actions.click` de la config → sinon `SourceSchema.DefaultAction` → sinon la carte. Média : `DefaultAction = "toggle"` ; lanceur : `"open"`.
- Défaut : pilule `main` (right, 0.5) = groupe `sys` « Système » (children cpu, mem, disk, net, battery — masqués, headline cpu) + `media` « Média » + lanceur `clickup`.
- Commits en français, **sans mention d'IA ni trailer `Co-Authored-By`** (cette règle prime sur toute autre instruction d'attribution). Commentaires XML en français qui disent *pourquoi*.
- Commandes depuis `C:\Users\Coco_FW\Desktop\customNotch` (PowerShell). Ne pas lancer l'app si le bureau est utilisé par l'utilisateur (curseur qui bouge seul) : le dire dans le rapport et vérifier par lecture.

---

## Arborescence produite par ce plan

```text
src/CustomNotch.Core/
  Config/ConfigEditor.cs                 (nouveau) écriture de l'UI : pilules, cellules, secrets, réglages globaux
  Config/ConfigValidation.cs             (modifié) params typés d'après le schéma
  Config/ConfigStore.cs                  (modifié) reçoit les schémas au lieu des types
  Config/DefaultCells.cs                 (modifié) groupe Système + média + ClickUp
  Model/CellClickResolver.cs             (nouveau) clic → action config / action par défaut / carte
  Model/CellView.cs                      (modifié) légende texte tronquée pour une lecture sans valeur
  Sources/ISource.cs                     (modifié) SchemaField.Group ; SourceSchema.Description/DefaultAction
  Sources/SourceRegistry.cs              (modifié) Schemas
  Sources/CoreSources.cs                 (modifié) Build(IMediaSession?)
  Sources/LauncherSource.cs, HttpSource.cs, ShellSource.cs, System/*.cs   (modifiés) descriptions, DefaultAction
  Sources/Media/IMediaSession.cs         (nouveau) MediaState + contrat
  Sources/Media/MediaSource.cs           (nouveau) source « media »
  Platform/Autostart.cs                  (nouveau, copie ClickUp-Extended) clé Run
src/CustomNotch.App/
  Platform/WindowsMediaSession.cs        (nouveau) WinRT GlobalSystemMediaTransportControls
  Shared/Controls.xaml                   (nouveau, copie) styles TextBox, PasswordBox, ComboBox, CheckBox, ScrollBar, ToolTip, ListBox, Button, Card, Section, Hint, Field, ProgressBar
  Styles.xaml                            (modifié) NavList, TreeView
  App.xaml                               (modifié) fusion de Controls.xaml
  IPillHost.cs, Controller.cs            (modifiés) Schema(), ShowSettings → fenêtre, média injecté
  Notch/PillWindow.cs                    (modifié) clic via CellClickResolver
  Cells/CellHost.cs                      (modifié) légende 11 px pour un statut à texte
  Settings/SettingsContext.cs            (nouveau) Store, Editor, Registry
  Settings/SettingsWindow.cs             (nouveau) sidebar + pages + Fermer
  Settings/PageBase.cs                   (nouveau, adapté d'AutoSort)
  Settings/GeneralPage.cs                (nouveau) cells_path, démarrage auto, thème, journal, version
  Settings/SourcesPage.cs                (nouveau) réglages globaux (vide) + secrets
  Settings/SchemaForm.cs                 (nouveau) formulaire généré depuis SourceSchema
  Settings/SourceCatalogDialog.cs        (nouveau) choix d'une source
  Settings/GlyphGallery.cs               (nouveau) grille de glyphes + tracé SVG
  Settings/PillsPage.cs                  (nouveau) arbre + barre d'outils + éditeur de pilule
  Settings/CellEditor.cs                 (nouveau) sections Source / Affichage / Actions / Groupe
tests/CustomNotch.Core.Tests/Config/ConfigEditorTests.cs, Model/CellClickResolverTests.cs, Sources/MediaSourceTests.cs (nouveaux) ; ConfigValidationTests.cs, ConfigStoreTests.cs, CellViewTests.cs, DocsExampleTests.cs (modifiés)
tests/CustomNotch.App.Tests/SchemaFormTests.cs (nouveau)
docs/ARCHITECTURE.md, README.md, CHANGELOG.md, docs/cells.example.json (modifiés)
```

---

### Task 1: Schéma enrichi, validation typée, résolution du clic, légende texte

**Files:**
- Modify: `src/CustomNotch.Core/Sources/ISource.cs`, `Sources/SourceRegistry.cs`, `Sources/LauncherSource.cs`, `Sources/HttpSource.cs`, `Sources/ShellSource.cs`, `Sources/System/{Cpu,Memory,Disk,Network,Battery}Source.cs`
- Modify: `src/CustomNotch.Core/Config/ConfigValidation.cs`, `Config/ConfigStore.cs`, `Model/CellView.cs`
- Create: `src/CustomNotch.Core/Model/CellClickResolver.cs`
- Modify: `src/CustomNotch.App/IPillHost.cs`, `Controller.cs`, `Notch/PillWindow.cs`, `Cells/CellHost.cs`
- Test: `tests/CustomNotch.Core.Tests/Model/CellClickResolverTests.cs` (nouveau), `Config/ConfigValidationTests.cs`, `Config/ConfigStoreTests.cs`, `Model/CellViewTests.cs`, `DocsExampleTests.cs`

**Interfaces:**
- Produces: `SchemaField(Name, Type, Label, Required, Help, Default, Choices, Group)`, `SourceSchema(Type, Title, Fields, DefaultGlyph, Description, DefaultAction)`, `SourceRegistry.Schemas : IReadOnlyDictionary<string, SourceSchema>`, `ConfigValidation.Validate(CellsFile, IReadOnlyDictionary<string, SourceSchema>)` (l'ancienne surcharge par ensemble de types reste), `ConfigStore(string home, IReadOnlyDictionary<string, SourceSchema> schemas)`, `record ClickPlan(ActionConfig? Config, string? SourceAction, bool OpenCard)`, `CellClickResolver.Resolve(CellConfig, SourceSchema?) → ClickPlan`, `IPillHost.Schema(string sourceType) → SourceSchema?`, `CellViews.Caption` rend le texte tronqué (10 caractères + « … ») pour une lecture sans valeur.

- [ ] **Step 1: Tests — résolution du clic, légende texte, params typés**

`tests/CustomNotch.Core.Tests/Model/CellClickResolverTests.cs` :
```csharp
using Xunit;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Model;

public class CellClickResolverTests
{
    private static readonly SourceSchema Launcher = new("launcher", "Lanceur", Array.Empty<SchemaField>(), "link", "ouvre", "open");
    private static readonly SourceSchema Cpu = new("system.cpu", "Processeur", Array.Empty<SchemaField>(), "cpu", "cpu", null);

    [Fact]
    public void L_action_de_la_config_l_emporte()
    {
        var cell = new CellConfig { Id = "c", Source = "launcher", Actions = new CellActions { Click = new ActionConfig { Open = "https://x" } } };
        var plan = CellClickResolver.Resolve(cell, Launcher);
        Assert.Equal("https://x", plan.Config!.Open);
        Assert.Null(plan.SourceAction);
        Assert.False(plan.OpenCard);
    }

    [Fact]
    public void Sinon_l_action_par_defaut_de_la_source()
    {
        var plan = CellClickResolver.Resolve(new CellConfig { Id = "c", Source = "launcher" }, Launcher);
        Assert.Equal("open", plan.SourceAction);
        Assert.False(plan.OpenCard);
    }

    [Fact]
    public void Sinon_la_carte()
    {
        Assert.True(CellClickResolver.Resolve(new CellConfig { Id = "c", Source = "system.cpu" }, Cpu).OpenCard);
        Assert.True(CellClickResolver.Resolve(new CellConfig { Id = "c", Source = "zz" }, null).OpenCard);
    }

    [Fact]
    public void Un_groupe_ouvre_toujours_la_carte()
    {
        var group = new CellConfig { Id = "g", Children = new() { "a" } };
        Assert.True(CellClickResolver.Resolve(group, Launcher).OpenCard);
    }

    [Fact]
    public void Un_click_vide_dans_la_config_ne_compte_pas()
    {
        var cell = new CellConfig { Id = "c", Source = "launcher", Actions = new CellActions { Click = new ActionConfig() } };
        Assert.Equal("open", CellClickResolver.Resolve(cell, Launcher).SourceAction);
    }
}
```

Ajouter à `tests/CustomNotch.Core.Tests/Model/CellViewTests.cs` :
```csharp
    [Fact]
    public void Un_statut_avec_texte_a_une_legende_tronquee()
    {
        Assert.Equal("Bohemian R…", CellViews.Caption(CellKind.Status, new Reading(Text: "Bohemian Rhapsody")));
        Assert.Equal("Court", CellViews.Caption(CellKind.Status, new Reading(Text: "Court")));
        Assert.Null(CellViews.Caption(CellKind.Status, Reading.Empty));
        Assert.Null(CellViews.Caption(CellKind.Status, new Reading(Value: 3, Text: "x")));
    }
```

Ajouter à `tests/CustomNotch.Core.Tests/Config/ConfigValidationTests.cs` :
```csharp
    private static readonly Dictionary<string, SourceSchema> Schemas = new()
    {
        ["http"] = new("http", "HTTP", new SchemaField[]
        {
            new("url", "url", "URL", Required: true), new("max", "number", "Max"), new("method", "choice", "Méthode", Choices: new[] { "GET", "POST" }), new("flag", "bool", "Drapeau"),
        }),
        ["launcher"] = new("launcher", "Lanceur", new[] { new SchemaField("open", "string", "Ouvrir", Required: true) }),
    };

    [Fact]
    public void Les_params_sont_verifies_d_apres_le_schema()
    {
        var f = File("""{"pills":[{"id":"p","cells":[
            {"id":"a","source":"http","params":{"url":"pas une url","max":"beaucoup","method":"PUT","flag":"oui"}},
            {"id":"b","source":"launcher"},
            {"id":"c","source":"http","params":{"url":"https://x","max":3,"method":"GET","flag":true}}]}]}""");
        var errors = ConfigValidation.Validate(f, Schemas);
        Assert.Contains(errors, e => e.Contains("cells[0].params.url"));
        Assert.Contains(errors, e => e.Contains("cells[0].params.max") && e.Contains("nombre"));
        Assert.Contains(errors, e => e.Contains("cells[0].params.method") && e.Contains("GET, POST"));
        Assert.Contains(errors, e => e.Contains("cells[0].params.flag"));
        Assert.Contains(errors, e => e.Contains("cells[1].params.open") && e.Contains("requis"));
        Assert.DoesNotContain(errors, e => e.Contains("cells[2]"));
    }

    [Fact]
    public void Une_source_inconnue_reste_signalee_avec_les_schemas()
    {
        var f = File("""{"pills":[{"id":"p","cells":[{"id":"a","source":"nope"}]}]}""");
        Assert.Contains(ConfigValidation.Validate(f, Schemas), e => e.Contains("source « nope » inconnue"));
    }
```
(ajouter `using CustomNotch.Core.Sources;` en tête du fichier.)

Dans `ConfigStoreTests.cs` et `DocsExampleTests.cs`, remplacer l'ensemble `Known`/`Types` par les schémas : `private static readonly IReadOnlyDictionary<string, SourceSchema> Known = CoreSources.Build().Schemas;` (et `using CustomNotch.Core.Sources;`). Le test `La_surcharge_locale_et_les_secrets_sont_appliques` reste valide : `url` y vaut `https://x`, une URI absolue.

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: erreurs de compilation (`CellClickResolver`, `Schemas`, `Group`… introuvables).

- [ ] **Step 3: Schéma et registre**

`src/CustomNotch.Core/Sources/ISource.cs` — remplacer les deux records :
```csharp
/// <summary>Un champ de paramètre : la fenêtre de réglages en fait un formulaire, la validation le vérifie.
/// Type : string | number | bool | secret | path | url | choice. Group : la section du formulaire (null = par défaut).</summary>
public sealed record SchemaField(string Name, string Type, string Label, bool Required = false, string? Help = null, string? Default = null, IReadOnlyList<string>? Choices = null, string? Group = null);

/// <summary>Ce qu'une source dit d'elle-même : son catalogue (Title, Description), ses champs, son glyph par défaut, et
/// l'action qu'un clic sur la cellule déclenche quand la config n'en fixe pas (« open », « toggle »… ; null = la carte).</summary>
public sealed record SourceSchema(string Type, string Title, IReadOnlyList<SchemaField> Fields, string? DefaultGlyph = null, string? Description = null, string? DefaultAction = null);
```

`src/CustomNotch.Core/Sources/SourceRegistry.cs` — ajouter :
```csharp
    /// <summary>Les schémas par type : ce que la validation et la fenêtre de réglages consomment.</summary>
    public IReadOnlyDictionary<string, SourceSchema> Schemas => _sources.ToDictionary(kv => kv.Key, kv => kv.Value.Schema);
```

Sources — compléter les schémas (positionnels : `DefaultGlyph`, puis `Description`, puis `DefaultAction`) :
- `LauncherSource.Schema` → `new(Type, "Lanceur", new[] { new SchemaField("open", "string", "Ouvrir", Required: true, Help: "URL, chemin, ou application") }, "link", "Ouvre une URL, un fichier ou une application d'un clic", "open")`. Et dans `ReadAsync`, retirer `Text: ctx.Str("open")` (le texte deviendrait une légende sous la cellule) : `new Reading(Status: Status.Off, Detail: new[] { new DetailRow("Ouvre", ctx.Str("open") ?? "—") }, Actions: new[] { new ActionSpec("open", "Ouvrir", "open") })`.
- `HttpSource.Schema` : `Description: "Un chiffre ou un texte lu dans une réponse JSON"` ; les champs `url`, `method`, `body` avec `Group: "Requête"`, les autres `Group: "Lecture"`.
- `ShellSource.Schema` : `Description: "La sortie d'une commande, lue comme nombre, JSON ou texte"`.
- `CpuSource` : `Description: "Occupation du processeur"` ; `MemorySource` : `"Mémoire utilisée sur le total"` ; `DiskSource` : `"Espace utilisé d'un lecteur"` ; `NetworkSource` : `"Débit réseau, avec historique"` ; `BatterySource` : `"Charge et alimentation"`.

- [ ] **Step 4: Validation typée et ConfigStore**

`src/CustomNotch.Core/Config/ConfigValidation.cs` — ajouter en tête `using System.Globalization; using System.Text.Json.Nodes; using CustomNotch.Core.Sources;` et la surcharge :
```csharp
    /// <summary>Avec les schémas : en plus des types de source, les params de chaque cellule sont vérifiés champ par champ
    /// (requis, nombre, booléen, URL absolue, choix). Un placeholder non résolu vaut chaîne vide : un champ requis vide
    /// est signalé comme tel — c'est aussi le symptôme d'un secret manquant.</summary>
    public static List<string> Validate(CellsFile file, IReadOnlyDictionary<string, SourceSchema> schemas)
    {
        var errors = Validate(file, schemas.Keys.ToHashSet());
        for (var i = 0; i < file.Pills.Count; i++)
            for (var j = 0; j < file.Pills[i].Cells.Count; j++)
            {
                var cell = file.Pills[i].Cells[j];
                if (cell.IsGroup || !schemas.TryGetValue(cell.Source, out var schema)) continue;
                ValidateParams(cell, $"pills[{i}].cells[{j}].params", schema, errors);
            }
        return errors;
    }

    private static void ValidateParams(CellConfig cell, string at, SourceSchema schema, List<string> errors)
    {
        foreach (var field in schema.Fields)
        {
            var node = cell.Params?[field.Name];
            var text = node switch { JsonValue v when v.TryGetValue<string>(out var s) => s, JsonValue v => v.ToJsonString(), _ => null };
            if (node is null || text is { Length: 0 })
            {
                if (field.Required) errors.Add($"{at}.{field.Name} : requis (vide, placeholder ou secret manquant)");
                continue;
            }
            switch (field.Type)
            {
                case "number":
                    if (!(node is JsonValue nv && nv.TryGetValue<double>(out _)) && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        errors.Add($"{at}.{field.Name} « {text} » : nombre attendu");
                    break;
                case "bool":
                    if (!(node is JsonValue bv && bv.TryGetValue<bool>(out _))) errors.Add($"{at}.{field.Name} « {text} » : true ou false attendu");
                    break;
                case "url":
                    if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                        errors.Add($"{at}.{field.Name} « {text} » : URL http(s) absolue attendue");
                    break;
                case "choice":
                    if (field.Choices is { Count: > 0 } && !field.Choices.Contains(text!))
                        errors.Add($"{at}.{field.Name} « {text} » : attendu {string.Join(", ", field.Choices)}");
                    break;
            }
        }
    }
```

`src/CustomNotch.Core/Config/ConfigStore.cs` : le constructeur devient `public ConfigStore(string home, IReadOnlyDictionary<string, SourceSchema> schemas)` (champ `_schemas`, `using CustomNotch.Core.Sources;`), et `Load()` appelle `ConfigValidation.Validate(file, _schemas)`. Dans `Controller.cs` : `new ConfigStore(home, _registry.Schemas)`.

- [ ] **Step 5: CellClickResolver, IPillHost.Schema, PillWindow, légende**

`src/CustomNotch.Core/Model/CellClickResolver.cs` :
```csharp
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.Core.Model;

/// <summary>Ce que fait un clic : l'action de la config, sinon celle que la source déclare par défaut, sinon la carte.</summary>
public sealed record ClickPlan(ActionConfig? Config, string? SourceAction, bool OpenCard);

public static class CellClickResolver
{
    public static ClickPlan Resolve(CellConfig cell, SourceSchema? schema)
    {
        if (cell.Actions?.Click is { } click && (click.Open ?? click.Shell ?? click.Source) is { Length: > 0 })
            return new ClickPlan(click, null, false);
        if (!cell.IsGroup && schema?.DefaultAction is { Length: > 0 } action)
            return new ClickPlan(null, action, false);
        return new ClickPlan(null, null, true);
    }
}
```

`src/CustomNotch.App/IPillHost.cs` — ajouter `SourceSchema? Schema(string sourceType);` (avec `using CustomNotch.Core.Sources;`). `Controller.cs` : `public SourceSchema? Schema(string sourceType) => _registry.Get(sourceType)?.Schema;`.

`src/CustomNotch.App/Notch/PillWindow.cs`, `OnCellClicked` — remplacer le corps du `try` :
```csharp
            var plan = Core.Model.CellClickResolver.Resolve(host.Cell, _host.Schema(host.Cell.Source));
            if (plan.Config is not null) await _host.RunActionAsync(host.Cell.Id, plan.Config);
            else if (plan.SourceAction is not null) await _host.InvokeSourceAsync(host.Cell.Id, plan.SourceAction);
            else OpenCard(host);
```

`src/CustomNotch.Core/Model/CellView.cs`, `Caption` — remplacer le `default:` :
```csharp
            default:
                // Une lecture sans valeur mais avec un texte (média : le titre) : dix caractères sous la cellule.
                return r.Value is null && r.Text is { Length: > 0 } t ? (t.Length <= 10 ? t : t[..10].TrimEnd() + "…") : null;
```

`src/CustomNotch.App/Cells/CellHost.cs`, dans `Render` après `_caption.Text = …` : `_caption.FontSize = (view.Kind == CellKind.Status ? 11 : 15) * _m.Scale;` (un titre en 15 px déborderait de la pilule).

- [ ] **Step 6: Tests verts, build**

Run: `dotnet build CustomNotch.sln` puis `dotnet test CustomNotch.sln`
Expected: 0 avertissement ; tous les tests PASS (les 134 existants + les nouveaux).

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(core): schéma de source enrichi, params validés, clic par défaut de la source, légende texte"
```

---


### Task 2: Source média (Core) et configuration par défaut

**Files:**
- Create: `src/CustomNotch.Core/Sources/Media/IMediaSession.cs`, `Sources/Media/MediaSource.cs`
- Modify: `src/CustomNotch.Core/Sources/CoreSources.cs`, `Config/DefaultCells.cs`
- Test: `tests/CustomNotch.Core.Tests/Sources/MediaSourceTests.cs` (nouveau), `Config/ConfigStoreTests.cs` (le test du premier lancement)

**Interfaces:**
- Consumes: `SourceBase`, `Reading`, `DetailRow`, `ActionSpec`, `Status`, `SourceSchema`.
- Produces: `record MediaState(string Title, string Artist, string App, bool Playing)`, `interface IMediaSession { MediaState? Current(); Task ToggleAsync(); Task NextAsync(); Task PreviousAsync(); event Action? Changed; }`, `MediaSource(IMediaSession? session)` (type `media`, `DefaultAction "toggle"`, glyph `music`, refresh 5 s, `Pushed` sur `Changed`), `CoreSources.Build(IMediaSession? media = null)`, nouveau `DefaultCells.Json()`.

- [ ] **Step 1: Tests**

`tests/CustomNotch.Core.Tests/Sources/MediaSourceTests.cs` :
```csharp
using System.Text.Json.Nodes;
using Xunit;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
using CustomNotch.Core.Sources.Media;
namespace CustomNotch.Core.Tests.Sources;

public class MediaSourceTests
{
    private sealed class FakeSession : IMediaSession
    {
        public MediaState? State;
        public List<string> Calls = new();
        public event Action? Changed;
        public MediaState? Current() => State;
        public Task ToggleAsync() { Calls.Add("toggle"); return Task.CompletedTask; }
        public Task NextAsync() { Calls.Add("next"); return Task.CompletedTask; }
        public Task PreviousAsync() { Calls.Add("prev"); return Task.CompletedTask; }
        public void Fire() => Changed?.Invoke();
    }

    private static CellContext Ctx(string id = "m") => new(id, new JsonObject(), null);

    [Fact]
    public async Task En_lecture_la_cellule_est_occupee_avec_titre_et_actions()
    {
        var session = new FakeSession { State = new MediaState("Bohemian Rhapsody", "Queen", "Spotify", true) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal("Bohemian Rhapsody — Queen", r.Text);
        Assert.Equal(Status.Busy, r.Status);
        Assert.Contains(r.Detail!, d => d.Label == "Application" && d.Text == "Spotify");
        Assert.Equal(new[] { "prev", "toggle", "next" }, r.Actions!.Select(a => a.Id));
        Assert.Equal("Pause", r.Actions![1].Label);
    }

    [Fact]
    public async Task En_pause_le_statut_est_ok_et_le_bouton_dit_lecture()
    {
        var session = new FakeSession { State = new MediaState("Titre", "", "chrome", false) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal("Titre", r.Text);
        Assert.Equal(Status.Ok, r.Status);
        Assert.Equal("Lecture", r.Actions![1].Label);
    }

    [Fact]
    public async Task Sans_session_ni_implementation_la_cellule_est_off()
    {
        var r1 = await new MediaSource(new FakeSession()).ReadAsync(Ctx(), CancellationToken.None);
        var r2 = await new MediaSource(null).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal(Status.Off, r1.Status);
        Assert.Equal("Aucune lecture", r1.Text);
        Assert.Equal(Status.Off, r2.Status);
    }

    [Fact]
    public async Task Les_actions_atteignent_la_session()
    {
        var session = new FakeSession { State = new MediaState("t", "a", "app", true) };
        var src = new MediaSource(session);
        await src.InvokeAsync("toggle", Ctx(), CancellationToken.None);
        await src.InvokeAsync("next", Ctx(), CancellationToken.None);
        await src.InvokeAsync("prev", Ctx(), CancellationToken.None);
        Assert.Equal(new[] { "toggle", "next", "prev" }, session.Calls);
    }

    [Fact]
    public async Task Un_changement_de_session_pousse_les_cellules_lues()
    {
        var session = new FakeSession();
        var src = new MediaSource(session);
        var pushed = new List<string>();
        src.Pushed += pushed.Add;
        await src.ReadAsync(Ctx("a"), CancellationToken.None);
        await src.ReadAsync(Ctx("b"), CancellationToken.None);
        session.Fire();
        Assert.Equal(new[] { "a", "b" }, pushed.OrderBy(x => x));
    }

    [Fact]
    public void Le_schema_declare_le_clic_par_defaut_et_le_registre_l_enregistre()
    {
        var registry = CoreSources.Build(new FakeSession());
        var schema = registry.Get("media")!.Schema;
        Assert.Equal("toggle", schema.DefaultAction);
        Assert.Equal("music", schema.DefaultGlyph);
        Assert.Contains("media", CoreSources.Build().Types);
    }
}
```

Dans `ConfigStoreTests.Le_premier_lancement_ecrit_un_cells_json_par_defaut`, remplacer l'assertion sur la première cellule par :
```csharp
        var main = store.Current.Pills[0];
        Assert.Equal(new[] { "sys", "cpu", "mem", "disk", "net", "battery", "media", "clickup" }, main.Cells.Select(c => c.Id));
        Assert.Equal(new[] { "cpu", "mem", "disk", "net", "battery" }, store.Current.Cell("sys")!.Children);
        Assert.Equal("cpu", store.Current.Cell("sys")!.Headline);
        Assert.All(new[] { "cpu", "mem", "disk", "net", "battery" }, id => Assert.False(store.Current.Cell(id)!.Visible));
        Assert.True(store.Current.Cell("media")!.Visible);
```

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.Core.Tests --filter "MediaSourceTests|ConfigStoreTests"`
Expected: compilation en échec (`IMediaSession`, `MediaSource` introuvables).

- [ ] **Step 3: Contrat et source**

`src/CustomNotch.Core/Sources/Media/IMediaSession.cs` :
```csharp
namespace CustomNotch.Core.Sources.Media;

/// <summary>Ce qui joue en ce moment, tel que le système le rapporte.</summary>
public sealed record MediaState(string Title, string Artist, string App, bool Playing);

/// <summary>La session média du système, vue de Core : lire l'état, agir, et prévenir. L'implémentation (WinRT) vit dans
/// l'application ; sans elle — tests, autre OS — la source média se dit hors service.</summary>
public interface IMediaSession
{
    MediaState? Current();
    Task ToggleAsync();
    Task NextAsync();
    Task PreviousAsync();
    /// <summary>Piste, lecture ou session qui change : les cellules abonnées sont relues sans attendre la cadence.</summary>
    event Action? Changed;
}
```

`src/CustomNotch.Core/Sources/Media/MediaSource.cs` :
```csharp
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.Media;

/// <summary>La cellule « ce qui joue » : titre — artiste, occupée en lecture, boutons précédent / lecture-pause / suivant.
/// Une seule instance sert toutes les cellules média : elle retient leurs ids pour les pousser au changement.</summary>
public sealed class MediaSource : SourceBase
{
    private readonly IMediaSession? _session;
    private readonly HashSet<string> _cells = new();
    private readonly object _lock = new();

    public MediaSource(IMediaSession? session)
    {
        _session = session;
        if (session is not null) session.Changed += () =>
        {
            string[] ids;
            lock (_lock) ids = _cells.ToArray();
            foreach (var id in ids) Push(id);
        };
    }

    public override string Type => "media";
    public override SourceSchema Schema => new(Type, "Média", Array.Empty<SchemaField>(), "music",
        "Ce qui joue (Spotify, navigateur, VLC…) : titre, lecture/pause, piste suivante", "toggle");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(5);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        lock (_lock) _cells.Add(ctx.CellId);
        var state = _session?.Current();
        if (state is null)
            return Task.FromResult(new Reading(Text: "Aucune lecture", Status: Status.Off, Detail: new[] { new DetailRow("Lecture", "aucune session média") }));
        var text = state.Artist.Length > 0 ? $"{state.Title} — {state.Artist}" : state.Title;
        return Task.FromResult(new Reading(Text: text, Status: state.Playing ? Status.Busy : Status.Ok,
            Detail: new[]
            {
                new DetailRow("Titre", state.Title), new DetailRow("Artiste", state.Artist.Length > 0 ? state.Artist : "—"),
                new DetailRow("Application", state.App),
            },
            Actions: new[]
            {
                new ActionSpec("prev", "Précédent", "prev"), new ActionSpec("toggle", state.Playing ? "Pause" : "Lecture", state.Playing ? "pause" : "play"),
                new ActionSpec("next", "Suivant", "next"),
            }));
    }

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        if (_session is null) return Task.CompletedTask;
        return action switch
        {
            "toggle" => _session.ToggleAsync(),
            "next" => _session.NextAsync(),
            "prev" => _session.PreviousAsync(),
            _ => Task.CompletedTask,
        };
    }
}
```

`src/CustomNotch.Core/Sources/CoreSources.cs` :
```csharp
using CustomNotch.Core.Sources.Media;

namespace CustomNotch.Core.Sources;

/// <summary>Toutes les sources livrées avec le cœur, dans un registre neuf. La session média est fournie par
/// l'application (WinRT) ; null = la source média se dit hors service.</summary>
public static class CoreSources
{
    public static SourceRegistry Build(IMediaSession? media = null)
    {
        var registry = new SourceRegistry();
        System.SystemSources.RegisterAll(registry);
        registry.Register(new LauncherSource());
        registry.Register(new HttpSource());
        registry.Register(new ShellSource());
        registry.Register(new MediaSource(media));
        return registry;
    }
}
```

`GlyphLibrary` (App) contient déjà `music`, `prev`/`next`/`play`/`pause` : ajouter `prev` et `next` s'ils manquent — `["prev"] = ("M11,3 L5,8 L11,13 M4,3 L4,13", false)`, `["next"] = ("M5,3 L11,8 L5,13 M12,3 L12,13", false)`.

- [ ] **Step 4: Nouveau défaut**

`src/CustomNotch.Core/Config/DefaultCells.cs` :
```csharp
namespace CustomNotch.Core.Config;

/// <summary>Le cells.json du premier lancement : une pilule à droite avec le groupe Système (ses enfants masqués : ils
/// vivent dans la carte du groupe), la lecture en cours, et un lanceur ClickUp. Tout le reste se règle dans Réglages.</summary>
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
                { "id": "sys", "label": "Système", "glyph": "cpu", "children": ["cpu", "mem", "disk", "net", "battery"], "headline": "cpu" },
                { "id": "cpu",     "source": "system.cpu",     "label": "CPU",      "glyph": "cpu",     "refresh": "2s", "visible": false },
                { "id": "mem",     "source": "system.memory",  "label": "Mémoire",  "glyph": "memory",  "refresh": "5s", "visible": false },
                { "id": "disk",    "source": "system.disk",    "label": "Disque",   "glyph": "disk",    "refresh": "1m", "visible": false, "params": { "drive": "C:" } },
                { "id": "net",     "source": "system.network", "label": "Réseau",   "glyph": "network", "refresh": "2s", "visible": false },
                { "id": "battery", "source": "system.battery", "label": "Batterie", "glyph": "battery", "visible": false },
                { "id": "media",   "source": "media",          "label": "Média",    "glyph": "music" },
                { "id": "clickup", "source": "launcher",       "label": "ClickUp",  "glyph": "link", "params": { "open": "https://app.clickup.com" } }
              ]
            }
          ]
        }
        """;
}
```

- [ ] **Step 5: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests`
Expected: PASS (dont `DocsExampleTests`, inchangé).

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(core): source média derrière IMediaSession ; groupe Système par défaut"
```

---

### Task 3: ConfigEditor — la porte d'écriture de l'interface

**Files:**
- Create: `src/CustomNotch.Core/Config/ConfigEditor.cs`
- Test: `tests/CustomNotch.Core.Tests/Config/ConfigEditorTests.cs`

**Interfaces:**
- Consumes: `ConfigStore` (`CellsPath`, `LocalPath`, `Secrets`, `Load()`, `LastErrors`, `SetPillLocal`), `CellsJson.Parse/Options`, `Json.WriteAtomic/Format`, `ConfigException`, `SourceSchema`.
- Produces: `ConfigEditor(ConfigStore store, IReadOnlyDictionary<string, SourceSchema> schemas)` avec `AddPill(edge) → id`, `RemovePill(id)`, `SetPillShared(id, mutate)`, `SetPillLocal(id, mutate)`, `MovePill(id, delta)`, `AddCell(pillId, sourceType) → id`, `RemoveCell(id)`, `SetCell(id, mutate)`, `MoveCell(id, delta)`, `MoveCellToPill(id, pillId)`, `SetCellVisible(id, bool)`, `SetSecret(name, value)`, `RemoveSecret(name)`, `SetSourceGlobal(type, mutate)`, `static SecretName(cellId, field) → "<cellId>.<field>"`, `static SecretPlaceholder(cellId, field) → "${secret:<cellId>.<field>}"`.

- [ ] **Step 1: Tests**

`tests/CustomNotch.Core.Tests/Config/ConfigEditorTests.cs` :
```csharp
using System.Text.Json.Nodes;
using Xunit;
using CustomNotch.Core;
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Config;

public class ConfigEditorTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, SourceSchema> Schemas = CoreSources.Build().Schemas;
    private readonly string _home = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly ConfigStore _store;
    private readonly ConfigEditor _editor;

    public ConfigEditorTests()
    {
        Directory.CreateDirectory(_home);
        _store = new ConfigStore(_home, Schemas);
        Assert.True(_store.Load());
        _editor = new ConfigEditor(_store, Schemas);
    }

    public void Dispose() { _store.Dispose(); try { Directory.Delete(_home, true); } catch (IOException) { } }

    private string Shared() => File.ReadAllText(_store.CellsPath);

    [Fact]
    public void Ajouter_une_pilule_puis_une_cellule_avec_les_defauts_du_schema()
    {
        var pill = _editor.AddPill("left");
        Assert.Equal("pill-2", pill);
        var cell = _editor.AddCell(pill, "system.disk");
        Assert.Equal("disk-2", cell);   // « disk » existe déjà dans le défaut
        var added = _store.Current.Cell(cell)!;
        Assert.Equal("system.disk", added.Source);
        Assert.Equal("Disque", added.Label);
        Assert.Equal("disk", added.Glyph);
        Assert.Equal("C:", added.Params!["drive"]!.GetValue<string>());
        Assert.Equal("left", _store.Current.Pills[1].Edge);
        Assert.StartsWith("// customNotch", Shared());
    }

    [Fact]
    public void Modifier_deplacer_supprimer_une_cellule()
    {
        _editor.SetCell("media", c => c["label"] = "Musique");
        Assert.Equal("Musique", _store.Current.Cell("media")!.Label);
        _editor.MoveCell("media", -1);
        Assert.Equal("media", _store.Current.Pills[0].Cells[0].Id);
        _editor.RemoveCell("cpu");
        Assert.Null(_store.Current.Cell("cpu"));
        var sys = _store.Current.Cell("sys")!;
        Assert.DoesNotContain("cpu", sys.Children!);
        Assert.Null(sys.Headline);   // la tête était cpu
    }

    [Fact]
    public void Masquer_va_dans_le_fichier_local_pas_dans_le_partage()
    {
        var before = Shared();
        _editor.SetCellVisible("media", false);
        Assert.Equal(before, Shared());
        Assert.False(_store.Current.Cell("media")!.Visible);
        Assert.Contains("\"visible\": false", File.ReadAllText(_store.LocalPath));
    }

    [Fact]
    public void Deplacer_une_cellule_vers_une_autre_pilule()
    {
        var pill = _editor.AddPill("top");
        _editor.MoveCellToPill("clickup", pill);
        Assert.Equal("clickup", _store.Current.Pills[1].Cells.Single().Id);
        Assert.DoesNotContain(_store.Current.Pills[0].Cells, c => c.Id == "clickup");
    }

    [Fact]
    public void Une_modification_refusee_est_annulee_et_remontee()
    {
        var before = Shared();
        var ex = Assert.Throws<ConfigException>(() => _editor.SetCell("sys", c => c["children"] = new JsonArray("sys")));
        Assert.Contains("boucle", ex.Message);
        Assert.Equal(before, Shared());
        Assert.Contains("cpu", _store.Current.Cell("sys")!.Children!);
    }

    [Fact]
    public void Un_secret_va_dans_secrets_json_et_le_partage_porte_le_placeholder()
    {
        var cell = _editor.AddCell("main", "http");
        _editor.SetSecret(ConfigEditor.SecretName(cell, "token"), "s3cr3t");
        _editor.SetCell(cell, c => { c["params"]!["url"] = "https://x"; c["params"]!["headers"] = new JsonObject { ["Authorization"] = "Bearer " + ConfigEditor.SecretPlaceholder(cell, "token") }; });
        Assert.DoesNotContain("s3cr3t", Shared());
        Assert.Equal("Bearer s3cr3t", _store.Current.Cell(cell)!.Params!["headers"]!["Authorization"]!.GetValue<string>());
        _editor.RemoveSecret(ConfigEditor.SecretName(cell, "token"));
        Assert.Equal("Bearer ", _store.Current.Cell(cell)!.Params!["headers"]!["Authorization"]!.GetValue<string>());
    }

    [Fact]
    public void Supprimer_une_pilule_retire_aussi_sa_surcharge_locale()
    {
        var pill = _editor.AddPill("bottom");
        _editor.SetPillLocal(pill, p => p["along"] = 0.2);
        _editor.RemovePill(pill);
        Assert.Single(_store.Current.Pills);
        Assert.DoesNotContain($"\"{pill}\"", File.ReadAllText(_store.LocalPath));
    }

    [Fact]
    public void Un_reglage_global_de_source_est_ecrit_sous_sources()
    {
        _editor.SetSourceGlobal("http", g => g["timeoutSeconds"] = 30);
        Assert.Equal(30, _store.Current.Sources!["http"]!["timeoutSeconds"]!.GetValue<int>());
    }
}
```
Ces tests supposent le **nouveau défaut** de la tâche 2 (`sys` avec `headline: cpu`, `media`, `clickup`) : exécuter la tâche 2 avant celle-ci.

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.Core.Tests --filter ConfigEditorTests`
Expected: compilation en échec (`ConfigEditor` introuvable).

- [ ] **Step 3: ConfigEditor**

`src/CustomNotch.Core/Config/ConfigEditor.cs` :
```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;

namespace CustomNotch.Core.Config;

/// <summary>La seule porte d'écriture de la fenêtre de réglages. Chaque opération relit le document concerné (pas de copie
/// en mémoire à désynchroniser), le modifie, l'écrit atomiquement, puis recharge le ConfigStore ; si la validation refuse,
/// le document précédent est réécrit et l'erreur remonte : jamais appliqué à moitié. Le fichier partagé est réécrit sans
/// ses commentaires manuels (System.Text.Json ne les conserve pas) ; un en-tête fixe le dit.</summary>
public sealed class ConfigEditor
{
    private const string Header =
        "// customNotch — cells.json édité par la fenêtre Réglages (les commentaires manuels ne sont pas conservés).\n" +
        "// Placeholders : ${env:NAME}, ${secret:name}, ${home}. Surcharge locale : cells.<machine>.json.\n";

    private readonly ConfigStore _store;
    private readonly IReadOnlyDictionary<string, SourceSchema> _schemas;

    public ConfigEditor(ConfigStore store, IReadOnlyDictionary<string, SourceSchema> schemas)
    {
        _store = store;
        _schemas = schemas;
    }

    public static string SecretName(string cellId, string field) => $"{cellId}.{field}";
    public static string SecretPlaceholder(string cellId, string field) => "${secret:" + SecretName(cellId, field) + "}";

    // ---- documents --------------------------------------------------------------------------------------------

    private JsonObject Shared()
        => File.Exists(_store.CellsPath) ? CellsJson.Parse(File.ReadAllText(_store.CellsPath)) : new JsonObject { ["version"] = 1, ["pills"] = new JsonArray() };

    private JsonObject Local()
    {
        try { return File.Exists(_store.LocalPath) ? CellsJson.Parse(File.ReadAllText(_store.LocalPath)) : new JsonObject(); }
        catch (ConfigException) { return new JsonObject(); }
    }

    private static JsonArray Pills(JsonObject root)
    {
        if (root["pills"] is not JsonArray a) root["pills"] = a = new JsonArray();
        return a;
    }

    private static JsonObject? PillNode(JsonObject root, string pillId)
        => Pills(root).OfType<JsonObject>().FirstOrDefault(p => p["id"]?.GetValue<string>() == pillId);

    private static JsonArray Cells(JsonObject pill)
    {
        if (pill["cells"] is not JsonArray a) pill["cells"] = a = new JsonArray();
        return a;
    }

    private static (JsonObject Pill, JsonObject Cell)? CellNode(JsonObject root, string cellId)
    {
        foreach (var pill in Pills(root).OfType<JsonObject>())
            foreach (var cell in Cells(pill).OfType<JsonObject>())
                if (cell["id"]?.GetValue<string>() == cellId) return (pill, cell);
        return null;
    }

    private static IEnumerable<JsonObject> AllCells(JsonObject root)
        => Pills(root).OfType<JsonObject>().SelectMany(p => Cells(p).OfType<JsonObject>());

    private static string Unique(string basis, IEnumerable<string> taken)
    {
        var set = taken.ToHashSet();
        if (!set.Contains(basis)) return basis;
        for (var i = 2; ; i++)
            if (!set.Contains($"{basis}-{i}")) return $"{basis}-{i}";
    }

    /// <summary>Écrit le partagé et recharge ; si la config est refusée, remet l'ancien texte et lève.</summary>
    private void CommitShared(JsonObject root)
    {
        var before = File.Exists(_store.CellsPath) ? File.ReadAllText(_store.CellsPath) : null;
        Json.WriteAtomic(_store.CellsPath, Header + root.ToJsonString(CellsJson.Options), "config");
        if (_store.Load()) return;
        var message = string.Join(" ; ", _store.LastErrors);
        if (before is not null) Json.WriteAtomic(_store.CellsPath, before, "config");
        _store.Load();
        throw new ConfigException(message);
    }

    private void CommitLocal(JsonObject root)
    {
        Json.WriteAtomic(_store.LocalPath, Json.Format(root), "config");
        if (!_store.Load()) throw new ConfigException(string.Join(" ; ", _store.LastErrors));
    }

    // ---- pilules ----------------------------------------------------------------------------------------------

    public string AddPill(string edge)
    {
        var root = Shared();
        var id = Unique("pill", Pills(root).OfType<JsonObject>().Select(p => p["id"]?.GetValue<string>() ?? ""));
        Pills(root).Add(new JsonObject { ["id"] = id, ["edge"] = edge, ["along"] = 0.5, ["cells"] = new JsonArray() });
        CommitShared(root);
        return id;
    }

    public void RemovePill(string pillId)
    {
        var root = Shared();
        var pill = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        Pills(root).Remove(pill);
        CommitShared(root);
        var local = Local();
        if (local["pills"] is JsonArray pills && pills.OfType<JsonObject>().FirstOrDefault(p => p["id"]?.GetValue<string>() == pillId) is { } entry)
        {
            pills.Remove(entry);
            CommitLocal(local);
        }
    }

    public void SetPillShared(string pillId, Action<JsonObject> mutate)
    {
        var root = Shared();
        mutate(PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable"));
        CommitShared(root);
    }

    public void SetPillLocal(string pillId, Action<JsonObject> mutate)
    {
        if (!_store.SetPillLocal(pillId, mutate)) throw new ConfigException(string.Join(" ; ", _store.LastErrors));
    }

    public void MovePill(string pillId, int delta)
    {
        var root = Shared();
        var pills = Pills(root);
        var pill = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        var from = pills.IndexOf(pill);
        var to = Math.Clamp(from + delta, 0, pills.Count - 1);
        if (to == from) return;
        pills.RemoveAt(from);
        pills.Insert(to, pill);
        CommitShared(root);
    }

    // ---- cellules ---------------------------------------------------------------------------------------------

    /// <summary>Une cellule neuve : id dérivé du type (« system.disk » → « disk », suffixé si pris), libellé et glyph du
    /// schéma, params remplis avec les défauts déclarés.</summary>
    public string AddCell(string pillId, string sourceType)
    {
        var schema = _schemas.GetValueOrDefault(sourceType) ?? throw new ConfigException($"source « {sourceType} » inconnue");
        var root = Shared();
        var pill = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        var id = Unique(sourceType.Split('.')[^1], AllCells(root).Select(c => c["id"]?.GetValue<string>() ?? ""));
        var cell = new JsonObject { ["id"] = id, ["source"] = sourceType, ["label"] = schema.Title };
        if (schema.DefaultGlyph is { } glyph) cell["glyph"] = glyph;
        var parameters = new JsonObject();
        foreach (var field in schema.Fields.Where(f => f.Default is not null))
            parameters[field.Name] = field.Type switch
            {
                "number" => JsonValue.Create(double.Parse(field.Default!, CultureInfo.InvariantCulture)),
                "bool" => JsonValue.Create(bool.Parse(field.Default!)),
                _ => JsonValue.Create(field.Default!),
            };
        if (parameters.Count > 0) cell["params"] = parameters;
        Cells(pill).Add(cell);
        CommitShared(root);
        return id;
    }

    /// <summary>Retire la cellule, et son id de tous les groupes qui la listaient (children, headline).</summary>
    public void RemoveCell(string cellId)
    {
        var root = Shared();
        var found = CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        Cells(found.Pill).Remove(found.Cell);
        foreach (var cell in AllCells(root))
        {
            if (cell["children"] is JsonArray children)
            {
                var gone = children.Where(c => c?.GetValue<string>() == cellId).ToList();
                foreach (var g in gone) children.Remove(g);
            }
            if (cell["headline"]?.GetValue<string>() == cellId) cell.Remove("headline");
        }
        CommitShared(root);
    }

    public void SetCell(string cellId, Action<JsonObject> mutate)
    {
        var root = Shared();
        mutate((CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable")).Cell);
        CommitShared(root);
    }

    public void MoveCell(string cellId, int delta)
    {
        var root = Shared();
        var found = CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        var cells = Cells(found.Pill);
        var from = cells.IndexOf(found.Cell);
        var to = Math.Clamp(from + delta, 0, cells.Count - 1);
        if (to == from) return;
        cells.RemoveAt(from);
        cells.Insert(to, found.Cell);
        CommitShared(root);
    }

    public void MoveCellToPill(string cellId, string pillId)
    {
        var root = Shared();
        var found = CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        var target = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        Cells(found.Pill).Remove(found.Cell);
        Cells(target).Add(found.Cell);
        CommitShared(root);
    }

    /// <summary>Masquer est un choix de poste, comme la position : il va dans la surcharge locale.</summary>
    public void SetCellVisible(string cellId, bool visible)
    {
        var shared = Shared();
        var found = CellNode(shared, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        var pillId = found.Pill["id"]!.GetValue<string>();
        SetPillLocal(pillId, pill =>
        {
            if (pill["cells"] is not JsonArray cells) pill["cells"] = cells = new JsonArray();
            var entry = cells.OfType<JsonObject>().FirstOrDefault(c => c["id"]?.GetValue<string>() == cellId);
            if (entry is null) cells.Add(entry = new JsonObject { ["id"] = cellId });
            entry["visible"] = visible;
        });
    }

    // ---- secrets et réglages globaux --------------------------------------------------------------------------

    public void SetSecret(string name, string value)
    {
        _store.Secrets.Set(name, value);
        _store.Load();
    }

    public void RemoveSecret(string name)
    {
        _store.Secrets.Remove(name);
        _store.Load();
    }

    public void SetSourceGlobal(string sourceType, Action<JsonObject> mutate)
    {
        var root = Shared();
        if (root["sources"] is not JsonObject sources) root["sources"] = sources = new JsonObject();
        if (sources[sourceType] is not JsonObject entry) sources[sourceType] = entry = new JsonObject();
        mutate(entry);
        CommitShared(root);
    }
}
```

- [ ] **Step 4: Tests verts**

Run: `dotnet test tests/CustomNotch.Core.Tests` (après la tâche 2, qui pose le nouveau défaut)
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(core): ConfigEditor — l'écriture de la configuration par l'interface"
```

---
### Task 4: Session média WinRT (App) et injection

**Files:**
- Create: `src/CustomNotch.App/Platform/WindowsMediaSession.cs`
- Modify: `src/CustomNotch.App/Controller.cs`

**Interfaces:**
- Consumes: `IMediaSession`, `MediaState`, `CoreSources.Build(IMediaSession?)`, `Log`.
- Produces: `WindowsMediaSession.Create() → WindowsMediaSession` (jamais null : l'initialisation WinRT est asynchrone, `Current()` rend null tant qu'elle n'a pas abouti), `IDisposable`.

- [ ] **Step 1: WindowsMediaSession**

`src/CustomNotch.App/Platform/WindowsMediaSession.cs` :
```csharp
using CustomNotch.Core;
using CustomNotch.Core.Sources.Media;
using Windows.Foundation;
using Windows.Media.Control;

namespace CustomNotch.App.Platform;

/// <summary>La session média système (celle que la touche lecture/pause du clavier pilote) : Spotify, un onglet
/// YouTube, VLC… sans compte ni API. L'initialisation WinRT est asynchrone : l'objet existe tout de suite (pour être
/// enregistré avant l'ordonnanceur), son état arrive un peu après et déclenche Changed.</summary>
public sealed class WindowsMediaSession : IMediaSession, IDisposable
{
    private readonly object _lock = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaState? _state;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs>? _onProps;
    private TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs>? _onPlayback;

    public event Action? Changed;

    public static WindowsMediaSession Create()
    {
        var s = new WindowsMediaSession();
        _ = s.InitAsync();
        return s;
    }

    private async Task InitAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (m, _) => Attach(m.GetCurrentSession());
            Attach(_manager.GetCurrentSession());
        }
        catch (Exception ex)
        {
            Log.Warning("media", $"Session média indisponible : {ex.Message}");
        }
    }

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        lock (_lock)
        {
            if (_session is not null)
            {
                if (_onProps is not null) _session.MediaPropertiesChanged -= _onProps;
                if (_onPlayback is not null) _session.PlaybackInfoChanged -= _onPlayback;
            }
            _session = session;
            if (session is not null)
            {
                _onProps = (_, _) => _ = RefreshAsync();
                _onPlayback = (_, _) => _ = RefreshAsync();
                session.MediaPropertiesChanged += _onProps;
                session.PlaybackInfoChanged += _onPlayback;
            }
        }
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_lock) session = _session;
        if (session is null) { Set(null); return; }
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playing = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var title = props?.Title ?? "";
            Set(title.Length == 0 ? null : new MediaState(title, props?.Artist ?? "", AppName(session.SourceAppUserModelId), playing));
        }
        catch (Exception ex)
        {
            Log.Warning("media", $"Lecture de la session média : {ex.Message}");
            Set(null);
        }
    }

    /// <summary>« Spotify.exe » ou « SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify » → un nom court.</summary>
    private static string AppName(string aumid)
    {
        var s = aumid.Split('!')[0];
        s = s.Split('_')[0];
        s = s.Split('.').Last(part => part.Length > 0);
        return s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s;
    }

    private void Set(MediaState? state)
    {
        lock (_lock)
        {
            if (Equals(state, _state)) return;
            _state = state;
        }
        Changed?.Invoke();
    }

    public MediaState? Current() { lock (_lock) return _state; }

    private async Task Do(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> op)
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_lock) session = _session;
        if (session is null) return;
        try { await op(session); }
        catch (Exception ex) { Log.Warning("media", $"Commande média : {ex.Message}"); }
    }

    public Task ToggleAsync() => Do(s => s.TryTogglePlayPauseAsync());
    public Task NextAsync() => Do(s => s.TrySkipNextAsync());
    public Task PreviousAsync() => Do(s => s.TrySkipPreviousAsync());

    public void Dispose()
    {
        Attach(null);
        _manager = null;
    }
}
```
Si le compilateur refuse `await` sur un `IAsyncOperation<T>` (CsWinRT le fournit normalement avec ce TFM), utiliser `.AsTask()` avec `using System;` — noter le choix dans le rapport.

- [ ] **Step 2: Injection dans Controller**

`src/CustomNotch.App/Controller.cs` : remplacer l'initialiseur de champ `_registry = CoreSources.Build()` par deux champs — `private readonly Platform.WindowsMediaSession _media = Platform.WindowsMediaSession.Create();` **déclaré avant** `_registry`, puis `private readonly SourceRegistry _registry;` initialisé dans le constructeur : `_registry = CoreSources.Build(_media);` **avant** `new ConfigStore(...)` et `new Scheduler(...)`. Dans `Stop()` : `_media.Dispose();`.

- [ ] **Step 3: Build, tests, vérification à l'écran**

Run: `dotnet build CustomNotch.sln` (0 avertissement), `dotnet test CustomNotch.sln`.
Puis (si le bureau est libre) : sauvegarder `%APPDATA%\customNotch\cells.json` en `cells.json.bak`, le supprimer, lancer `dotnet run --project src/CustomNotch.App`. Expected : trois cellules — « Système » (anneau CPU, carte avec cinq lignes), « Média », « ClickUp ». Lancer une lecture (Spotify, ou un onglet YouTube) : la cellule Média passe en pastille bleue avec le titre tronqué dessous ; un clic met en pause (pastille verte, bouton « Lecture » dans la carte) ; « Suivant » dans la carte change de piste. Sans lecture : glyph gris, « Aucune lecture ». Restaurer `cells.json.bak` ensuite et tuer l'app. Sans bureau libre : le dire et vérifier par lecture.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(app): session média WinRT branchée sur la source media"
```

---

### Task 5: La fenêtre Réglages — coquille, pages Général et Sources, styles de contrôles

**Files:**
- Create: `src/CustomNotch.App/Shared/Controls.xaml` (copie), `src/CustomNotch.Core/Platform/Autostart.cs` (copie)
- Create: `src/CustomNotch.App/Settings/SettingsContext.cs`, `Settings/PageBase.cs`, `Settings/SettingsWindow.cs`, `Settings/GeneralPage.cs`, `Settings/SourcesPage.cs`, `Settings/PillsPage.cs` (provisoire : une page vide, remplacée en tâche 7)
- Modify: `src/CustomNotch.App/App.xaml`, `Styles.xaml`, `Controller.cs`

**Interfaces:**
- Consumes: `ConfigStore` (`Current`, `Changed`, `App`, `Secrets`, `CellsPath`), `ConfigEditor`, `SourceRegistry`, `Theme` (`Apply`, `ApplyChrome`, `ResolveTheme`), `Ui` (`Text`, `FormRow`, `FormLabel`), `ActionRunner.Open`, `Log.Directory`, `App.Version`.
- Produces: `record SettingsContext(ConfigStore Store, ConfigEditor Editor, SourceRegistry Registry)`, `abstract class PageBase : UserControl` (`Ctx`, `Body`, `Refresh()`, `Detach()`, `Section(text)`, `Card(content)`, `Row(label, field)`, `Combo(items, value, onChange)`, `Debounced(TextBox, Action<string>)`), `SettingsWindow(SettingsContext)` avec `Go(string pageKey)`, `Controller.ShowSettings()` = une seule fenêtre, `Autostart.IsEnabled()/SetEnabled(bool)/LaunchCommand()`.

- [ ] **Step 1: Styles de contrôles repris de ClickUp-Extended**

Créer `src/CustomNotch.App/Shared/Controls.xaml` : un `<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">` contenant, **copiés tels quels** depuis `C:\Users\Coco_FW\Desktop\ClickUp-Extended\src\ClickUpExtended.App\App.xaml` (bloc `<Application.Resources><ResourceDictionary>`), les styles suivants et rien d'autre : `Card` (Border), `Section`, `Hint`, `Field` (TextBox), `ThumbStyle`, `ScrollBar`, `ToolTip`, `CheckBox`, `ComboBox`, `ComboBoxItem`, `TextBox`, `PasswordBox`, `Button` (le style par défaut, sans clé), `ListBox`, `ListBoxItem`, `ProgressBar`. **Ne pas copier** : `UiFont`, `Primary`, `Secondary`, `GhostButton`, `ContextMenu`, `MenuItem`, `MenuItem.SeparatorStyleKey` (déjà dans `Styles.xaml`). Vérifier par `git diff --no-index` bloc à bloc que le texte copié est identique à l'original.

`src/CustomNotch.App/App.xaml` — fusionner après `Styles.xaml` :
```xml
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Styles.xaml" />
                <ResourceDictionary Source="Shared/Controls.xaml" />
            </ResourceDictionary.MergedDictionaries>
```
Le csproj inclut les `.xaml` comme `Page` automatiquement (`UseWPF`) ; si le build se plaint d'un `x:Class`, aucun n'est attendu dans un dictionnaire.

`src/CustomNotch.App/Styles.xaml` — ajouter avant `</ResourceDictionary>` :
```xml
    <!-- Navigation de la fenêtre Réglages (patron AutoSort, pinceaux de notre palette). -->
    <Style x:Key="NavList" TargetType="ListBox">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="ItemContainerStyle">
            <Setter.Value>
                <Style TargetType="ListBoxItem">
                    <Setter Property="Foreground" Value="{DynamicResource Muted}" />
                    <Setter Property="Cursor" Value="Hand" />
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="ListBoxItem">
                                <Border x:Name="Bd" Background="Transparent" CornerRadius="8" Margin="0,2" Padding="12,9" Height="40">
                                    <ContentPresenter VerticalAlignment="Center" />
                                </Border>
                                <ControlTemplate.Triggers>
                                    <Trigger Property="IsMouseOver" Value="True">
                                        <Setter TargetName="Bd" Property="Background" Value="{DynamicResource SurfaceHover}" />
                                        <Setter Property="Foreground" Value="{DynamicResource Fg}" />
                                    </Trigger>
                                    <Trigger Property="IsSelected" Value="True">
                                        <Setter TargetName="Bd" Property="Background" Value="{DynamicResource Accent}" />
                                        <Setter Property="Foreground" Value="{DynamicResource OnAccent}" />
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </Setter.Value>
        </Setter>
    </Style>
    <!-- L'arbre pilules → cellules : une liste indentée, stylée comme la navigation mais en Surface. -->
    <Style x:Key="TreeList" TargetType="ListBox" BasedOn="{StaticResource NavList}">
        <Setter Property="ItemContainerStyle">
            <Setter.Value>
                <Style TargetType="ListBoxItem">
                    <Setter Property="Foreground" Value="{DynamicResource Fg}" />
                    <Setter Property="Cursor" Value="Hand" />
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="ListBoxItem">
                                <Border x:Name="Bd" Background="Transparent" CornerRadius="6" Margin="0,1" Padding="8,6">
                                    <ContentPresenter VerticalAlignment="Center" />
                                </Border>
                                <ControlTemplate.Triggers>
                                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Background" Value="{DynamicResource SurfaceHover}" /></Trigger>
                                    <Trigger Property="IsSelected" Value="True"><Setter TargetName="Bd" Property="Background" Value="{DynamicResource AccentSoft}" /></Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </Setter.Value>
        </Setter>
    </Style>
```
(`AccentSoft` et `Sidebar` sont posés par `Theme.Apply`.)

- [ ] **Step 2: Autostart (copie)**

`src/CustomNotch.Core/Platform/Autostart.cs` : copier la classe `Autostart` (de `public static class Autostart` à son accolade fermante) depuis `C:\Users\Coco_FW\Desktop\ClickUp-Extended\src\ClickUpExtended.Core\Platform.cs`, avec `using System.Diagnostics; using Microsoft.Win32;`, namespace `CustomNotch.Core.Platform` ; deux modifications : `LegacyNames = Array.Empty<string>()` et, dans `LaunchCommand`, le nom de repli `"ClickUp - Extended"` → `App.Name`. (Les gardes `OperatingSystem.IsWindows()` sont ce qui fait accepter `Registry` sur `net10.0` par l'analyseur CA1416.)

- [ ] **Step 3: Contexte, PageBase, fenêtre**

`src/CustomNotch.App/Settings/SettingsContext.cs` :
```csharp
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Ce que les pages de réglages reçoivent : l'état (Store), la plume (Editor), le catalogue (Registry).</summary>
public sealed record SettingsContext(ConfigStore Store, ConfigEditor Editor, SourceRegistry Registry);
```

`src/CustomNotch.App/Settings/PageBase.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CustomNotch.App.Settings;

/// <summary>Une page : en-tête (titre, sous-titre) et corps vertical, dans un défilement. Les briques communes aux pages
/// (section, carte, ligne de formulaire, combo, champ à anti-rebond) vivent ici pour que chaque page reste courte.</summary>
public abstract class PageBase : UserControl
{
    protected readonly StackPanel Body = new();
    private readonly List<Action> _detach = new();

    protected PageBase(SettingsContext ctx, string title, string subtitle = "")
    {
        Ctx = ctx;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(Ui.Text(title, 21, FontWeights.SemiBold));
        if (subtitle.Length > 0)
        {
            var sub = Ui.Text(subtitle, 12, null, "Muted");
            sub.TextWrapping = TextWrapping.Wrap;
            sub.Margin = new Thickness(0, 4, 0, 0);
            header.Children.Add(sub);
        }
        var stack = new StackPanel { Margin = new Thickness(28, 22, 28, 20) };
        stack.Children.Add(header);
        stack.Children.Add(Body);
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
    }

    protected SettingsContext Ctx { get; }

    /// <summary>Relit l'état et reconstruit ce qui doit l'être (appelé à l'ouverture et quand la config change ailleurs).</summary>
    public virtual void Refresh() { }

    /// <summary>Abonne un gestionnaire au ConfigStore en notant comment le désabonner.</summary>
    protected void OnStoreChanged(Action handler)
    {
        Action<Core.Config.CellsFile> h = _ => Dispatcher.BeginInvoke(handler);
        Ctx.Store.Changed += h;
        _detach.Add(() => Ctx.Store.Changed -= h);
    }

    public void Detach()
    {
        foreach (var undo in _detach) undo();
        _detach.Clear();
    }

    protected static TextBlock Section(string text)
    {
        var t = Ui.Text(text.ToUpperInvariant(), 11, FontWeights.SemiBold, "Muted");
        t.Margin = new Thickness(0, 18, 0, 6);
        return t;
    }

    protected static Border Card(UIElement content)
    {
        var card = new Border { Child = content, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        return card;
    }

    protected static Grid Row(string label, UIElement field, double labelWidth = 220) => Ui.FormRow(Ui.FormLabel(label), field, labelWidth);

    /// <summary>Une liste déroulante valeur/libellé qui rappelle onChange avec la valeur choisie.</summary>
    protected static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string? current, Action<string> onChange)
    {
        var combo = new ComboBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (value, label) in items) combo.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        combo.SelectedIndex = Math.Max(0, items.ToList().FindIndex(i => i.Value == current));
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem it && it.Tag is string v) onChange(v); };
        return combo;
    }

    /// <summary>Un champ texte qui rappelle onChange 300 ms après la dernière frappe (et à la perte du focus).</summary>
    protected static TextBox Debounced(string initial, Action<string> onChange, double width = 360)
    {
        var box = new TextBox { Text = initial, Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var last = initial;
        void Flush() { timer.Stop(); if (box.Text != last) { last = box.Text; onChange(box.Text); } }
        timer.Tick += (_, _) => Flush();
        box.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        box.LostFocus += (_, _) => Flush();
        return box;
    }

    protected static Button Btn(string text, Action click, string style = "Secondary")
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0) };
        b.SetResourceReference(StyleProperty, style);
        b.Click += (_, _) => click();
        return b;
    }
}
```

`src/CustomNotch.App/Settings/SettingsWindow.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;

namespace CustomNotch.App.Settings;

/// <summary>La fenêtre Réglages : sidebar à gauche, page à droite, « Fermer » en bas. Une seule instance, tenue par le
/// contrôleur ; chaque page applique ses changements immédiatement (pas de bouton Enregistrer, comme dans les autres apps).</summary>
public sealed class SettingsWindow : Window
{
    public static readonly (string Key, string Label)[] Nav = { ("pills", "Pilules & cellules"), ("sources", "Sources"), ("general", "Général") };

    private readonly ListBox _nav = new();
    private readonly ContentControl _host = new();
    private readonly Dictionary<string, PageBase> _pages = new();

    public SettingsWindow(SettingsContext ctx)
    {
        Title = "customNotch — Réglages";
        Width = 1040; Height = 720; MinWidth = 900; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Bg");
        SetResourceReference(ForegroundProperty, "Fg");
        FontFamily = (System.Windows.Media.FontFamily)FindResource("UiFont");

        _pages["pills"] = new PillsPage(ctx);
        _pages["sources"] = new SourcesPage(ctx);
        _pages["general"] = new GeneralPage(ctx);

        var side = new Grid { Width = 220 };
        side.SetResourceReference(BackgroundProperty, "Sidebar");
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var brand = new StackPanel { Margin = new Thickness(16, 18, 16, 12) };
        brand.Children.Add(Ui.Text("customNotch", 20, FontWeights.SemiBold));
        brand.Children.Add(Ui.Text("Réglages", 11.5, null, "Muted"));
        side.Children.Add(brand);
        _nav.SetResourceReference(StyleProperty, "NavList");
        _nav.Margin = new Thickness(8, 0, 8, 0);
        foreach (var (key, label) in Nav) _nav.Items.Add(new ListBoxItem { Content = label, Tag = key });
        _nav.SelectionChanged += (_, _) => { if (_nav.SelectedItem is ListBoxItem it && it.Tag is string key) Show(key); };
        Grid.SetRow(_nav, 1);
        side.Children.Add(_nav);
        var version = Ui.Text($"version {Core.App.Version}", 11.5, null, "Muted");
        version.Margin = new Thickness(16, 8, 16, 16);
        Grid.SetRow(version, 2);
        side.Children.Add(version);

        var bottom = new Border { Padding = new Thickness(20, 10, 20, 10), BorderThickness = new Thickness(0, 1, 0, 0) };
        bottom.SetResourceReference(Border.BackgroundProperty, "Surface");
        bottom.SetResourceReference(Border.BorderBrushProperty, "Border");
        var close = new Button { Content = "Fermer", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 100 };
        close.SetResourceReference(StyleProperty, "Primary");
        close.Click += (_, _) => Close();
        bottom.Child = close;

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(_host);
        Grid.SetRow(bottom, 1);
        content.Children.Add(bottom);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(side);
        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Content = root;

        SourceInitialized += (_, _) => Theme.ApplyChrome(this);
        Closed += (_, _) => { foreach (var p in _pages.Values) p.Detach(); };
        _nav.SelectedIndex = 0;
    }

    public void Go(string key) => _nav.SelectedIndex = Array.FindIndex(Nav, n => n.Key == key);

    private void Show(string key)
    {
        var page = _pages[key];
        page.Refresh();
        _host.Content = page;
    }
}
```

- [ ] **Step 4: Pages Général et Sources, page Pilules provisoire**

`src/CustomNotch.App/Settings/GeneralPage.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core;
using CustomNotch.Core.Actions;
using CustomNotch.Core.Platform;

namespace CustomNotch.App.Settings;

public sealed class GeneralPage : PageBase
{
    private static readonly (string, string)[] Themes = { ("system", "Automatique (suit Windows)"), ("light", "Clair"), ("dark", "Sombre") };
    private readonly TextBox _cellsPath;
    private readonly CheckBox _autostart = new() { Content = "Lancer customNotch à l'ouverture de session" };

    public GeneralPage(SettingsContext ctx) : base(ctx, "Général", "L'emplacement de la configuration, le démarrage, le thème.")
    {
        _cellsPath = new TextBox { Text = ctx.Store.CellsPath, IsReadOnly = true, Width = 460, HorizontalAlignment = HorizontalAlignment.Left };
        var browse = Btn("Parcourir…", () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "cells.json|*.json", FileName = ctx.Store.CellsPath, CheckFileExists = false };
            if (dialog.ShowDialog() != true) return;
            ctx.Store.App.Set("cells_path", dialog.FileName);
            ctx.Store.App.Save();
            _cellsPath.Text = dialog.FileName + "  (au prochain démarrage)";
        });
        var openDir = Btn("Ouvrir le dossier", () => ActionRunner.Open(Path.GetDirectoryName(ctx.Store.CellsPath)!));
        var pathRow = new StackPanel { Orientation = Orientation.Horizontal };
        pathRow.Children.Add(_cellsPath); browse.Margin = new Thickness(8, 0, 8, 0); pathRow.Children.Add(browse); pathRow.Children.Add(openDir);
        var hint = Ui.Text("Mets ce fichier dans un dossier synchronisé pour retrouver tes pilules sur une autre machine ; la position et l'écran restent propres à chaque poste.", 11, null, "Muted");
        hint.TextWrapping = TextWrapping.Wrap;

        Body.Children.Add(Section("Configuration"));
        Body.Children.Add(Card(Stack(Row("Fichier cells.json", pathRow), hint)));

        _autostart.IsChecked = Autostart.IsEnabled();
        _autostart.Checked += (_, _) => Autostart.SetEnabled(true);
        _autostart.Unchecked += (_, _) => Autostart.SetEnabled(false);
        var theme = Combo(Themes, ctx.Store.App.GetString("appearance.theme", "system"), v =>
        {
            ctx.Store.App.Set("appearance.theme", v);
            ctx.Store.App.Save();
            Theme.Apply(Application.Current, ctx.Store.App);
        });
        Body.Children.Add(Section("Poste"));
        Body.Children.Add(Card(Stack(Row("Démarrage", _autostart), Row("Thème des fenêtres", theme))));

        var journal = Btn("Ouvrir le journal", () => { if (Log.Directory is { } d) ActionRunner.Open(d); });
        var repo = Btn("Le dépôt sur GitHub", () => ActionRunner.Open("https://github.com/c-chares69/customNotch"), "GhostButton");
        var about = new StackPanel { Orientation = Orientation.Horizontal };
        about.Children.Add(journal); about.Children.Add(repo);
        Body.Children.Add(Section("À propos"));
        Body.Children.Add(Card(Stack(Row($"customNotch {Core.App.Version}", about))));
    }

    private static StackPanel Stack(params UIElement[] children)
    {
        var s = new StackPanel();
        foreach (var c in children) s.Children.Add(c);
        return s;
    }
}
```

`src/CustomNotch.App/Settings/SourcesPage.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;

namespace CustomNotch.App.Settings;

/// <summary>Les réglages globaux par type de source (aucun dans cette version : la page le dit) et la liste des secrets —
/// leurs noms, jamais leurs valeurs.</summary>
public sealed class SourcesPage : PageBase
{
    private readonly StackPanel _secrets = new();

    public SourcesPage(SettingsContext ctx) : base(ctx, "Sources", "Ce qui vaut pour toutes les cellules d'un même type, et les secrets chiffrés sur ce poste.")
    {
        var none = Ui.Text("Aucune source livrée avec cette version n'a de réglage global. Claude Code et ClickUp en auront.", 12, null, "Muted");
        none.TextWrapping = TextWrapping.Wrap;
        Body.Children.Add(Section("Réglages globaux"));
        Body.Children.Add(Card(none));
        Body.Children.Add(Section("Secrets (secrets.json, chiffrés avec ton compte Windows)"));
        Body.Children.Add(Card(_secrets));
        OnStoreChanged(Refresh);
    }

    public override void Refresh()
    {
        _secrets.Children.Clear();
        var names = Ctx.Store.Secrets.Names.OrderBy(n => n).ToList();
        if (names.Count == 0) { _secrets.Children.Add(Ui.Text("Aucun secret. Un champ « secret » d'une cellule en crée un.", 12, null, "Muted")); return; }
        foreach (var name in names)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var remove = Btn("Effacer", () => Ctx.Editor.RemoveSecret(name), "GhostButton");
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            var defined = Ui.Text("défini", 11, null, "Muted"); defined.Margin = new Thickness(12, 0, 12, 0); DockPanel.SetDock(defined, Dock.Right);
            row.Children.Add(defined);
            row.Children.Add(Ui.Text(name, 13));
            _secrets.Children.Add(row);
        }
    }
}
```

`src/CustomNotch.App/Settings/PillsPage.cs` (provisoire, remplacée en tâche 7) :
```csharp
namespace CustomNotch.App.Settings;

public sealed class PillsPage : PageBase
{
    public PillsPage(SettingsContext ctx) : base(ctx, "Pilules & cellules", "Arrive à la tâche 7.") { }
}
```

- [ ] **Step 5: Controller — une seule fenêtre**

`src/CustomNotch.App/Controller.cs` : champs `private readonly ConfigEditor _editor;` (initialisé dans le constructeur après `_config` : `_editor = new ConfigEditor(_config, _registry.Schemas);`) et `private Settings.SettingsWindow? _settings;`. Remplacer `ShowSettings` :
```csharp
    /// <summary>Une seule fenêtre Réglages : réouverte ou ramenée au premier plan.</summary>
    public void ShowSettings()
    {
        if (_settings is null)
        {
            _settings = new Settings.SettingsWindow(new Settings.SettingsContext(_config, _editor, _registry));
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    }
```
Dans `Stop()` : `_settings?.Close();` avant la fermeture des pilules.

- [ ] **Step 6: Build, tests, vérification à l'écran**

Run: `dotnet build CustomNotch.sln` (0 avertissement — attention aux styles copiés : une clé absente lèverait à l'exécution, pas à la compilation), `dotnet test CustomNotch.sln`.
Écran (si libre) : lancer, « Réglages… » dans le tray → la fenêtre s'ouvre, sidebar à trois entrées, page Général : le chemin de `cells.json`, la case Démarrage (cocher → `HKCU\…\Run\customNotch` apparaît ; décocher → disparaît), le thème (Sombre → la fenêtre passe sombre immédiatement ; Clair → clair ; la pilule reste noire), « Ouvrir le journal » ouvre l'explorateur. Page Sources : « Aucun secret ». Rouvrir depuis le menu clic-droit de la pilule : la même fenêtre revient. Tuer l'app ensuite.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(app): fenêtre Réglages — coquille, pages Général et Sources, styles de contrôles"
```

---

### Task 6: SchemaForm — le formulaire généré depuis le schéma d'une source

**Files:**
- Create: `src/CustomNotch.App/Settings/SchemaForm.cs`
- Test: `tests/CustomNotch.App.Tests/SchemaFormTests.cs`

**Interfaces:**
- Consumes: `SourceSchema`, `SchemaField`, `Ui.FormRow/FormLabel`, `ConfigEditor.SecretPlaceholder`.
- Produces: `enum SchemaForm.Kind { Text, Number, Check, Choice, Path, Url, Secret }`, `SchemaForm.ControlKind(SchemaField) → Kind`, `SchemaForm.InitialText(SchemaField, JsonObject? params) → string` (valeur affichée : la valeur, sinon `Default`, sinon vide ; un placeholder `${secret:…}` rend vide), `SchemaForm.IsSecretSet(SchemaField, JsonObject?) → bool`, `SchemaForm.ParseNumber(string) → double?`, `SchemaForm.Build(SourceSchema schema, JsonObject? params, Action<string, JsonNode?> onChange, Action<string, string?> onSecret) → UIElement` (`onSecret(fieldName, value)` : la page stocke le secret et pose le placeholder ; `value` null = effacer).

- [ ] **Step 1: Tests (fonctions pures)**

`tests/CustomNotch.App.Tests/SchemaFormTests.cs` :
```csharp
using System.Text.Json.Nodes;
using Xunit;
using CustomNotch.App.Settings;
using CustomNotch.Core.Sources;
namespace CustomNotch.App.Tests;

public class SchemaFormTests
{
    private static SchemaField F(string type, string? def = null) => new("x", type, "X", Default: def);

    [Theory]
    [InlineData("string", SchemaForm.Kind.Text)] [InlineData("number", SchemaForm.Kind.Number)] [InlineData("bool", SchemaForm.Kind.Check)]
    [InlineData("choice", SchemaForm.Kind.Choice)] [InlineData("path", SchemaForm.Kind.Path)] [InlineData("url", SchemaForm.Kind.Url)]
    [InlineData("secret", SchemaForm.Kind.Secret)] [InlineData("zzz", SchemaForm.Kind.Text)]
    public void Chaque_type_a_son_controle(string type, SchemaForm.Kind expected) => Assert.Equal(expected, SchemaForm.ControlKind(F(type)));

    [Fact]
    public void La_valeur_initiale_vient_des_params_puis_du_defaut()
    {
        Assert.Equal("D:", SchemaForm.InitialText(F("string", "C:"), new JsonObject { ["x"] = "D:" }));
        Assert.Equal("C:", SchemaForm.InitialText(F("string", "C:"), null));
        Assert.Equal("", SchemaForm.InitialText(F("string"), new JsonObject()));
        Assert.Equal("3.5", SchemaForm.InitialText(F("number"), new JsonObject { ["x"] = 3.5 }));
    }

    [Fact]
    public void Un_secret_affiche_vide_mais_se_sait_defini()
    {
        var p = new JsonObject { ["x"] = "${secret:c.x}" };
        Assert.Equal("", SchemaForm.InitialText(F("secret"), p));
        Assert.True(SchemaForm.IsSecretSet(F("secret"), p));
        Assert.False(SchemaForm.IsSecretSet(F("secret"), new JsonObject { ["x"] = "" }));
    }

    [Fact]
    public void Les_nombres_acceptent_virgule_et_point()
    {
        Assert.Equal(1.5, SchemaForm.ParseNumber("1,5"));
        Assert.Equal(2, SchemaForm.ParseNumber(" 2 "));
        Assert.Null(SchemaForm.ParseNumber("deux"));
        Assert.Null(SchemaForm.ParseNumber(""));
    }
}
```

- [ ] **Step 2: Vérifier l'échec**

Run: `dotnet test tests/CustomNotch.App.Tests --filter SchemaFormTests`
Expected: compilation en échec.

- [ ] **Step 3: SchemaForm**

`src/CustomNotch.App/Settings/SchemaForm.cs` :
```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Le formulaire d'une source, construit depuis son schéma : une rangée par champ, groupées par section. Un
/// champ « secret » n'affiche jamais sa valeur ; il dit « défini » et sait s'effacer. La page décide où chaque
/// changement va (params ou secrets.json) : le formulaire ne fait que rappeler.</summary>
public static class SchemaForm
{
    public enum Kind { Text, Number, Check, Choice, Path, Url, Secret }

    public static Kind ControlKind(SchemaField field) => field.Type switch
    {
        "number" => Kind.Number, "bool" => Kind.Check, "choice" => Kind.Choice, "path" => Kind.Path, "url" => Kind.Url, "secret" => Kind.Secret, _ => Kind.Text,
    };

    public static string InitialText(SchemaField field, JsonObject? parameters)
    {
        var node = parameters?[field.Name];
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s.StartsWith("${secret:", StringComparison.Ordinal) ? "" : s;
            if (v.TryGetValue<double>(out var d)) return d.ToString(CultureInfo.InvariantCulture);
            if (v.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        }
        return field.Default ?? "";
    }

    public static bool IsSecretSet(SchemaField field, JsonObject? parameters)
        => parameters?[field.Name] is JsonValue v && v.TryGetValue<string>(out var s) && s.StartsWith("${secret:", StringComparison.Ordinal);

    public static double? ParseNumber(string text)
        => double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static UIElement Build(SourceSchema schema, JsonObject? parameters, Action<string, JsonNode?> onChange, Action<string, string?> onSecret)
    {
        var stack = new StackPanel();
        if (schema.Fields.Count == 0)
        {
            stack.Children.Add(Ui.Text("Cette source n'a pas de paramètre.", 12, null, "Muted"));
            return stack;
        }
        foreach (var group in schema.Fields.GroupBy(f => f.Group ?? ""))
        {
            if (group.Key.Length > 0)
            {
                var title = Ui.Text(group.Key.ToUpperInvariant(), 11, FontWeights.SemiBold, "Muted");
                title.Margin = new Thickness(0, 10, 0, 2);
                stack.Children.Add(title);
            }
            foreach (var field in group)
            {
                var label = field.Label + (field.Required ? " *" : "");
                var row = Ui.FormRow(Ui.FormLabel(label), Control(field, parameters, onChange, onSecret), 200);
                if (field.Help is { } help)
                {
                    var wrap = new StackPanel();
                    wrap.Children.Add(row);
                    var h = Ui.Text(help, 11, null, "Muted"); h.Margin = new Thickness(200, -4, 0, 6); h.TextWrapping = TextWrapping.Wrap;
                    wrap.Children.Add(h);
                    stack.Children.Add(wrap);
                }
                else stack.Children.Add(row);
            }
        }
        return stack;
    }

    private static UIElement Control(SchemaField field, JsonObject? parameters, Action<string, JsonNode?> onChange, Action<string, string?> onSecret)
    {
        switch (ControlKind(field))
        {
            case Kind.Check:
                var check = new CheckBox { IsChecked = InitialText(field, parameters) == "true", VerticalAlignment = VerticalAlignment.Center };
                check.Checked += (_, _) => onChange(field.Name, JsonValue.Create(true));
                check.Unchecked += (_, _) => onChange(field.Name, JsonValue.Create(false));
                return check;
            case Kind.Choice:
                var combo = new ComboBox { MinWidth = 200, HorizontalAlignment = HorizontalAlignment.Left };
                var choices = field.Choices ?? Array.Empty<string>();
                foreach (var c in choices) combo.Items.Add(c);
                combo.SelectedIndex = Math.Max(0, choices.ToList().IndexOf(InitialText(field, parameters)));
                combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string v) onChange(field.Name, JsonValue.Create(v)); };
                return combo;
            case Kind.Secret:
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                var pass = new PasswordBox { Width = 260 };
                var state = Ui.Text(IsSecretSet(field, parameters) ? "défini" : "non défini", 11, null, "Muted");
                state.Margin = new Thickness(10, 0, 10, 0); state.VerticalAlignment = VerticalAlignment.Center;
                var save = new Button { Content = "Enregistrer" }; save.SetResourceReference(FrameworkElement.StyleProperty, "Secondary");
                save.Click += (_, _) => { if (pass.Password.Length == 0) return; onSecret(field.Name, pass.Password); pass.Clear(); state.Text = "défini"; };
                var clear = new Button { Content = "Effacer", Margin = new Thickness(6, 0, 0, 0) }; clear.SetResourceReference(FrameworkElement.StyleProperty, "GhostButton");
                clear.Click += (_, _) => { onSecret(field.Name, null); state.Text = "non défini"; };
                panel.Children.Add(pass); panel.Children.Add(state); panel.Children.Add(save); panel.Children.Add(clear);
                return panel;
            case Kind.Path:
                var pathPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var pathBox = Text(field, parameters, s => onChange(field.Name, s.Length == 0 ? null : JsonValue.Create(s)));
                var browse = new Button { Content = "…", Margin = new Thickness(6, 0, 0, 0), MinWidth = 32 }; browse.SetResourceReference(FrameworkElement.StyleProperty, "Secondary");
                browse.Click += (_, _) => { var d = new Microsoft.Win32.OpenFileDialog { CheckFileExists = false }; if (d.ShowDialog() == true) pathBox.Text = d.FileName; };
                pathPanel.Children.Add(pathBox); pathPanel.Children.Add(browse);
                return pathPanel;
            case Kind.Number:
                return Text(field, parameters, s =>
                {
                    if (s.Trim().Length == 0) { onChange(field.Name, null); return; }
                    if (ParseNumber(s) is { } n) onChange(field.Name, JsonValue.Create(n));
                });
            default:
                return Text(field, parameters, s => onChange(field.Name, s.Length == 0 ? null : JsonValue.Create(s)));
        }
    }

    /// <summary>Un champ texte à anti-rebond (300 ms) ; rouge tant qu'un nombre attendu n'en est pas un.</summary>
    private static TextBox Text(SchemaField field, JsonObject? parameters, Action<string> commit)
    {
        var box = new TextBox { Text = InitialText(field, parameters), Width = 340, HorizontalAlignment = HorizontalAlignment.Left };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var last = box.Text;
        void Flush()
        {
            timer.Stop();
            if (box.Text == last) return;
            last = box.Text;
            if (ControlKind(field) == Kind.Number && box.Text.Trim().Length > 0 && ParseNumber(box.Text) is null) { box.SetResourceReference(Control.BorderBrushProperty, "Danger"); return; }
            box.SetResourceReference(Control.BorderBrushProperty, "Border");
            commit(box.Text);
        }
        timer.Tick += (_, _) => Flush();
        box.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        box.LostFocus += (_, _) => Flush();
        return box;
    }
}
```

- [ ] **Step 4: Tests verts, build**

Run: `dotnet build CustomNotch.sln` ; `dotnet test tests/CustomNotch.App.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(app): SchemaForm — le formulaire d'une source généré depuis son schéma"
```

---

### Task 7: Page « Pilules & cellules » — arbre, barre d'outils, éditeur de pilule, catalogue

**Files:**
- Create: `src/CustomNotch.App/Settings/SourceCatalogDialog.cs`, `Settings/PillEditor.cs`, `Settings/EditorBanner.cs`
- Replace: `src/CustomNotch.App/Settings/PillsPage.cs`
- Create: `src/CustomNotch.App/Settings/CellEditor.cs` (provisoire : affiche l'id ; remplacé en tâche 8)

**Interfaces:**
- Consumes: `SettingsContext`, `PageBase` (`Section`, `Card`, `Row`, `Combo`, `Debounced`, `Btn`, `OnStoreChanged`), `ConfigEditor`, `CellsFile`/`PillConfig`/`CellConfig`, `GlyphLibrary.Get`, `Glyphs.Stroke/Fill`, `ConfigException`.
- Produces: `SourceCatalogDialog.Pick(Window owner, SourceRegistry registry) → string?` (type choisi ou null), `PillEditor(SettingsContext, PillConfig, EditorBanner) : UserControl`, `EditorBanner : Border` (`Show(string message)`, `Clear()`), `CellEditor(SettingsContext, CellConfig, EditorBanner) : UserControl`, `PillsPage.Select(kind, id)`, `PillsPage.Try(Action)` (exécute une opération de l'éditeur et affiche `ConfigException` dans le bandeau).

- [ ] **Step 1: Bandeau d'erreur et catalogue**

`src/CustomNotch.App/Settings/EditorBanner.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;

namespace CustomNotch.App.Settings;

/// <summary>Le message d'une configuration refusée, en tête de l'éditeur : rouge, effacé à la prochaine opération réussie.
/// La valeur fautive reste dans son champ pour être corrigée.</summary>
public sealed class EditorBanner : Border
{
    private readonly TextBlock _text = Ui.Text("", 12, FontWeights.SemiBold, "Danger");

    public EditorBanner()
    {
        Child = _text;
        _text.TextWrapping = TextWrapping.Wrap;
        Padding = new Thickness(12, 8, 12, 8);
        CornerRadius = new CornerRadius(8);
        Margin = new Thickness(0, 0, 0, 12);
        BorderThickness = new Thickness(1);
        SetResourceReference(BorderBrushProperty, "Danger");
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(28, 240, 101, 94));
        Visibility = Visibility.Collapsed;
    }

    public void Show(string message) { _text.Text = message; Visibility = Visibility.Visible; }
    public void Clear() => Visibility = Visibility.Collapsed;
}
```

`src/CustomNotch.App/Settings/SourceCatalogDialog.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Le catalogue des sources : titre et description, filtrable. Rend le type choisi, ou null.</summary>
public sealed class SourceCatalogDialog : Window
{
    private readonly ListBox _list = new();
    private readonly List<SourceSchema> _all;
    private string? _result;

    private SourceCatalogDialog(Window owner, SourceRegistry registry)
    {
        Owner = owner;
        Title = "Ajouter une cellule";
        Width = 520; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "Bg");
        FontFamily = (System.Windows.Media.FontFamily)FindResource("UiFont");
        _all = registry.Schemas.Values.OrderBy(s => s.Title).ToList();

        var filter = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        filter.TextChanged += (_, _) => Fill(filter.Text);
        _list.MouseDoubleClick += (_, _) => Accept();
        var ok = new Button { Content = "Ajouter", MinWidth = 100, Margin = new Thickness(8, 0, 0, 0), IsDefault = true }; ok.SetResourceReference(StyleProperty, "Primary"); ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Annuler", MinWidth = 100, IsCancel = true }; cancel.SetResourceReference(StyleProperty, "Secondary");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(filter);
        Grid.SetRow(_list, 1); root.Children.Add(_list);
        Grid.SetRow(buttons, 2); root.Children.Add(buttons);
        Content = root;
        SourceInitialized += (_, _) => Theme.ApplyChrome(this);
        Fill("");
        Loaded += (_, _) => filter.Focus();
    }

    private void Fill(string filter)
    {
        _list.Items.Clear();
        foreach (var s in _all.Where(s => filter.Length == 0 || s.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || (s.Description ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase) || s.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new StackPanel { Margin = new Thickness(4) };
            row.Children.Add(Ui.Text(s.Title, 13, FontWeights.SemiBold));
            var d = Ui.Text(s.Description ?? s.Type, 11, null, "Muted"); d.TextWrapping = TextWrapping.Wrap;
            row.Children.Add(d);
            _list.Items.Add(new ListBoxItem { Content = row, Tag = s.Type });
        }
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private void Accept()
    {
        if (_list.SelectedItem is not ListBoxItem it) return;
        _result = (string)it.Tag;
        DialogResult = true;
    }

    public static string? Pick(Window owner, SourceRegistry registry)
    {
        var dialog = new SourceCatalogDialog(owner, registry);
        return dialog.ShowDialog() == true ? dialog._result : null;
    }
}
```

- [ ] **Step 2: Éditeur de pilule**

`src/CustomNotch.App/Settings/PillEditor.cs` :
```csharp
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Une pilule : bord, écran, position, échelle, visible. Bord et échelle vont dans le fichier partagé ; écran,
/// position et visibilité sont des choix de poste (surcharge locale), comme un drag.</summary>
public sealed class PillEditor : UserControl
{
    private static readonly (string Value, string Label)[] Edges = { ("right", "Droite"), ("left", "Gauche"), ("top", "Haut"), ("bottom", "Bas") };

    public PillEditor(SettingsContext ctx, PillConfig pill, Action<Action> run)
    {
        var body = new StackPanel();
        body.Children.Add(Ui.Text($"Pilule « {pill.Id} »", 17, FontWeights.SemiBold));

        var edges = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in Edges)
        {
            var radio = new RadioButton { Content = label, GroupName = "edge-" + pill.Id, IsChecked = pill.Edge == value, Margin = new Thickness(0, 0, 14, 0) };
            radio.Checked += (_, _) => run(() => ctx.Editor.SetPillShared(pill.Id, p => p["edge"] = value));
            edges.Children.Add(radio);
        }

        var screens = new List<(string, string)> { ("", "Écran principal") };
        foreach (var s in System.Windows.Forms.Screen.AllScreens) screens.Add((s.DeviceName, $"{s.DeviceName.TrimStart('\\', '.')} — {s.Bounds.Width}×{s.Bounds.Height}{(s.Primary ? " (principal)" : "")}"));
        var screen = new ComboBox { MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (v, l) in screens) screen.Items.Add(new ComboBoxItem { Content = l, Tag = v });
        screen.SelectedIndex = Math.Max(0, screens.FindIndex(s => s.Item1 == (pill.Screen ?? "")));
        screen.SelectionChanged += (_, _) => { if (screen.SelectedItem is ComboBoxItem it) run(() => ctx.Editor.SetPillLocal(pill.Id, p => { if ((string)it.Tag is { Length: > 0 } v) p["screen"] = v; else p.Remove("screen"); })); };

        var along = Slider(pill.Along, 0, 1, 0.01, v => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["along"] = Math.Round(v, 3))));
        var scale = Slider(pill.Scale, 0.5, 2, 0.1, v => run(() => ctx.Editor.SetPillShared(pill.Id, p => p["scale"] = Math.Round(v, 1))));
        var visible = new CheckBox { Content = "Visible sur ce poste", IsChecked = pill.Visible };
        visible.Checked += (_, _) => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["visible"] = true));
        visible.Unchecked += (_, _) => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["visible"] = false));

        var form = new StackPanel();
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Bord de l'écran"), edges, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Écran"), screen, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Position le long du bord"), along, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Échelle"), scale, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel(""), visible, 200));
        var card = new Border { Child = form, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), BorderThickness = new Thickness(1), Margin = new Thickness(0, 12, 0, 0) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        body.Children.Add(card);
        Content = body;
    }

    /// <summary>Un curseur qui n'écrit qu'au relâchement (ou 300 ms après un changement au clavier) : pas une écriture par pixel.</summary>
    private static UIElement Slider(double value, double min, double max, double step, Action<double> commit)
    {
        var panel = new DockPanel { Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var slider = new System.Windows.Controls.Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        var label = Ui.Text(Fmt(value), 12, null, "Muted"); label.Width = 56; label.Margin = new Thickness(10, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(label, Dock.Right);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var last = value;
        void Flush() { timer.Stop(); if (Math.Abs(slider.Value - last) > 1e-9) { last = slider.Value; commit(slider.Value); } }
        timer.Tick += (_, _) => Flush();
        slider.ValueChanged += (_, e) => { label.Text = Fmt(e.NewValue); timer.Stop(); timer.Start(); };
        panel.Children.Add(label);
        panel.Children.Add(slider);
        return panel;

        static string Fmt(double v) => v <= 1 ? $"{Math.Round(v * 100)} %" : $"×{v:0.0}";
    }
}
```
Note : pour l'échelle, `Fmt` affiche « ×1,0 » au-dessus de 1 et un pourcentage en dessous — un curseur d'échelle 0,5 affiche « 50 % » : acceptable mais trompeur ; faire de `Fmt` un paramètre (`Func<double, string>`) : position → `$"{Math.Round(v * 100)} %"`, échelle → `$"×{v:0.0}"`.

- [ ] **Step 3: La page**

`src/CustomNotch.App/Settings/CellEditor.cs` (provisoire) :
```csharp
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

public sealed class CellEditor : UserControl
{
    public CellEditor(SettingsContext ctx, CellConfig cell, Action<Action> run) => Content = Ui.Text($"Cellule « {cell.Id} » — éditeur à la tâche 8", 14);
}
```

`src/CustomNotch.App/Settings/PillsPage.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Maître-détail : à gauche l'arbre pilule → cellules (une liste indentée), à droite l'éditeur de l'élément
/// choisi. Toute opération passe par ConfigEditor ; une configuration refusée s'affiche dans le bandeau et rien n'est
/// appliqué. La sélection survit au rafraîchissement.</summary>
public sealed class PillsPage : PageBase
{
    private readonly ListBox _tree = new();
    private readonly ContentControl _editor = new();
    private readonly EditorBanner _banner = new();
    private readonly Button _hide;
    private (string Kind, string Id)? _selected;

    public PillsPage(SettingsContext ctx) : base(ctx, "Pilules & cellules", "Chaque pilule est ancrée à un bord ; ses cellules lisent une source. Tout s'applique immédiatement.")
    {
        _tree.SetResourceReference(StyleProperty, "TreeList");
        _tree.SelectionChanged += (_, _) => { if (_tree.SelectedItem is ListBoxItem it && it.Tag is (string kind, string id)) { _selected = (kind, id); ShowEditor(); } };

        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        tools.Children.Add(Btn("+ Pilule", () => Try(() => Select("pill", Ctx.Editor.AddPill("right")))));
        tools.Children.Add(Btn("+ Cellule", AddCell, "Primary"));
        tools.Children.Add(Btn("↑", () => Move(-1)));
        tools.Children.Add(Btn("↓", () => Move(+1)));
        _hide = Btn("Masquer", ToggleVisible);
        tools.Children.Add(_hide);
        tools.Children.Add(Btn("Supprimer", Remove, "GhostButton"));

        var left = new DockPanel { Width = 300, Margin = new Thickness(0, 0, 20, 0) };
        DockPanel.SetDock(tools, Dock.Top);
        left.Children.Add(tools);
        left.Children.Add(Card(_tree));

        var right = new StackPanel();
        right.Children.Add(_banner);
        right.Children.Add(_editor);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Body.Children.Add(grid);
        OnStoreChanged(Refresh);
    }

    /// <summary>Exécute une opération de l'éditeur ; un refus s'affiche, une réussite efface le bandeau.</summary>
    public void Try(Action op)
    {
        try { op(); _banner.Clear(); }
        catch (ConfigException ex) { _banner.Show(ex.Message); }
    }

    public void Select(string kind, string id)
    {
        _selected = (kind, id);
        Refresh();
    }

    public override void Refresh()
    {
        var file = Ctx.Store.Current;
        _tree.Items.Clear();
        var groups = file.AllCells().Where(c => c.IsGroup).ToList();
        var childIds = groups.SelectMany(g => g.Children!).ToHashSet();
        foreach (var pill in file.Pills)
        {
            _tree.Items.Add(Item("pill", pill.Id, $"Pilule « {pill.Id} »  ·  {Edge(pill.Edge)}", 0, pill.Visible, null, true));
            foreach (var cell in pill.Cells.Where(c => !childIds.Contains(c.Id)))
            {
                _tree.Items.Add(Item("cell", cell.Id, cell.Label ?? cell.Id, 1, cell.Visible, cell.Glyph, false));
                if (cell.IsGroup)
                    foreach (var childId in cell.Children!)
                        if (file.Cell(childId) is { } child)
                            _tree.Items.Add(Item("cell", child.Id, child.Label ?? child.Id, 2, child.Visible, child.Glyph, false));
            }
        }
        if (_selected is { } sel)
        {
            var match = _tree.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is (string k, string id) && k == sel.Kind && id == sel.Id);
            if (match is not null) _tree.SelectedItem = match; else _selected = null;
        }
        if (_selected is null && _tree.Items.Count > 0) _tree.SelectedIndex = 0;
        ShowEditor();
    }

    private static string Edge(string edge) => edge switch { "left" => "gauche", "top" => "haut", "bottom" => "bas", _ => "droite" };

    private static ListBoxItem Item(string kind, string id, string text, int depth, bool visible, string? glyph, bool bold)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(depth * 18, 0, 0, 0) };
        if (glyph is not null)
        {
            var (data, filled) = Cells.GlyphLibrary.Get(glyph);
            var brush = new System.Windows.Media.SolidColorBrush(Cells.StatusPalette.Ink);
            var icon = filled ? Glyphs.Fill(data, 14, brush) : Glyphs.Stroke(data, 14, brush, 1.5);
            icon.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(icon);
        }
        var label = Ui.Text(text, 13, bold ? FontWeights.SemiBold : FontWeights.Normal, visible ? "Fg" : "Muted");
        row.Children.Add(label);
        return new ListBoxItem { Content = row, Tag = (kind, id), ToolTip = visible ? null : "masquée sur ce poste" };
    }

    private void ShowEditor()
    {
        var file = Ctx.Store.Current;
        if (_selected is not { } sel) { _editor.Content = Ui.Text("Choisis une pilule ou une cellule.", 13, null, "Muted"); return; }
        if (sel.Kind == "pill")
        {
            var pill = file.Pills.FirstOrDefault(p => p.Id == sel.Id);
            _editor.Content = pill is null ? null : new PillEditor(Ctx, pill, Try);
            _hide.Content = pill?.Visible == false ? "Afficher" : "Masquer";
        }
        else
        {
            var cell = file.Cell(sel.Id);
            _editor.Content = cell is null ? null : new CellEditor(Ctx, cell, Try);
            _hide.Content = cell?.Visible == false ? "Afficher" : "Masquer";
        }
    }

    private string? PillOfSelection()
    {
        if (_selected is not { } sel) return null;
        if (sel.Kind == "pill") return sel.Id;
        return Ctx.Store.Current.Pills.FirstOrDefault(p => p.Cells.Any(c => c.Id == sel.Id))?.Id;
    }

    private void AddCell()
    {
        var pillId = PillOfSelection() ?? Ctx.Store.Current.Pills.FirstOrDefault()?.Id;
        if (pillId is null) { _banner.Show("Ajoute d'abord une pilule."); return; }
        var type = SourceCatalogDialog.Pick(Window.GetWindow(this)!, Ctx.Registry);
        if (type is null) return;
        Try(() => Select("cell", Ctx.Editor.AddCell(pillId, type)));
    }

    private void Move(int delta)
    {
        if (_selected is not { } sel) return;
        Try(() => { if (sel.Kind == "pill") Ctx.Editor.MovePill(sel.Id, delta); else Ctx.Editor.MoveCell(sel.Id, delta); });
    }

    private void ToggleVisible()
    {
        if (_selected is not { } sel) return;
        var file = Ctx.Store.Current;
        Try(() =>
        {
            if (sel.Kind == "pill") { var v = file.Pills.First(p => p.Id == sel.Id).Visible; Ctx.Editor.SetPillLocal(sel.Id, p => p["visible"] = !v); }
            else Ctx.Editor.SetCellVisible(sel.Id, !file.Cell(sel.Id)!.Visible);
        });
    }

    private void Remove()
    {
        if (_selected is not { } sel) return;
        var what = sel.Kind == "pill" ? $"la pilule « {sel.Id} » et ses cellules" : $"la cellule « {sel.Id} »";
        if (MessageBox.Show(Window.GetWindow(this), $"Supprimer {what} ?", "customNotch", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Try(() => { if (sel.Kind == "pill") Ctx.Editor.RemovePill(sel.Id); else Ctx.Editor.RemoveCell(sel.Id); _selected = null; });
    }
}
```
`Cells.StatusPalette.Ink` est une `Color` publique (tâche 10 du socle) ; `Glyphs.Fill/Stroke` sont `internal static` dans `Shared/Glyphs.cs` (même assembly).

- [ ] **Step 4: Build, vérification à l'écran**

Run: `dotnet build CustomNotch.sln` (0 avertissement), `dotnet test CustomNotch.sln`.
Écran (si libre) : Réglages → l'arbre montre `Pilule « main » · droite` puis `Système` avec ses cinq enfants indentés et grisés, `Média`, `ClickUp`. Choisir la pilule : bord/écran/position/échelle ; passer le bord à « Haut » → la pilule bouge tout de suite ; remettre « Droite ». « + Pilule » crée `pill-2` à droite (deux pilules superposées : la déplacer à gauche par le bord). « + Cellule » sur `pill-2` → catalogue → « Processeur » → `cpu-2` apparaît dans l'arbre et dans la pilule. ↑↓ réordonnent (la pilule suit). Masquer/Afficher grise l'entrée et retire/remet la cellule. Supprimer `pill-2` (confirmation) → disparaît. Provoquer un refus : impossible depuis cette page tant que l'éditeur de cellule n'existe pas — vérifié en tâche 8. Tuer l'app.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "feat(app): page Pilules & cellules — arbre, barre d'outils, éditeur de pilule, catalogue"
```

---

### Task 8: L'éditeur de cellule — Source, Affichage, Actions, Groupe

**Files:**
- Create: `src/CustomNotch.App/Settings/GlyphGallery.cs`
- Modify: `src/CustomNotch.App/Cells/GlyphLibrary.cs` (`Names`)
- Replace: `src/CustomNotch.App/Settings/CellEditor.cs`

**Interfaces:**
- Consumes: `SchemaForm.Build`, `SourceCatalogDialog.Pick`, `ConfigEditor` (`SetCell`, `SetSecret`, `RemoveSecret`, `SecretName`, `SecretPlaceholder`, `SetCellVisible`), `PageBase` helpers via des copies locales (l'éditeur n'est pas une page), `GlyphLibrary.Names/Get`, `CellConfig`, `Thresholds`.
- Produces: `GlyphLibrary.Names : IEnumerable<string>`, `GlyphGallery.Build(string? current, Action<string?> onPick) → UIElement`, `CellEditor(SettingsContext, CellConfig, Action<Action> run)`.

- [ ] **Step 1: Galerie de glyphes**

Dans `src/CustomNotch.App/Cells/GlyphLibrary.cs`, ajouter : `public static IEnumerable<string> Names => Named.Keys.Where(k => k != "dot").OrderBy(k => k);`.

`src/CustomNotch.App/Settings/GlyphGallery.cs` :
```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using CustomNotch.App.Cells;

namespace CustomNotch.App.Settings;

/// <summary>Les glyphes nommés en grille (le tracé rendu, le nom en infobulle), et un champ pour coller un tracé SVG.</summary>
public static class GlyphGallery
{
    public static UIElement Build(string? current, Action<string?> onPick)
    {
        var stack = new StackPanel();
        var grid = new WrapPanel { MaxWidth = 460 };
        var buttons = new List<ToggleButton>();
        foreach (var name in GlyphLibrary.Names)
        {
            var (data, filled) = GlyphLibrary.Get(name);
            var brush = new System.Windows.Media.SolidColorBrush(StatusPalette.Ink);
            var button = new ToggleButton
            {
                Width = 40, Height = 40, Margin = new Thickness(0, 0, 4, 4), ToolTip = name, Tag = name,
                Content = filled ? Glyphs.Fill(data, 20, brush) : Glyphs.Stroke(data, 20, brush, 1.6),
                IsChecked = string.Equals(name, current, StringComparison.OrdinalIgnoreCase),
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "GlyphButton");
            button.Checked += (_, _) => { foreach (var other in buttons) if (other != button) other.IsChecked = false; onPick(name); };
            button.Unchecked += (_, _) => { if (buttons.All(b => b.IsChecked != true)) button.IsChecked = true; };
            buttons.Add(button);
            grid.Children.Add(button);
        }
        stack.Children.Add(grid);
        var raw = new TextBox { Width = 460, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0), Text = current is { } c && c.StartsWith('M') ? c : "" };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => { timer.Stop(); if (raw.Text.Trim() is { Length: > 0 } t && t.StartsWith('M')) { foreach (var b in buttons) b.IsChecked = false; onPick(t); } };
        raw.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        var hint = Ui.Text("Ou un tracé SVG (commence par M) — grille 16×16 pour un trait, 24×24 pour un plein.", 11, null, "Muted");
        stack.Children.Add(raw);
        stack.Children.Add(hint);
        return stack;
    }
}
```
Style `GlyphButton` à ajouter dans `Styles.xaml` :
```xml
    <Style x:Key="GlyphButton" TargetType="ToggleButton">
        <Setter Property="Background" Value="{DynamicResource Surface}" />
        <Setter Property="BorderBrush" Value="{DynamicResource Border}" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ToggleButton">
                    <Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="8">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Background" Value="{DynamicResource SurfaceHover}" /></Trigger>
                        <Trigger Property="IsChecked" Value="True"><Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource Accent}" /><Setter TargetName="Bd" Property="Background" Value="{DynamicResource AccentSoft}" /></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
```

- [ ] **Step 2: L'éditeur de cellule**

`src/CustomNotch.App/Settings/CellEditor.cs` :
```csharp
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Une cellule en quatre sections : Source (le formulaire de son schéma), Affichage, Actions, Groupe. Chaque
/// contrôle écrit tout de suite par ConfigEditor via `run` (qui affiche un refus dans le bandeau de la page).</summary>
public sealed class CellEditor : UserControl
{
    private static readonly (string, string)[] Kinds = { ("", "Automatique"), ("ring", "Anneau"), ("value", "Valeur"), ("status", "Statut"), ("sparkline", "Sparkline") };
    private static readonly (string, string)[] Refreshes = { ("", "Par défaut"), ("1s", "1 s"), ("2s", "2 s"), ("5s", "5 s"), ("30s", "30 s"), ("1m", "1 min"), ("5m", "5 min"), ("1h", "1 h") };
    private static readonly (string, string)[] ClickKinds = { ("", "Ouvrir la carte"), ("default", "Action par défaut de la source"), ("open", "Ouvrir une URL, un fichier, une app"), ("shell", "Lancer une commande"), ("source", "Une action de la source") };
    private static readonly (string, string)[] ActionKinds = { ("open", "Ouvrir…"), ("shell", "Commande…"), ("source", "Action de la source") };

    private readonly SettingsContext _ctx;
    private readonly CellConfig _cell;
    private readonly Action<Action> _run;

    public CellEditor(SettingsContext ctx, CellConfig cell, Action<Action> run)
    {
        _ctx = ctx; _cell = cell; _run = run;
        var body = new StackPanel();
        body.Children.Add(Ui.Text($"{(cell.IsGroup ? "Groupe" : "Cellule")} « {cell.Id} »", 17, FontWeights.SemiBold));
        body.Children.Add(Section("Source"));
        body.Children.Add(Card(SourceSection()));
        body.Children.Add(Section("Affichage"));
        body.Children.Add(Card(DisplaySection()));
        body.Children.Add(Section("Actions"));
        body.Children.Add(Card(ActionsSection()));
        body.Children.Add(Section("Groupe"));
        body.Children.Add(Card(GroupSection()));
        Content = body;
    }

    private SourceSchema? Schema => _cell.IsGroup ? null : _ctx.Registry.Get(_cell.Source)?.Schema;

    // ---- Source ---------------------------------------------------------------------------------------------

    private UIElement SourceSection()
    {
        var stack = new StackPanel();
        if (_cell.IsGroup)
        {
            stack.Children.Add(Ui.Text("Un groupe n'a pas de source : son anneau et sa carte viennent de ses enfants.", 12, null, "Muted"));
            return stack;
        }
        var schema = Schema;
        var head = new DockPanel();
        var change = Btn("Changer…", () =>
        {
            var type = SourceCatalogDialog.Pick(Window.GetWindow(this)!, _ctx.Registry);
            if (type is null || type == _cell.Source) return;
            var target = _ctx.Registry.Get(type)!.Schema;
            _run(() => _ctx.Editor.SetCell(_cell.Id, c =>
            {
                c["source"] = type;
                c.Remove("params");
                var defaults = new JsonObject();
                foreach (var f in target.Fields.Where(f => f.Default is not null))
                    defaults[f.Name] = f.Type switch { "number" => JsonValue.Create(double.Parse(f.Default!, CultureInfo.InvariantCulture)), "bool" => JsonValue.Create(bool.Parse(f.Default!)), _ => JsonValue.Create(f.Default!) };
                if (defaults.Count > 0) c["params"] = defaults;
                if (target.DefaultGlyph is { } g) c["glyph"] = g;
            }));
        });
        DockPanel.SetDock(change, Dock.Right);
        head.Children.Add(change);
        var title = new StackPanel();
        title.Children.Add(Ui.Text(schema?.Title ?? _cell.Source, 13, FontWeights.SemiBold));
        var desc = Ui.Text(schema?.Description ?? "source inconnue", 11, null, "Muted"); desc.TextWrapping = TextWrapping.Wrap;
        title.Children.Add(desc);
        head.Children.Add(title);
        stack.Children.Add(head);
        if (schema is null) return stack;
        var form = SchemaForm.Build(schema, _cell.Params,
            (name, node) => _run(() => _ctx.Editor.SetCell(_cell.Id, c =>
            {
                if (c["params"] is not JsonObject p) c["params"] = p = new JsonObject();
                if (node is null) p.Remove(name); else p[name] = node.DeepClone();
            })),
            (name, value) => _run(() =>
            {
                if (value is null)
                {
                    _ctx.Editor.RemoveSecret(ConfigEditor.SecretName(_cell.Id, name));
                    _ctx.Editor.SetCell(_cell.Id, c => (c["params"] as JsonObject)?.Remove(name));
                }
                else
                {
                    _ctx.Editor.SetSecret(ConfigEditor.SecretName(_cell.Id, name), value);
                    _ctx.Editor.SetCell(_cell.Id, c =>
                    {
                        if (c["params"] is not JsonObject p) c["params"] = p = new JsonObject();
                        p[name] = ConfigEditor.SecretPlaceholder(_cell.Id, name);
                    });
                }
            }));
        ((FrameworkElement)form).Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(form);
        return stack;
    }

    // ---- Affichage ------------------------------------------------------------------------------------------

    private UIElement DisplaySection()
    {
        var stack = new StackPanel();
        stack.Children.Add(Row("Libellé", Debounced(_cell.Label ?? "", v => Set(c => { if (v.Length == 0) c.Remove("label"); else c["label"] = v; }))));
        stack.Children.Add(Row("Glyph", GlyphGallery.Build(_cell.Glyph, g => Set(c => { if (g is null) c.Remove("glyph"); else c["glyph"] = g; }))));
        if (!_cell.IsGroup)
        {
            stack.Children.Add(Row("Type de rendu", Combo(Kinds, _cell.Kind ?? "", v => Set(c => { if (v.Length == 0) c.Remove("kind"); else c["kind"] = v; }))));
            var refresh = new StackPanel { Orientation = Orientation.Horizontal };
            var known = Refreshes.Any(r => r.Item1 == (_cell.Refresh ?? ""));
            var combo = Combo(Refreshes, known ? _cell.Refresh ?? "" : "", v => Set(c => { if (v.Length == 0) c.Remove("refresh"); else c["refresh"] = v; }));
            var custom = Debounced(known ? "" : _cell.Refresh ?? "", v => Set(c => { if (v.Trim().Length == 0) c.Remove("refresh"); else c["refresh"] = v.Trim(); }), 100);
            custom.Margin = new Thickness(8, 0, 0, 0);
            refresh.Children.Add(combo); refresh.Children.Add(custom); refresh.Children.Add(Muted("  ou « 90s », « 2h »"));
            stack.Children.Add(Row("Cadence de lecture", refresh));
        }
        var thresholds = new StackPanel { Orientation = Orientation.Horizontal };
        var warn = Debounced(_cell.Thresholds?.Warn?.ToString(CultureInfo.InvariantCulture) ?? "", _ => CommitThresholds(), 90);
        var crit = Debounced(_cell.Thresholds?.Crit?.ToString(CultureInfo.InvariantCulture) ?? "", _ => CommitThresholds(), 90);
        var invert = new CheckBox { Content = "inversé (bas = mauvais)", IsChecked = _cell.Thresholds?.Invert ?? false, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        invert.Checked += (_, _) => CommitThresholds(); invert.Unchecked += (_, _) => CommitThresholds();
        thresholds.Children.Add(Muted("avertir à ")); thresholds.Children.Add(warn); thresholds.Children.Add(Muted("  critique à ")); thresholds.Children.Add(crit); thresholds.Children.Add(invert);
        stack.Children.Add(Row("Seuils (% ou valeur)", thresholds));
        return stack;

        void CommitThresholds()
        {
            var w = SchemaForm.ParseNumber(warn.Text);
            var k = SchemaForm.ParseNumber(crit.Text);
            Set(c =>
            {
                if (w is null && k is null && invert.IsChecked != true) { c.Remove("thresholds"); return; }
                var t = new JsonObject();
                if (w is { } wv) t["warn"] = wv;
                if (k is { } kv) t["crit"] = kv;
                if (invert.IsChecked == true) t["invert"] = true;
                c["thresholds"] = t;
            });
        }
    }

    // ---- Actions --------------------------------------------------------------------------------------------

    private UIElement ActionsSection()
    {
        var stack = new StackPanel();
        var click = _cell.Actions?.Click;
        var current = click is null ? (Schema?.DefaultAction is not null ? "default" : "") : click.Open is not null ? "open" : click.Shell is not null ? "shell" : click.Source is not null ? "source" : "";
        var value = Debounced(click?.Open ?? click?.Shell ?? click?.Source ?? "", _ => CommitClick(), 320);
        var kind = Combo(ClickKinds, current, _ => CommitClick());
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        value.Margin = new Thickness(8, 0, 0, 0);
        panel.Children.Add(kind); panel.Children.Add(value);
        stack.Children.Add(Row("Au clic", panel));
        stack.Children.Add(Muted("« Action par défaut » : ouvrir pour un lanceur, lecture/pause pour le média."));

        var buttons = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var list = (_cell.Actions?.Card ?? new List<ActionConfig>()).ToList();
        void Render()
        {
            buttons.Children.Clear();
            for (var i = 0; i < list.Count; i++)
            {
                var index = i; var a = list[i];
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                row.Children.Add(Debounced(a.Label ?? "", v => { a.Label = v; CommitCard(); }, 140));
                var k = Combo(ActionKinds, a.Open is not null ? "open" : a.Shell is not null ? "shell" : "source", v => { var val = a.Open ?? a.Shell ?? a.Source ?? ""; a.Open = a.Shell = a.Source = null; if (v == "open") a.Open = val; else if (v == "shell") a.Shell = val; else a.Source = val; CommitCard(); });
                k.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(k);
                var v = Debounced(a.Open ?? a.Shell ?? a.Source ?? "", t => { if (a.Open is not null) a.Open = t; else if (a.Shell is not null) a.Shell = t; else a.Source = t; CommitCard(); }, 220);
                v.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(v);
                row.Children.Add(Btn("↑", () => { if (index > 0) { (list[index - 1], list[index]) = (list[index], list[index - 1]); CommitCard(); } }, "GhostButton"));
                row.Children.Add(Btn("↓", () => { if (index < list.Count - 1) { (list[index + 1], list[index]) = (list[index], list[index + 1]); CommitCard(); } }, "GhostButton"));
                row.Children.Add(Btn("✕", () => { list.RemoveAt(index); CommitCard(); }, "GhostButton"));
                buttons.Children.Add(row);
            }
            buttons.Children.Add(Btn("+ Bouton", () => { list.Add(new ActionConfig { Label = "Ouvrir", Open = "https://" }); CommitCard(); }));
        }
        Render();
        stack.Children.Add(Row("Boutons de la carte", buttons));
        return stack;

        void CommitClick()
        {
            var k = ((ComboBoxItem)kind.SelectedItem).Tag as string ?? "";
            var v = value.Text.Trim();
            Set(c =>
            {
                if (c["actions"] is not JsonObject actions) c["actions"] = actions = new JsonObject();
                switch (k)
                {
                    case "open": actions["click"] = new JsonObject { ["open"] = v }; break;
                    case "shell": actions["click"] = new JsonObject { ["shell"] = v }; break;
                    case "source": actions["click"] = new JsonObject { ["source"] = v }; break;
                    default: actions.Remove("click"); break;   // carte, ou action par défaut de la source (= pas de click)
                }
                if (actions.Count == 0) c.Remove("actions");
            });
        }

        void CommitCard()
        {
            Set(c =>
            {
                if (c["actions"] is not JsonObject actions) c["actions"] = actions = new JsonObject();
                if (list.Count == 0) actions.Remove("card");
                else actions["card"] = new JsonArray(list.Select(a =>
                {
                    var o = new JsonObject();
                    if (a.Label is { Length: > 0 } l) o["label"] = l;
                    if (a.Open is not null) o["open"] = a.Open; else if (a.Shell is not null) o["shell"] = a.Shell; else if (a.Source is not null) o["source"] = a.Source;
                    return (JsonNode)o;
                }).ToArray());
                if (actions.Count == 0) c.Remove("actions");
            });
            Render();
        }
    }

    // ---- Groupe ---------------------------------------------------------------------------------------------

    private UIElement GroupSection()
    {
        var stack = new StackPanel();
        var pill = _ctx.Store.Current.Pills.FirstOrDefault(p => p.Cells.Any(c => c.Id == _cell.Id));
        var candidates = (pill?.Cells ?? new List<CellConfig>()).Where(c => c.Id != _cell.Id).ToList();
        if (candidates.Count == 0) { stack.Children.Add(Muted("Aucune autre cellule dans cette pilule.")); return stack; }
        var children = (_cell.Children ?? new List<string>()).ToList();
        var boxes = new WrapPanel { MaxWidth = 460 };
        foreach (var c in candidates)
        {
            var box = new CheckBox { Content = c.Label ?? c.Id, IsChecked = children.Contains(c.Id), Margin = new Thickness(0, 0, 16, 4) };
            box.Checked += (_, _) =>
            {
                if (!children.Contains(c.Id)) children.Add(c.Id);
                CommitChildren();
                if (c.Visible && MessageBox.Show(Window.GetWindow(this), $"Masquer « {c.Label ?? c.Id} » de la pilule ? Il reste dans la carte du groupe.", "customNotch", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    _run(() => _ctx.Editor.SetCellVisible(c.Id, false));
            };
            box.Unchecked += (_, _) => { children.Remove(c.Id); CommitChildren(); };
            boxes.Children.Add(box);
        }
        stack.Children.Add(Row("Enfants", boxes));
        var heads = new[] { ("", "Le pire enfant") }.Concat(children.Select(id => (id, _ctx.Store.Current.Cell(id)?.Label ?? id))).ToArray();
        stack.Children.Add(Row("Tête (anneau de la pilule)", Combo(heads, _cell.Headline ?? "", v => Set(c => { if (v.Length == 0) c.Remove("headline"); else c["headline"] = v; }))));
        stack.Children.Add(Muted("Cocher au moins un enfant fait de cette cellule un groupe : sa source n'est alors plus lue."));
        return stack;

        void CommitChildren() => Set(c =>
        {
            if (children.Count == 0) { c.Remove("children"); c.Remove("headline"); }
            else
            {
                c["children"] = new JsonArray(children.Select(id => (JsonNode)id).ToArray());
                if (c["headline"]?.GetValue<string>() is { } h && !children.Contains(h)) c.Remove("headline");
            }
        });
    }

    // ---- briques (copies locales de PageBase : l'éditeur n'est pas une page) ---------------------------------

    private void Set(Action<JsonObject> mutate) => _run(() => _ctx.Editor.SetCell(_cell.Id, mutate));

    private static TextBlock Section(string text) { var t = Ui.Text(text.ToUpperInvariant(), 11, FontWeights.SemiBold, "Muted"); t.Margin = new Thickness(0, 18, 0, 6); return t; }
    private static TextBlock Muted(string text) { var t = Ui.Text(text, 11, null, "Muted"); t.TextWrapping = TextWrapping.Wrap; t.VerticalAlignment = VerticalAlignment.Center; return t; }
    private static Grid Row(string label, UIElement field) => Ui.FormRow(Ui.FormLabel(label), field, 200);

    private static Border Card(UIElement content)
    {
        var card = new Border { Child = content, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        return card;
    }

    private static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string current, Action<string> onChange)
    {
        var combo = new ComboBox { MinWidth = 200, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (value, label) in items) combo.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        combo.SelectedIndex = Math.Max(0, items.ToList().FindIndex(i => i.Value == current));
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem it && it.Tag is string v) onChange(v); };
        return combo;
    }

    private static TextBox Debounced(string initial, Action<string> onChange, double width = 360)
    {
        var box = new TextBox { Text = initial, Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var last = initial;
        void Flush() { timer.Stop(); if (box.Text != last) { last = box.Text; onChange(box.Text); } }
        timer.Tick += (_, _) => Flush();
        box.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        box.LostFocus += (_, _) => Flush();
        return box;
    }

    private static Button Btn(string text, Action click, string style = "Secondary")
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 0) };
        b.SetResourceReference(FrameworkElement.StyleProperty, style);
        b.Click += (_, _) => click();
        return b;
    }
}
```
Les briques `Section/Card/Row/Combo/Debounced/Btn` sont dupliquées depuis `PageBase` parce que `CellEditor` n'est pas une page : si la revue le demande, les extraire dans une classe `Bricks` statique partagée par les deux (`Settings/Bricks.cs`) — préférable, à faire directement si le temps le permet : `PageBase` et `CellEditor` appellent alors `Bricks.Section(...)`.

- [ ] **Step 3: Build, vérification à l'écran**

Run: `dotnet build CustomNotch.sln` (0 avertissement), `dotnet test CustomNotch.sln`.
Écran (si libre), de bout en bout :
1. `+ Cellule` → « HTTP / JSON » → `http` : le formulaire montre URL *, Méthode, Corps (section Requête), Chemin JSON, Chemin du texte, Maximum, Unité (section Lecture). Sans URL, la pilule montre la cellule grisée (lecture refusée = stale) ; taper `https://api.github.com/repos/vinzdg/codenotch` et `stargazers_count` comme chemin → un nombre apparaît dans la pilule en quelques secondes.
2. Libellé « Étoiles », glyph `chart` dans la galerie → la légende et le glyph changent tout de suite.
3. Seuils 100 / 1000 → l'anneau n'existe pas (pas de max) mais la couleur suit ; « Type de rendu = Anneau » avec Maximum 2000 → anneau.
4. Actions : « Au clic = Ouvrir » + `https://github.com/vinzdg/codenotch` → clic sur la cellule ouvre le navigateur. « + Bouton » → « Ouvrir » dans la carte.
5. Groupe : sur `sys`, cocher `http` → il rejoint la carte du groupe ; proposer de le masquer → oui → il disparaît de la pilule. Décocher → il revient.
6. Refus : sur `sys`, tenter de cocher… un cycle n'est pas constructible par l'UI (une cellule ne se coche pas elle-même) ; provoquer un refus autrement : Cadence personnalisée « vite » → bandeau rouge « refresh « vite » … », la valeur reste dans le champ, la pilule ne change pas ; corriger en « 90s » → bandeau effacé.
7. Un champ secret : ajouter une cellule `http`, dans « Corps » rien ; il n'y a pas de champ `secret` dans les sources livrées — vérifier le mécanisme avec le test `ConfigEditorTests.Un_secret_va_dans_secrets_json…` et, à l'écran, la page Sources qui liste les secrets.
Supprimer les cellules d'essai, tuer l'app.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(app): éditeur de cellule — source, affichage, actions, groupe ; galerie de glyphes"
```

---

### Task 9: Documentation, exemple, défaut sur ce poste, vérification de bout en bout

**Files:**
- Modify: `docs/ARCHITECTURE.md`, `README.md`, `CHANGELOG.md`, `docs/cells.example.json`, `Directory.Build.props` (version 0.2.0)

- [ ] **Step 1: Version et changelog**

`Directory.Build.props` : `<Version>0.2.0</Version>`. `CHANGELOG.md`, en tête :
```markdown
## 0.2.0 - réglages par l'interface
- Fenêtre Réglages : pilules (bord, écran, position, échelle), cellules (catalogue de sources, formulaire généré,
  libellé, glyph, seuils, cadence, actions, groupes), secrets chiffrés, démarrage automatique, thème. Tout s'applique
  immédiatement ; `cells.json` n'a plus besoin d'être ouvert.
- Source `media` : ce qui joue (Spotify, navigateur, VLC…), lecture/pause au clic, précédent/suivant dans la carte.
- Groupe « Système » par défaut (CPU, mémoire, disque, réseau, batterie dans une seule cellule).
- Le clic sur une cellule suit l'action par défaut de sa source (ouvrir, lecture/pause) quand la config n'en fixe pas.
- Les params des cellules sont vérifiés d'après le schéma de la source (requis, nombre, URL, choix).
```

- [ ] **Step 2: ARCHITECTURE.md**

Mettre à jour : l'arborescence (§2 — tous les fichiers nouveaux de ce plan avec une ligne de commentaire chacun, dont `Shared/Controls.xaml` et `Platform/Autostart.cs` comme copies) ; §3 flux de données (ajouter le sens « Réglages → ConfigEditor → fichiers → ConfigStore.Changed → tout le reste ») ; §4 (`DefaultAction`, `Group`, validation typée, légende texte) ; §5 Sources (ligne `media` : `IMediaSession`/`WindowsMediaSession`, Busy/Ok/Off, actions) ; nouvelle section **« Réglages »** (fenêtre, pages, `SchemaForm`, routage partagé/local/secrets, annulation sur refus, en-tête réécrit, ce qui n'est pas dans l'UI : rien) ; §7 erreurs (la ligne « une modification refusée par l'UI est annulée et affichée ») ; § « Ce qui vient ensuite » = plan 3 (Claude Code, ClickUp via ClickUp-Extended, updater, installateur, glisser-déposer, position de lecture).

- [ ] **Step 3: README et exemple**

`README.md` : « Configuration » commence par « Tout se règle dans **Réglages…** (icône du tray, ou clic droit sur une pilule) » ; le paragraphe sur les trois fichiers reste (c'est ce que Réglages écrit), avec la phrase « `cells.json` reste lisible et synchronisable ; l'éditer à la main reste possible mais ses commentaires disparaissent à la première sauvegarde par l'interface ». Tableau des sources : ajouter `media`. `docs/cells.example.json` : remplacer les quatre cellules système visibles par le groupe `sys` + enfants masqués (comme le défaut), ajouter `media`, garder les exemples `http` et `shell` ; `DocsExampleTests` doit rester vert.

- [ ] **Step 4: Ce poste**

Sauvegarder `%APPDATA%\customNotch\cells.json` en `cells.json.avant-0.2` puis le supprimer : le prochain lancement écrit le nouveau défaut. Ne pas toucher `secrets.json` ni la surcharge locale.

- [ ] **Step 5: Vérification complète**

Run: `dotnet build CustomNotch.sln` (0 avertissement), `dotnet test CustomNotch.sln` (tout vert), `powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Run`.
Écran (si libre) : la pilule par défaut (Système, Média, ClickUp) ; Réglages depuis le tray ; thème clair puis sombre (fenêtre, catalogue, menus) ; une lecture Spotify/YouTube visible dans Média. Tuer l'app à la fin, relancer depuis le raccourci du menu Démarrer.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "docs: réglages par l'interface, média, groupe Système ; version 0.2.0"
```

---

## Auto-revue du plan

- **Couverture de la spec** — §1 décisions : forme (T5, T7), application immédiate (T5–T8 : anti-rebond 300 ms), `ConfigEditor` (T3), en-tête réécrit (T3), secrets (T3, T6, T8), `DefaultAction` (T1), média (T2, T4), défaut (T2), pas de glisser-déposer (T7 : ↑↓), galerie (T8). §2 : T1 (schéma, validation, résolution du clic, légende), T3 (`ConfigEditor`). §3 : T5 (fenêtre, Général, Sources), T6 (`SchemaForm`), T7 (arbre, pilule, catalogue), T8 (cellule). §4 : T2, T4. §5 : T2, T9. §6 tests : T1, T2, T3, T6 ; vérifs écran T4, T5, T7, T8, T9 ; docs T9.
- **Types** — `ConfigEditor(ConfigStore, IReadOnlyDictionary<string, SourceSchema>)` (T3) est construit ainsi dans `Controller` (T5) ; `SettingsContext(Store, Editor, Registry)` (T5) consommé par T6–T8 ; `SchemaForm.Build(schema, params, onChange, onSecret)` (T6) appelé avec cette signature en T8 ; `PillEditor(ctx, pill, Action<Action> run)` et `CellEditor(ctx, cell, Action<Action> run)` (T7/T8) reçoivent `PillsPage.Try` ; `IMediaSession`/`MediaState` (T2) implémentés en T4 ; `CoreSources.Build(IMediaSession?)` (T2) appelé en T4 ; `IPillHost.Schema` (T1) utilisé par `PillWindow` (T1).
- **Ordre d'exécution** — T1, T2, T3, T4, T5, T6, T7, T8, T9 : les tests de T3 s'appuient sur le défaut de T2.
- **Points d'attention** — le style `TreeList` hérite de `NavList` : `BasedOn` sur une clé du même dictionnaire nécessite que `NavList` soit déclaré avant ; les styles copiés (`Controls.xaml`) référencent `Border`, `Surface`, `Subtle`, `Accent`, `OnAccent`, `Track`, `Danger` — tous posés par `Theme.Apply` ; `Registry` sur `net10.0` (T5, `Autostart`) passe l'analyseur grâce aux gardes `OperatingSystem.IsWindows()` ; WinRT (T4) : si `await` sur `IAsyncOperation` ne compile pas, `.AsTask()`.

## Plan 3 (à écrire ensuite)

Claude Code (identifiants, endpoint usage, renouvellement `claude -p`, hooks, serveur d'événements, exe hook, connexion), ClickUp (endpoint local dans ClickUp-Extended — sous-projet de l'autre dépôt — puis la source), updater (`Updates.cs` repris, `latest.json` sur les releases), diagnostic `--report`, installateur Inno Setup + tâches planifiées, icône `.ico`, page Sources remplie.
