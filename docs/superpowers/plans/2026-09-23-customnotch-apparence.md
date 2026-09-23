# customNotch 0.2.2 — apparence et flexibilité — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Formes de cellules au choix (rondes / carrées arrondies, par pilule), indicateur d'activité « pastille seule » par défaut, légende masquable, carte au survol grande et au thème Windows avec boutons en icônes, carte média avec position de lecture et repli d'ouverture, carte Système avec synthèse et valeurs absolues.

**Architecture:** Le modèle (`Core`) porte les nouvelles options (`AppearanceConfig`, `PillConfig.CellsShape`, `CellConfig.Caption/Activity`, `SourceSchema.DefaultCaption`) et les résout dans `CellView` (`Shape`, `Activity`, `ShowCaption`), sans WPF. L'App dessine : `SquareArc` (géométrie pure, testée), faces carrées, `HoverCard` refaite au thème avec `CardScale`, boutons à icônes. Les Réglages exposent chaque option ; `ConfigEditor.SetAppearance` écrit le bloc partagé.

**Tech Stack:** C# 14 / .NET 10, WPF, xUnit, System.Text.Json, WinRT `Windows.Media.Control` (timeline).

**Spec:** `docs/superpowers/specs/2026-09-23-customnotch-apparence-design.md`

## Global Constraints

- Commits en français, sans `Co-Authored-By`, sans mention d'IA (règle du user, prime sur toute autre consigne d'attribution).
- `TreatWarningsAsErrors` ; docs XML en français qui disent pourquoi ; `Core` sans dépendance Windows hors `Platform/`.
- `Shared/*` copiés de ClickUp-Extended : ne pas modifier.
- Ne pas lancer l'application (une copie installée tourne depuis `%LOCALAPPDATA%` ; bureau occupé). Vérifier par lecture, build et tests.
- Valeurs : formes `round|square` ; activité `dot|ring` ; `cardScale` ∈ [1, 1.5] ; rayon des cellules carrées = `12/44 × Ring`.

---

### Task 1: Modèle et configuration — options d'apparence résolues dans CellView

**Files:**
- Modify: `src/CustomNotch.Core/Config/CellsFile.cs`, `src/CustomNotch.Core/Config/ConfigValidation.cs`, `src/CustomNotch.Core/Config/ConfigEditor.cs`, `src/CustomNotch.Core/Sources/ISource.cs` (`SourceSchema.DefaultCaption`), `src/CustomNotch.Core/Model/CellView.cs`, `src/CustomNotch.App/Controller.cs` (`View()`)
- Test: `tests/CustomNotch.Core.Tests/Model/CellViewTests.cs`, `tests/CustomNotch.Core.Tests/Config/ConfigValidationTests.cs`, `tests/CustomNotch.Core.Tests/Config/ConfigEditorTests.cs`

**Interfaces:**
- Produces: `sealed class AppearanceConfig { string Activity = "dot"; double CardScale = 1.0; }` et `CellsFile.Appearance : AppearanceConfig` (JSON `appearance`, jamais null après lecture : `??= new()`) ; `PillConfig.CellsShape : string = "round"` (JSON `cellsShape`) ; `CellConfig.Caption : bool?`, `CellConfig.Activity : string?` ; `SourceSchema(..., bool DefaultCaption = true)` (dernier paramètre positionnel, défaut true) ; `CellView` gagne `string Shape, string Activity, bool ShowCaption` (trois derniers paramètres) ; `CellViews.From(CellConfig cell, Reading r, long nowMs, PillConfig? pill = null, AppearanceConfig? appearance = null, SourceSchema? schema = null)` et `FromGroup(CellConfig group, IReadOnlyList<CellView> children, long nowMs, PillConfig? pill = null, AppearanceConfig? appearance = null)` ; `CellViews.Resolve(CellConfig cell, PillConfig? pill, AppearanceConfig? appearance, SourceSchema? schema) → (string Shape, string Activity, bool ShowCaption)` (pur) ; `ConfigEditor.SetAppearance(Action<JsonObject> mutate)`.
- Consumes: `CellsJson` (sérialisation snake_case ? — vérifier `CellsJson.Options` : si la politique de nommage est `SnakeCaseLower`, `CellsShape` → `cellsShape` et `CardScale` → `cardScale` sans attribut ; sinon poser `[JsonPropertyName]`).

