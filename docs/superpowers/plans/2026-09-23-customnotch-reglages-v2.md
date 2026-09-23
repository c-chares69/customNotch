# customNotch 0.3.2 — Réglages au niveau de ClickUp-Extended — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** La fenêtre Réglages reprend la composition, la densité et les briques de celle de ClickUp-Extended (`HubWindow.xaml` + `SettingsPages.cs`) : sidebar avec marque, entrées de navigation à glyph (`NavItem`), en-tête de page (titre 22 + sous-titre), cartes titrées en capitales (`Section`), lignes de formulaire à libellé fixe (`Ui.FormRow`, champs bornés à 640 px), champs numériques avec − / +, indications (`Hint`) sous les champs, messages d'erreur sous le champ fautif (`Problem`), boutons `Primary`/`Secondary` au même gabarit, « Fermer » en bas à droite. Chaque page est réécrite avec ces briques ; aucun comportement ne change.

**Architecture:** `Settings/Bricks.cs` devient le pendant de `SettingsPages` (briques `Card(title, children…)`, `Check`, `Combo`, `Number`, `Text`, `Action`, `Hint`, `Problem/Say`, `Page`) ; `SettingsWindow` reprend le squelette de `HubWindow.xaml` (sidebar 240 px, marque avec l'icône de l'app, `NavItem` radio à glyph, `NavSection`, en-tête de page, `ScrollViewer` 36/12/18/24, pied « Fermer ») ; les styles `NavItem`, `NavSection` sont copiés dans `Shared/Controls.xaml` (le `NavList`/`TreeList` maison disparaît, sauf l'arbre des pilules qui garde `TreeList`). Les pages (`PillsPage` + `PillEditor` + `CellEditor`, `SourcesPage`, `ClaudePage`, `GeneralPage`) sont recomposées avec ces briques.

**Tech Stack:** WPF, styles `DynamicResource` posés par `Theme.Apply` (déjà les mêmes clés que ClickUp-Extended).

**Spec:** ce document. Référence visuelle : `C:\Users\Coco_FW\Desktop\ClickUp-Extended\docs\apercu-reglages.png` et le code cité.

## Global Constraints

- Commits en français, sans `Co-Authored-By`, sans mention d'IA.
- `Shared/*` : copies fidèles (les deux styles ajoutés à `Controls.xaml` sont copiés tels quels depuis `<Window.Resources>` de `HubWindow.xaml` de ClickUp-Extended, avec le `x:Static` de glyph adapté à `CustomNotch.App.Glyphs`).
- Aucun changement de comportement : mêmes clés écrites, mêmes `ConfigEditor` appels, mêmes anti-rebonds et vidages au démontage, mêmes bandeaux (`EditorBanner` reste, stylé comme le `Banner` de HubWindow : fond `Warn`, texte sombre).
- Processus allégé : deux tâches, un implémenteur chacune (ou un seul en séquence), doc dans la tâche, pas de revue par tâche.
- Ne pas lancer l'application (une copie installée tourne).

---

### Task 1: Briques et coquille — `Bricks` façon `SettingsPages`, `SettingsWindow` façon `HubWindow`

**Files:**
- Modify: `src/CustomNotch.App/Settings/{Bricks,PageBase,SettingsWindow,EditorBanner}.cs`, `src/CustomNotch.App/Shared/Controls.xaml` (+ `NavItem`, `NavSection` copiés), `src/CustomNotch.App/Styles.xaml` (retirer `NavList` s'il n'a plus d'usage ; garder `TreeList` en le faisant hériter d'une base locale), `src/CustomNotch.App/Shared/Glyphs.cs` (ne pas modifier : vérifier que `Layout`, `Drop`, `Monitor`, `Link`, `Gear`, `Pulse` existent — oui, listés)
- Docs: `docs/ARCHITECTURE.md` § Réglages (briques, coquille), `CHANGELOG.md` `## 0.3.2 - <date>` ouvert

**Briques (`Bricks`, statiques, `FrameworkElement host` pour `FindResource` comme dans `SettingsPages`) — signatures :**
```csharp
public static Border Card(FrameworkElement host, string title, params UIElement[] children);   // titre en capitales style Section, cases à cocher sur deux colonnes à partir de quatre — copie de SettingsPages.Card
public static StackPanel Page(params UIElement[] cards);
public static TextBlock Hint(FrameworkElement host, string text);          // style Hint
public static TextBlock Problem(FrameworkElement host); public static void Say(TextBlock problem, string text);
public static CheckBox Check(FrameworkElement host, string label, bool value, Action<bool> commit, string? hint = null);
public static Grid Combo(FrameworkElement host, string label, IReadOnlyList<(string Value, string Text)> options, string? current, Action<string> commit);   // ligne de formulaire complète
public static Grid Number(FrameworkElement host, string label, double value, double min, double max, Action<double> commit, string suffix = "", int decimals = 0, double step = 1);   // champ 110 px + − / + + suffixe, commit sur Entrée / perte de focus
public static Grid Text(FrameworkElement host, string label, string value, Action<string> commit, string? placeholder = null, bool secret = false, Func<string, string?>? validate = null);   // validate rend le message d'erreur ou null ; commit sur Entrée / perte de focus (plus d'anti-rebond 300 ms : c'est le modèle ClickUp-Extended — SAUF pour les champs où la frappe doit s'appliquer en direct : garder `Debounced` disponible pour le libellé de cellule et le tracé SVG)
public static Button Action(FrameworkElement host, string text, Action click, bool primary = false);
public static Grid Row(string label, UIElement field);   // Ui.FormRow(Ui.FormLabel(label), field) — libellé 240
public static UIElement Slider(...)   // inchangé (échelle, position), rendu dans une ligne de formulaire
```
`PageBase` : `Title`/`Subtitle` exposés (l'en-tête est dessiné par la fenêtre, pas par la page) ; `Body` = `Page(...)` ; `Banner` conservé.

**Coquille (`SettingsWindow`)** : grille 240 / * ; sidebar `Sidebar` avec bord droit `Border` ; marque = tuile 34×34 arrondie 9 (dégradé `#2E3039 → #0D0E12`) portant la pilule de l'icône (le tracé de `make-icon.ps1` : capsule blanche + anneau + deux barres, en `Path` 16×14 blanc) + « customNotch » 14 semi-gras + « Réglages » 11.5 `Subtle` ; navigation = `RadioButton` style `NavItem` avec glyph en `Tag` : Pilules & cellules (`Layout`), Sources (`Link`), Claude (`Pulse`), puis `NavSection` « SYSTÈME » et Général (`Gear`) ; bas de sidebar : « version x.y.z » 11 `Subtle` ; page = `Banner` (warn) + en-tête (titre 22 semi-gras, sous-titre `Hint`) + `ScrollViewer` `Padding 36,12,18,24` + pied « Fermer » `Primary` à droite (comme aujourd'hui). Taille 1040×720, min 900×600.

- [ ] **Step 1**: styles copiés (`NavItem`, `NavSection`) — vérifier qu'ils ne référencent que des clés posées par `Theme.Apply` ; le `Tag` glyph est un `Geometry` → adapter `x:Static app:Glyphs.X` en `x:Static local:Glyphs.X` avec le namespace `CustomNotch.App`.
- [ ] **Step 2**: `Bricks` + `PageBase` ; les pages existantes doivent encore compiler (garder les anciennes signatures en transition si nécessaire, retirées en Task 2).
- [ ] **Step 3**: `SettingsWindow` ; `dotnet build` 0 avertissement, `dotnet test` vert ; commit
```powershell
git add -A
git commit -m "feat(settings): briques et coquille au gabarit de ClickUp-Extended (NavItem, cartes, lignes de formulaire, en-tête)"
```

---

### Task 2: Les pages recomposées

**Files:**
- Modify: `src/CustomNotch.App/Settings/{GeneralPage,SourcesPage,ClaudePage,PillsPage,PillEditor,CellEditor,SchemaForm,GlyphGallery,SourceCatalogDialog}.cs`
- Docs: `README.md` (section Réglages si elle décrit l'écran), `docs/ARCHITECTURE.md` § Réglages, `CHANGELOG.md` 0.3.2 finalisé, `Directory.Build.props` 0.3.2

**Général** (titre « Général », sous-titre « Ce poste, l'apparence, les mises à jour ») : `Card("Configuration", Text(« Fichier cells.json », …), Hint)`, `Card("Apparence", Combo(activité), <échelle de la carte en Slider dans une Row>, Combo(thème))`, `Card("Mises à jour", Check, Text(url, placeholder = adresse par défaut), Number(heures, 1–720, " h"), Action(Vérifier maintenant), Action(Installer, primary) + Action(Ignorer), statut Hint)`, `Card("Poste", Check(démarrage) + Hint d'erreur)`, `Card("Journal et diagnostic", Action(Ouvrir le journal), Action(Signaler un problème), Hint)`, `Card("À propos", Hint(version), Action(Le dépôt sur GitHub))`.
**Sources** (« Sources », « Réglages globaux par type de source et secrets ») : une `Card` par type de source ayant des champs globaux (générés par `SchemaForm` — qui adopte `Row`/`Text`/`Check`/`Combo`/`Problem` des briques au lieu de ses propres contrôles), puis `Card("Secrets", liste nom → Action(Effacer), Hint DPAPI)`.
**Claude** (« Claude », « Compte, jeton, lecture, sessions ») : `Card("Compte", Hint plan/tier, Hint jeton, Action(Se connecter, primary))`, `Card("Lecture", Hint dernière lecture / erreur / prochain essai, Action(Relire maintenant))`, `Card("Sessions", lignes nom — état — depuis en Hint)`, `Card("Dossier", Text(home))`.
**Pilules & cellules** (« Pilules & cellules », « Ce que montre chaque pilule ») : à gauche l'arbre (`TreeList`, largeur 280) dans une `Card("Pilules")` avec la barre d'outils en `Action` `Secondary` compacts (+ Pilule, + Cellule, ↑, ↓, Masquer, Supprimer) ; à droite l'éditeur : `PillEditor` = `Card("Pilule « id »", Combo(bord — radios acceptés), Combo(écran), Row(position: Slider), Row(échelle: Slider), Combo(forme des cellules), Check(visible sur ce poste))` ; `CellEditor` = `Card("Source", …SchemaForm…, Action(Changer de source))`, `Card("Affichage", Text(libellé — Debounced), Row(glyph: GlyphGallery), Combo(type de rendu), Combo(cadence) + Text(personnalisée), Row(seuils: warn/crit Number + Check inversé), Combo(légende), Combo(activité))`, `Card("Actions", Combo(clic) + Text(cible), liste des actions de carte en lignes + Action(Ajouter))`, `Card("Groupe", cases enfants sur deux colonnes, Combo(tête))`. Le bandeau d'erreur reste en tête de l'éditeur.

- [ ] **Step 1**: Général, Sources, Claude — [ ] **Step 2**: Pilules & cellules (PillEditor, CellEditor, SchemaForm, GlyphGallery, catalogue) — [ ] **Step 3**: docs, version ; `dotnet build` 0 avertissement, `dotnet test` vert (les tests `SchemaFormTests` sur les fonctions pures restent verts) ; commit
```powershell
git add -A
git commit -m "feat(settings): toutes les pages recomposées avec les briques de ClickUp-Extended ; version 0.3.2"
```

## Auto-revue

- Couverture : sidebar/marque/nav (T1), en-tête (T1), cartes/lignes/champs (T1), pages (T2), aucun changement de clés (T2 relit chaque appel `ConfigEditor`).
- Points d'attention : `NavItem` est un `RadioButton` — la sélection programmée (`Go(key)`) passe par `IsChecked` ; `SchemaForm.Build` garde sa signature (Task 8 du plan 2 l'appelle) ; les focus restaurés par nom (`PillsPage.ShowEditor`) reposent sur `Name` des champs : conserver les noms dans les nouvelles briques (`Text(..., name)` optionnel).