- [ ] **Step 1: Tests (écrire, voir échouer)**

`CellViewTests` — ajouter :
```csharp
    [Fact]
    public void La_forme_et_l_activite_viennent_de_la_pilule_et_de_l_apparence()
    {
        var pill = new PillConfig { Id = "p", CellsShape = "square" };
        var app = new AppearanceConfig { Activity = "ring" };
        var cell = new CellConfig { Id = "c", Source = "system.cpu" };
        var v = CellViews.From(cell, new Reading(Value: 3, Max: 100), 0, pill, app);
        Assert.Equal("square", v.Shape);
        Assert.Equal("ring", v.Activity);
        Assert.True(v.ShowCaption);
    }

    [Fact]
    public void La_cellule_surcharge_l_activite_et_la_legende()
    {
        var cell = new CellConfig { Id = "c", Source = "system.cpu", Activity = "dot", Caption = false };
        var v = CellViews.From(cell, new Reading(Value: 3, Max: 100), 0, null, new AppearanceConfig { Activity = "ring" });
        Assert.Equal("dot", v.Activity);
        Assert.False(v.ShowCaption);
    }

    [Fact]
    public void Le_schema_de_la_source_fixe_la_legende_par_defaut()
    {
        var schema = new SourceSchema("media", "Média", Array.Empty<SchemaField>(), "music", "", "toggle", DefaultCaption: false);
        var v = CellViews.From(new CellConfig { Id = "m", Source = "media" }, new Reading(Text: "Titre"), 0, null, null, schema);
        Assert.False(v.ShowCaption);
        var forced = CellViews.From(new CellConfig { Id = "m", Source = "media", Caption = true }, new Reading(Text: "Titre"), 0, null, null, schema);
        Assert.True(forced.ShowCaption);
    }

    [Fact]
    public void Sans_options_les_defauts_sont_rond_pastille_legende()
    {
        var v = CellViews.From(new CellConfig { Id = "c" }, new Reading(Text: "x"), 0);
        Assert.Equal("round", v.Shape); Assert.Equal("dot", v.Activity); Assert.True(v.ShowCaption);
    }
```
`ConfigValidationTests` — ajouter `Une_forme_ou_une_echelle_inconnue_est_refusee` : un fichier avec `pills[0].cellsShape = "hex"`, `appearance.cardScale = 3`, `cells[0].activity = "blink"` → trois erreurs contenant `cellsShape`, `cardScale`, `activity`. (Construire le `CellsFile` en objets, comme les tests voisins.)
`ConfigEditorTests` — ajouter `SetAppearance_ecrit_le_bloc_partage` : `_editor.SetAppearance(a => a["activity"] = "ring")` puis `_store.Current.Appearance.Activity == "ring"` et le texte de `cells.json` contient `"appearance"`.

Run: `dotnet test CustomNotch.sln --filter "FullyQualifiedName~CellViewTests|FullyQualifiedName~ConfigValidationTests|FullyQualifiedName~ConfigEditorTests"` → échecs de compilation attendus.

- [ ] **Step 2: Modèle**

`CellsFile.cs` :
```csharp
/// <summary>L'apparence commune à toutes les pilules (partagée entre machines) : l'indicateur d'activité et l'échelle
/// de la carte au survol. Les formes sont par pilule, la légende et l'activité surchargées par cellule.</summary>
public sealed class AppearanceConfig
{
    /// <summary>dot : Busy/Attention ne colorent que la pastille ; ring : l'arc animé autour de la cellule.</summary>
    public string Activity { get; set; } = "dot";
    /// <summary>1.0 à 1.5 : la carte au survol est agrandie d'autant (texte compris).</summary>
    public double CardScale { get; set; } = 1.0;
}
```
`CellsFile` : `public AppearanceConfig Appearance { get; set; } = new();`. `PillConfig` : `/// <summary>round | square : la forme de toutes les cellules de la pilule.</summary> public string CellsShape { get; set; } = "round";`. `CellConfig` : `/// <summary>null = selon la source (SourceSchema.DefaultCaption) ; false = jamais de texte sous la cellule.</summary> public bool? Caption { get; set; }` et `/// <summary>dot | ring ; null = l'apparence globale.</summary> public string? Activity { get; set; }`. Vérifier la politique de nommage JSON dans `CellsJson` (`cellsShape`, `cardScale` doivent lire/écrire ainsi) — ajouter `[JsonPropertyName]` si nécessaire. `CellsJson.ToFile` : après désérialisation, `file.Appearance ??= new()`.

`ISource.cs` : `SourceSchema` gagne `bool DefaultCaption = true` en dernier paramètre positionnel (doc : « false pour une source dont le texte n'a pas sa place sous la cellule — le média : le titre est dans la carte »).

`ConfigValidation.Validate(file, knownSources)` : après `scale` : `if (pill.CellsShape is not ("round" or "square")) errors.Add($"{at}.cellsShape « {pill.CellsShape} » : attendu round ou square");` ; par cellule : `if (cell.Activity is not null and not ("dot" or "ring")) errors.Add($"{cat}.activity « {cell.Activity} » : attendu dot ou ring");` ; en tête : `if (file.Appearance.Activity is not ("dot" or "ring")) errors.Add("appearance.activity : attendu dot ou ring"); if (file.Appearance.CardScale is < 1 or > 1.5) errors.Add("appearance.cardScale : attendu entre 1 et 1.5");`.

`ConfigEditor.SetAppearance(Action<JsonObject> mutate)` : `var root = Shared(); if (root["appearance"] is not JsonObject a) root["appearance"] = a = new JsonObject(); mutate(a); CommitShared(root);`.

- [ ] **Step 3: CellView**

`CellView` : ajouter `string Shape, string Activity, bool ShowCaption` en fin de record. `CellViews.Resolve` :
```csharp
    /// <summary>Ce que la cellule montre au-delà de sa lecture : la forme vient de la pilule, l'activité de la cellule
    /// sinon de l'apparence globale, la légende de la cellule sinon du schéma de sa source. Pur, pour être testé.</summary>
    public static (string Shape, string Activity, bool ShowCaption) Resolve(CellConfig cell, PillConfig? pill, AppearanceConfig? appearance, SourceSchema? schema)
        => (pill?.CellsShape ?? "round", cell.Activity ?? appearance?.Activity ?? "dot", cell.Caption ?? schema?.DefaultCaption ?? true);
```
`From` et `FromGroup` prennent les paramètres optionnels et passent le triplet (le groupe n'a pas de schéma : `Resolve(group, pill, appearance, null)`). Toutes les constructions de `CellView` existantes (y compris tests) passent les trois valeurs.

`Controller.View(cellId)` : retrouver la pilule de la cellule (`_config.Current.Pills.First(p => p.Cells.Contains(cell))` — écrire un petit `PillOf(cell)`), passer `pill`, `_config.Current.Appearance`, `Schema(cell.Source)`.

- [ ] **Step 4: Vérifier et committer**

`dotnet build CustomNotch.sln` (0 avertissement), `dotnet test CustomNotch.sln` (vert).
```powershell
git add -A
git commit -m "feat(core): options d'apparence — forme par pilule, activité, légende, échelle de la carte ; résolues dans CellView"
```

---

### Task 2: Média et carte — position de lecture, repli d'ouverture, synthèse du groupe, boutons à icônes

**Files:**
- Modify: `src/CustomNotch.Core/Sources/Media/IMediaSession.cs` (`MediaState`), `MediaSource.cs`, `src/CustomNotch.Core/Model/CellClickResolver.cs` (si nécessaire), `src/CustomNotch.App/Notch/CardContent.cs`
- Test: `tests/CustomNotch.Core.Tests/Sources/MediaSourceTests.cs`, `tests/CustomNotch.App.Tests/CardContentTests.cs`

**Interfaces:**
- Produces: `MediaState(string Title, string Artist, string App, bool Playing, byte[]? Cover = null, long? PositionMs = null, long? DurationMs = null, long? PositionAtMs = null)` (égalité : les trois nouveaux champs comptent comme les autres) ; `MediaSource` : schéma avec champ `fallbackOpen` (`string`, non requis, défaut `spotify:`, libellé « Sans lecture, le clic ouvre »), `DefaultCaption: false` ; lecture avec session : `Detail = [ DetailRow("position", "2:31 / 5:55", 0.42, "timeline") ]` seulement si `PositionMs` et `DurationMs > 0` (sinon `Detail` vide), `Actions = [ ("prev", "", "prev"), ("toggle", "", playing ? "pause" : "play"), ("next", "", "next") ]` (libellés vides), `Text` = « Titre — Artiste » (pour la légende automatique si un user la réactive) ; lecture sans session : `Text: "Aucune lecture"`, `Status.Off`, `Detail = [ DetailRow("Lecture", "aucune — cliquer ouvre l'application") ]`, `Actions = [ ("open", "Ouvrir", "open") ]` ; `InvokeAsync("toggle"|"open")` sans session → `ActionRunner.Open(fallbackOpen)` (lire `ctx.Params["fallbackOpen"]`, défaut `spotify:`).
- `CardContent.Build` : titre = `Title` du média quand `view.Reading` porte une ligne `position` ou que la source est `media` (`cell.Source == "media"`) → `CardModel(Title: titre de la piste, Subtitle: artiste, Rows: [ligne position seule], Image: pochette)` ; sans session : `Title = view.Label`, une ligne « Aucune lecture… ». Groupe : `Subtitle` = synthèse : Crit → `"{label} critique"`, sinon Warn → `"{label} à surveiller"`, sinon `"tout va bien"` (label du pire enfant) ; `CardRow.Text` d'un enfant = `caption` si l'enfant n'a pas de Detail, sinon `caption + " · " + detail[0].Text` quand `detail[0].Text` existe et diffère de la caption (ex. « 61 % · 9,8 Go »).
- `CardAction.Label` peut être vide : la carte montre l'icône seule (Task 5).

- [ ] **Step 1: Tests**

`MediaSourceTests` : `La_position_fait_une_ligne_timeline` (état avec `PositionMs: 151_000, DurationMs: 355_000` → une `DetailRow` label `position`, texte `"2:31 / 5:55"`, fraction ≈ 0.425, hint `timeline`) ; `Sans_timeline_pas_de_ligne_position` ; `Les_actions_media_n_ont_pas_de_libelle` (trois actions, `Label == ""`, icônes `prev`/`pause`/`next` en lecture) ; `Sans_session_le_clic_ouvre_le_repli` : `FakeSession` sans état, `ReadAsync` → action `open` ; `InvokeAsync("toggle", ctx)` ne lève pas (l'ouverture réelle passe par `ActionRunner.Open` — pour le test, injecter une `Func<string, bool> open` dans le constructeur de `MediaSource` (paramètre optionnel, défaut `ActionRunner.Open`) et vérifier qu'elle reçoit `"spotify:"`).
`CardContentTests` : `Un_groupe_a_une_synthese_et_des_valeurs_absolues` (enfants CPU 34 % Ok sans Detail, Mémoire 61 % avec `DetailRow("Utilisée", "9,8 Go", .61)`, Disque Warn → `Subtitle == "Disque à surveiller"`, la ligne Mémoire a `Text == "61 % · 9,8 Go"`) ; `Une_cellule_media_a_le_titre_en_tete` (lecture média avec `Text: "Bohemian Rhapsody — Queen"`, Detail position, cell `Source = "media"`, `Label = "Spotify"` → `Title == "Bohemian Rhapsody"`, `Subtitle == "Queen"`, une seule ligne `position`).

- [ ] **Step 2: Implémenter**

`MediaSource` : `private static string Clock(long ms) => $"{ms / 60000}:{ms / 1000 % 60:00}";`. Pour séparer titre et artiste dans `CardContent` sans réanalyser le texte : `Reading.Text` = « Titre — Artiste » ; `CardContent` coupe sur `" — "` (première occurrence) ; sans « — », tout est le titre. (Simple, et la carte n'est pas un modèle de données.)

`CellClickResolver` : inchangé (le clic par défaut reste `toggle` ; c'est `MediaSource.InvokeAsync` qui ouvre le repli sans session).

- [ ] **Step 3: Vérifier et committer**

`dotnet build` 0 avertissement, `dotnet test` vert.
```powershell
git add -A
git commit -m "feat(media,card): position de lecture, ouverture de repli sans lecture, synthèse du groupe, actions sans libellé"
```

---

### Task 3: Session média — la timeline WinRT et l'avance locale

**Files:**
- Modify: `src/CustomNotch.App/Platform/WindowsMediaSession.cs`, `src/CustomNotch.Core/Sources/Media/MediaSource.cs` (avance locale)

- [ ] **Step 1: Lire la timeline**

`RefreshAsync` : `var tl = session.GetTimelineProperties();` → `PositionMs = (long)tl.Position.TotalMilliseconds`, `DurationMs = (long)(tl.EndTime - tl.StartTime).TotalMilliseconds`, `PositionAtMs = tl.LastUpdatedTime.ToUnixTimeMilliseconds()` ; si `DurationMs <= 0` → les trois null. S'abonner à `session.TimelinePropertiesChanged` comme aux deux autres événements (gestionnaire conservé et désabonné dans `Dispose`/`Attach`). Doc : Spotify publie la position toutes les quelques secondes seulement ; l'avance à la seconde est locale.

- [ ] **Step 2: Avance locale**

`MediaSource.ReadAsync` : si `Playing` et `PositionAtMs` connu, la position affichée = `PositionMs + (now − PositionAtMs)`, bornée à `DurationMs` (`ctx.NowMs` existe-t-il dans `CellContext` ? sinon `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` via un `Func<long> now` injectable, défaut horloge). `DefaultRefresh` du média passe à **1 s** quand une lecture est en cours (`ReadAsync` peut le dire ? — non : la cadence est fixée par le Scheduler). Solution : garder 5 s de cadence, et faire avancer la barre côté carte : `HoverCard` (Task 5) reçoit la ligne `position` avec `Hint == "timeline"` et anime la barre localement d'une seconde à l'autre entre deux lectures (un `DispatcherTimer` 1 s tant que la carte est ouverte, qui recalcule `fraction` et le texte à partir de `PositionAtMs` — pour cela la ligne porte aussi la base : `DetailRow("position", "2:31 / 5:55", 0.42, "timeline:151000:355000:1790168101000")` (position, durée, horodatage). Ajuster le test de la Task 2 en conséquence (`Hint` commence par `timeline:`).

- [ ] **Step 3: Vérifier et committer**

`dotnet build` 0 avertissement, `dotnet test` vert.
```powershell
git add -A
git commit -m "feat(media): position de lecture depuis la timeline système, avancée localement à la seconde"
```

---

### Task 4: Cellules — formes carrées, activité par pastille, légende masquable

**Files:**
- Create: `src/CustomNotch.App/Cells/SquareArc.cs`
- Modify: `src/CustomNotch.App/Cells/{CellFace,RingCell,StatusCell,CellHost}.cs`
- Test: `tests/CustomNotch.App.Tests/SquareArcTests.cs`

**Interfaces:**
- Produces: `SquareArc.Geometry(double fraction, double size, double radius, double startOffset = size/2)` → `Geometry` figée : le contour d'un carré arrondi de côté `size`, rayon `radius`, parcouru dans le sens horaire depuis le milieu du bord haut, sur `fraction` de son périmètre (`0` → `Geometry.Empty`, `1` → contour complet moins 0,1 %). `SquareArc.Perimeter(size, radius) = 4 × (size − 2 radius) + 2π radius`.
- `CellFace.Render(CellView view, PillMetrics m)` lit `view.Shape` et `view.Activity`.

- [ ] **Step 1: Tests SquareArc**

```csharp
public class SquareArcTests
{
    [Fact] public void Zero_est_vide() => Assert.True(SquareArc.Geometry(0, 44, 12).IsEmpty());
    [Fact] public void Le_perimetre_est_celui_du_carre_arrondi() => Assert.Equal(4 * 20 + 2 * Math.PI * 12, SquareArc.Perimeter(44, 12), 6);
    [Fact]
    public void Un_quart_finit_au_milieu_du_bord_droit()
    {
        var g = SquareArc.Geometry(0.25, 44, 12);
        var end = SquareArc.PointAt(0.25, 44, 12);
        Assert.Equal(44, end.X, 3); Assert.Equal(22, end.Y, 3);
        Assert.False(g.IsEmpty());
    }
    [Fact]
    public void Un_demi_finit_au_milieu_du_bord_bas()
    {
        var end = SquareArc.PointAt(0.5, 44, 12);
        Assert.Equal(22, end.X, 3); Assert.Equal(44, end.Y, 3);
    }
}
```
(Le projet de tests App référence WPF : `Geometry` s'utilise sans thread STA pour ces appels.)

- [ ] **Step 2: SquareArc**

Implémentation : le contour est une suite de 8 segments (4 droits, 4 arcs de 90°) ; `PointAt(fraction, size, radius)` avance d'une longueur `fraction × Perimeter` le long de cette suite en partant de `(size/2, 0)` sens horaire ; `Geometry` construit un `StreamGeometry` (`BeginFigure` au point de départ, puis `LineTo`/`ArcTo` segment par segment, le dernier tronqué), figé. Commentaire : pourquoi partir du milieu du haut (même origine que `RingArc`) et pourquoi `0.999` à 100 % (même raison que l'anneau).

- [ ] **Step 3: Faces**

- `RingCell` : si `view.Shape == "square"` : la piste est un `Path` avec `SquareArc.Geometry(1, side, r)` (trait `stroke`, `StrokeLineJoin.Round`), l'arc `SquareArc.Geometry(fraction, side, r)` ; sinon comme aujourd'hui. `side = m.Ring − stroke`, `r = 12/44 × m.Ring − stroke/2` (garder les deux jeux d'éléments et basculer `Visibility`, pour ne pas recréer la face).
- `StatusCell` : en carré, `_disc` (Ellipse) est remplacé par un `Border` `CornerRadius = 12/44 × m.Ring` (fond piste ou `ImageBrush` pochette) ; la pastille reste ronde.
- `CellFace.Activity(status, m, activity)` : nouveau paramètre `string activity` ; si `"dot"` → arc caché, animation arrêtée (`StopSpin`, `BeginAnimation(OpacityProperty, null)`), retour ; sinon comme aujourd'hui. En carré + `"ring"`, l'arc d'activité suit aussi le contour carré (`SquareArc.Geometry(0.25, …)` qui tourne ; l'animation de rotation sur un carré est acceptable : elle tourne autour du centre).
- `CellHost.Render` : `_caption.Visibility = view.ShowCaption ? Visible : Collapsed` — mais la hauteur réservée reste (`Height = m.Caption` sur un conteneur) pour que les cellules gardent le même pas : remplacer `Collapsed` par `Hidden`.

- [ ] **Step 4: Vérifier et committer**

`dotnet build` 0 avertissement, `dotnet test` vert.
```powershell
git add -A
git commit -m "feat(cells): cellules carrées arrondies, activité par pastille seule, légende masquable"
```

---

### Task 5: La carte au survol — mise en page B, au thème, boutons à icônes, échelle

**Files:**
- Modify: `src/CustomNotch.App/Notch/{HoverCard,PillWindow,PillMetrics}.cs`, `src/CustomNotch.App/Styles.xaml` (`CardButton`), `src/CustomNotch.App/Cells/StatusPalette.cs` (retirer `CardBg` si plus utilisé)

**Interfaces:**
- `PillMetrics(double Scale, double CardScale = 1.0)` : `CardWidth => 340 * CardScale` ; `PillWindow` construit `_m` avec `Pill.Scale` et `appearance.CardScale` (le `Controller`/`IPillHost` expose `Appearance` : ajouter `AppearanceConfig Appearance { get; }` à `IPillHost`, implémenté par `Controller` depuis `_config.Current`). Le changement d'échelle passe par `ConfigStore.Changed` → `PillWindow.Apply(pill)` recrée `_m` (comme `Scale`).

- [ ] **Step 1: HoverCard**

Reconstruire `Show(model, cellId)` selon la spec §1 « Carte au survol » : `Background = (Brush)FindResource("Surface")` → utiliser `SetResourceReference(BackgroundProperty, "Surface")`, `BorderBrush` ← `Border`, `BorderThickness = 1`, `CornerRadius = 18`, `Padding = 18,20`, `Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 12, Opacity = 0.45, Direction = 270 }`, `MaxWidth = _m.CardWidth`, `MinWidth = 240 * CardScale`, `LayoutTransform = new ScaleTransform(CardScale, CardScale)` (les tailles de police restent celles de la spec, l'échelle fait le reste — donc `MaxWidth = 340`, non multiplié, puisque la transformation multiplie).
En-tête : `model.Image` → `Border` 56×56 rayon 12 avec `ImageBrush` ; sinon tuile 40×40 rayon 10 fond `SurfaceHover` avec le glyph 20 en `Fg` ; à droite, `Title` 17 semi-gras `Fg` et `Subtitle` 13 `Muted`.
Lignes : `Grid` 2 colonnes (`*`, `Auto`) ; label 14 semi-gras `Fg`, texte 13 `Muted` à droite ; barre 6 px rayon 3 fond `Track`, remplissage couleur du statut ; séparateur 1 px `Border` entre les lignes (pas avant la première) ; ligne `position` (hint commençant par `timeline:`) : pas de label, barre pleine largeur couleur Busy, temps `m:ss` à gauche et durée à droite en 12 `Muted`, animée par un `DispatcherTimer` 1 s tant que la carte est ouverte (recalcul depuis `timeline:pos:dur:at`).
Actions : `WrapPanel` → `UniformGrid` 1 ligne quand toutes les actions ont un libellé vide (icône seule, 44×36, la deuxième 64 de large) ; sinon `WrapPanel` de boutons icône + texte. Contenu d'un bouton : `StackPanel` horizontal, `GlyphLibrary.Get(icon)` rendu 16 px en `Fg` (`Glyphs.Fill/Stroke` selon `filled`) puis `TextBlock` 13 si libellé. `CardButton` restylé dans `Styles.xaml` : fond `SurfaceHover`, texte `Fg`, bord transparent, rayon 10, padding `12,8`, `FontSize 13`.
Note de péremption : 12 `Muted`, marge haute 10.

- [ ] **Step 2: Fenêtre**

`PillWindow.PlaceCard` : `w` par défaut `_m.CardWidth`. `Layout`/`MoveTo` : `reserve = _m.CardWidth + _m.CardGap` déjà en place, `CardWidth` intègre `CardScale`. `IPillHost.Appearance` : ajouter et implémenter.

- [ ] **Step 3: Vérifier et committer**

`dotnet build` 0 avertissement, `dotnet test` vert.
```powershell
git add -A
git commit -m "feat(card): carte au survol grande et au thème Windows, boutons à icônes, position de lecture animée, échelle réglable"
```

---

### Task 6: Réglages — forme par pilule, légende et activité par cellule, carte Apparence

**Files:**
- Modify: `src/CustomNotch.App/Settings/{PillEditor,CellEditor,GeneralPage}.cs`

- [ ] **Step 1: PillEditor**

Après « Échelle » : `Row("Forme des cellules", Combo(Shapes, pill.CellsShape, v => run(() => ctx.Editor.SetPillShared(pill.Id, p => { if (v == "round") p.Remove("cellsShape"); else p["cellsShape"] = v; }))))` avec `Shapes = { ("round", "Rondes"), ("square", "Carrées arrondies") }` (utiliser la brique `Combo` disponible dans `PillEditor`, sinon `Bricks.Combo`).

- [ ] **Step 2: CellEditor (section Affichage)**

Après « Glyph » : `Row("Légende sous la cellule", Combo(Captions, _cell.Caption switch { null => "", true => "on", false => "off" }, v => Set(c => { if (v == "") c.Remove("caption"); else c["caption"] = v == "on"; })))` avec `Captions = { ("", "Selon la source"), ("on", "Toujours"), ("off", "Jamais") }` ; `Row("Activité", Combo(Activities, _cell.Activity ?? "", v => Set(c => { if (v == "") c.Remove("activity"); else c["activity"] = v; })))` avec `Activities = { ("", "Par défaut"), ("dot", "Pastille seule"), ("ring", "Anneau animé") }`.

- [ ] **Step 3: GeneralPage**

Nouvelle section « Apparence » avant « Poste » : `Row("Indicateur d'activité", Combo(Activities, ctx.Store.Current.Appearance.Activity, v => Try(() => ctx.Editor.SetAppearance(a => a["activity"] = v))))` et `Row("Échelle de la carte", <curseur 1.0–1.5 pas 0.05, libellé « 120 % »>)` — reprendre le `Slider` de `PillEditor` (le déplacer dans `Bricks` s'il est privé, avec le même vidage au démontage) ; écriture `SetAppearance(a => a["cardScale"] = Math.Round(v, 2))`. `GeneralPage` n'a pas de bandeau : ajouter un `EditorBanner` en tête et un `Try` local identique à celui de `PillsPage` (ou exposer `PageBase.Try` : préférable — le déplacer dans `PageBase` et faire pointer `PillsPage.Try` dessus).

- [ ] **Step 4: Vérifier et committer**

`dotnet build` 0 avertissement, `dotnet test` vert.
```powershell
git add -A
git commit -m "feat(settings): forme des cellules par pilule, légende et activité par cellule, carte Apparence"
```

---

### Task 7: Documentation, exemple, version 0.2.2

**Files:**
- Modify: `CHANGELOG.md`, `README.md`, `docs/ARCHITECTURE.md`, `docs/cells.example.json`, `Directory.Build.props`

- [ ] **Step 1**

`Directory.Build.props` : `0.2.2`. `CHANGELOG.md`, en tête, format ClickUp-Extended :
```markdown
## 0.2.2 - <date>

- **Cellules rondes ou carrées, au choix par pilule** (Réglages → pilule → Forme des cellules) : la jauge
  devient un cadre qui se remplit, la pochette et le disque prennent la même forme.
- **La lecture ne fait plus tourner d'anneau.** Occupé et Attention ne colorent que la pastille ; l'anneau
  animé reste disponible (Réglages → Général → Apparence, ou par cellule).
- **Légende masquable** sous chaque cellule ; jamais de titre sous la cellule média (le titre est dans la carte).
- **La carte au survol, refaite.** Plus grande (340 px, texte 14/13), lignes séparées, ombre, au thème
  Windows clair/sombre comme la fenêtre Réglages ; échelle réglable jusqu'à 150 %. Les boutons portent
  les icônes du pack ; précédent / lecture-pause / suivant sont des icônes seules.
- **Carte média** : pochette en grand, titre et artiste en tête, position de lecture (barre et temps) qui
  avance à la seconde ; plus de ligne « Application ». Sans lecture, le clic ouvre l'application
  (`fallbackOpen`, `spotify:` par défaut).
- **Carte Système** : sous-titre de synthèse (« tout va bien », « Disque à surveiller ») et valeurs
  absolues à côté du pourcentage.
```
`README.md` : tableau des sources (`media` : `fallbackOpen`) ; section Configuration : les nouvelles clés (`appearance`, `cellsShape`, `caption`, `activity`) en deux lignes. `docs/ARCHITECTURE.md` : §4 (`CellView.Shape/Activity/ShowCaption`, `Resolve`), §5 (`media` : timeline, `fallbackOpen`), §6 rendu (`SquareArc`, carte au thème, `CardScale`), §Réglages (Apparence). `docs/cells.example.json` : ajouter `"appearance": { "activity": "dot", "cardScale": 1.0 }` et `"cellsShape": "square"` sur la première pilule (commentés) ; `DocsExampleTests` vert.

- [ ] **Step 2: Vérification et commit**

`dotnet build` 0 avertissement, `dotnet test` vert.
```powershell
git add -A
git commit -m "docs: apparence et flexibilité ; version 0.2.2"
```

---

## Auto-revue du plan

- **Couverture de la spec** — §1 : forme (T1, T4, T6), activité (T1, T4, T6), légende (T1, T4, T6), carte B au thème + échelle (T1, T5, T6), boutons (T2, T5), carte média (T2, T3, T5), `fallbackOpen` (T2), Système (T2), thème (T5), ce poste (rien). §2 modèle : T1, T2. §3 rendu : T4, T5. §4 réglages : T6. §5 tests : T1, T2, T4. Docs : T7.
- **Types** — `CellViews.From(cell, r, now, pill?, appearance?, schema?)` (T1) appelé par `Controller.View` (T1) et les tests (T2) ; `CellView.Shape/Activity/ShowCaption` (T1) lus en T4 ; `MediaState` étendu (T2) rempli en T3 ; `DetailRow("position", …, "timeline:pos:dur:at")` produit en T2/T3, lu en T5 ; `PillMetrics.CardScale` (T5) alimenté par `IPillHost.Appearance` (T5) ; `ConfigEditor.SetAppearance` (T1) appelé en T6.
- **Ordre** — T1, T2, T3, T4, T5, T6, T7.
- **Points d'attention** — la politique de nommage JSON de `CellsJson` (snake_case ou attributs) ; `Geometry` dans les tests App sans STA (OK pour des `StreamGeometry`) ; `SetResourceReference` sur une fenêtre sans `Application.Resources` fusionnées — `PillWindow` est une `Window` de la même `Application`, les ressources d'application s'appliquent ; `DropShadowEffect` sur une fenêtre `AllowsTransparency` : coûteux mais acceptable sur une carte de 340 px (si le rendu logiciel rame, passer `BlurRadius` à 24).
