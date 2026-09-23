# customNotch — Conception du plan 2 : Réglages, média, groupe par défaut

*Spec validée le 23 septembre 2026. Complète la spec du socle (`2026-09-22-customnotch-design.md`), qui reste
l'autorité pour tout ce qui n'est pas redit ici. Plan d'implémentation : `docs/superpowers/plans/`.*

Trois demandes : **tout se règle par l'interface** (plus besoin d'ouvrir `cells.json`), **les métriques du PC
dans un seul groupe** par défaut, et **une source média** (lecture en cours, play/pause). Claude Code, ClickUp,
l'updater et l'installateur restent au plan 3.

---

## 1. Décisions

| Sujet | Décision (et pourquoi) |
|---|---|
| Forme de la fenêtre | **Sidebar + pages** (patron AutoSort), page « Pilules & cellules » en **maître-détail** avec formulaires générés depuis `SourceSchema`. Une source nouvelle apparaît dans l'UI sans écran dédié. Un assistant pas-à-pas ou un éditeur JSON intégré ont été écartés (deux chemins d'édition, ou l'inverse de « tout via l'UI »). |
| Quand ça s'applique | **Immédiatement**, à chaque changement (anti-rebond 300 ms sur les champs texte) — un seul bouton « Fermer », comme dans ClickUp-Extended et AutoSort. La pilule se met à jour par le rechargement à chaud déjà en place. |
| Qui écrit les fichiers | `ConfigEditor` (Core), seule porte d'écriture de l'UI ; relit les documents à chaque opération, écrit atomiquement, recharge, **annule** si la validation refuse. |
| Commentaires de `cells.json` | Perdus à la première sauvegarde par l'UI (System.Text.Json ne les conserve pas) ; un en-tête fixe de deux lignes est réécrit. Assumé : le fichier n'est plus fait pour être édité à la main. |
| Secrets | Un champ de type `secret` écrit la valeur dans `secrets.json` (DPAPI) sous `<cellId>.<champ>` et met `${secret:<cellId>.<champ>}` dans `params`. Le fichier partagé ne contient jamais un secret. |
| Action au clic | `SourceSchema.DefaultAction` (`open` pour le lanceur, `toggle` pour le média) remplace le cas particulier `launcher` codé dans `PillWindow`. |
| Média | Session média **système** (`Windows.Media.Control`) derrière une interface `IMediaSession` dans Core, implémentée dans App (`Core` reste sans dépendance Windows). Clic = play/pause. |
| Groupe par défaut | `Système` = cpu, mémoire, disque, réseau, batterie (enfants masqués), tête cpu ; puis `Média`, puis le lanceur ClickUp. |
| Glisser-déposer | Non : ↑ ↓ suffisent (YAGNI). |
| Glyphes | Galerie des glyphes nommés de `GlyphLibrary` + champ « tracé SVG » (déjà accepté par le socle). |

---

## 2. Core — édition de configuration et schéma

### 2.1 `ConfigEditor` (`Core/Config/ConfigEditor.cs`)

```csharp
sealed class ConfigEditor(ConfigStore store)
{
    string AddPill(string edge);                                   // id « pill-N », fichier partagé
    void   RemovePill(string pillId);                              // partagé + entrée locale
    void   SetPillShared(string pillId, Action<JsonObject> mutate);   // edge, scale
    void   SetPillLocal (string pillId, Action<JsonObject> mutate);   // along, screen, visible (ConfigStore.SetPillLocal)
    void   MovePill(string pillId, int delta);
    string AddCell(string pillId, string sourceType);              // id « <type>-N » (« http-2 »), params = défauts du schéma
    void   RemoveCell(string cellId);                              // retire aussi l'id des children/headline des groupes
    void   SetCell(string cellId, Action<JsonObject> mutate);      // label, glyph, kind, refresh, thresholds, params, actions, children, headline
    void   MoveCell(string cellId, int delta);
    void   MoveCellToPill(string cellId, string pillId);
    void   SetCellVisible(string cellId, bool visible);            // fichier local, comme la position
    void   SetSecret(string name, string value);  void RemoveSecret(string name);
    void   SetSourceGlobal(string sourceType, Action<JsonObject> mutate);   // cells.json → "sources": { type: {…} }
}
```

- Chaque méthode : lit le document concerné (`CellsJson.Parse`), mute, `Json.WriteAtomic`, `store.Load()`. Si `Load()`
  rend false, le document précédent est réécrit et `ConfigException` remonte avec `store.LastErrors` joints — l'UI affiche
  le message, la valeur reste dans le champ.
- Le fichier partagé est réécrit avec `CellsJson.Options` (indenté), précédé de :
  `// customNotch — cells.json édité par la fenêtre Réglages (les commentaires manuels ne sont pas conservés).`
  `// Placeholders : ${env:NAME}, ${secret:name}, ${home}. Surcharge locale : cells.<machine>.json.`
- `SetCellVisible` écrit `visible` dans la surcharge locale : masquer une cellule est un choix de poste, comme sa position.
  `AddCell` crée la cellule visible.
- Ids générés : le type sans le préfixe de namespace (`system.cpu` → `cpu`), suffixé `-2`, `-3`… si pris.

### 2.2 Schéma des sources (`Sources/ISource.cs`)

- `SchemaField(Name, Type, Label, Required, Help, Default, Choices, Group)` ; types : `string | number | bool | secret |
  path | url | choice`. `Group` = section du formulaire (« Connexion », « Affichage »…), null = section par défaut.
- `SourceSchema(Type, Title, Fields, DefaultGlyph, Description, DefaultAction)`.
- **Validation typée** : `ConfigValidation.Validate` reçoit les schémas (`IReadOnlyDictionary<string, SourceSchema>`) et
  vérifie, pour chaque cellule non-groupe, les champs `Required` présents et les types (`number` → nombre, `bool` → booléen,
  `url` → URI absolue http(s), `choice` → une des valeurs). Un `${…}` non résolu vaut chaîne vide : un champ `Required` à
  placeholder manquant est signalé (« secret manquant »).
- `PillWindow.OnCellClicked` : `config.click` → sinon `Schema.DefaultAction` → sinon la carte. Décision extraite en fonction
  pure `CellClickResolver.Resolve(CellConfig, SourceSchema?) → ClickPlan { ActionConfig? Config; string? SourceAction; bool OpenCard }`.

### 2.3 `Caption` pour un texte

`CellViews.Caption` : une lecture sans valeur mais avec `Text` (statut/média) rend le texte tronqué à **10 caractères + « … »**,
que la cellule affiche en 11 px (au lieu de 15) — le média a ainsi un titre sous sa cellule.

---

## 3. App — la fenêtre Réglages (`App/Settings/`)

- **`SettingsWindow`** : 1040 × 720 (min 900 × 600), sidebar 220 px (`Pilules & cellules`, `Sources`, `Général`), page
  à droite, barre du bas avec « Fermer ». `Theme.ApplyChrome` pour la barre de titre. **Une seule instance** : le tray, le
  menu de la pilule et le `show` de `SingleInstance` ouvrent ou ramènent la même fenêtre (`Controller.ShowSettings`).
- **`PageBase`** : titre, sous-titre, rangées (repris d'AutoSort). Les pages construisent leurs contrôles en C# avec `Ui.cs` ;
  elles lisent `ConfigStore.Current` et s'abonnent à `ConfigStore.Changed` (Dispatcher) pour se rafraîchir si le fichier
  change par ailleurs, en préservant la sélection.

### 3.1 Page « Pilules & cellules »

- **Gauche (280 px)** : arbre pilule → cellules (`TreeView` stylé par la palette). Une cellule masquée est grisée ; les
  enfants d'un groupe sont indentés sous lui (et n'apparaissent qu'une fois). Barre d'outils : **+ Pilule**, **+ Cellule**
  (catalogue : liste `Titre — Description` des `SourceSchema`, filtrable), **↑ ↓**, **Masquer / Afficher**, **Supprimer**
  (confirmation ; supprimer une pilule supprime ses cellules).
- **Droite — pilule** : bord (4 boutons radio), écran (liste `Screen.AllScreens` + « principal »), position le long du bord
  (curseur 0–100 %), échelle (curseur 0,5–2 par pas de 0,1), visible.
- **Droite — cellule**, quatre sections :
  - **Source** : type et description, bouton « Changer… » (catalogue ; les params incompatibles sont abandonnés), puis le
    **formulaire généré** par `SchemaForm`.
  - **Affichage** : libellé ; glyph = galerie (grille de 40 px, chaque glyph rendu, nom en infobulle) + champ « tracé SVG » ;
    type de rendu `auto | anneau | valeur | statut | sparkline` ; cadence (`combo` 1 s, 2 s, 5 s, 30 s, 1 min, 5 min, 1 h +
    champ libre) ; seuils warn / crit (nombres, vides = défaut) + « inversé (bas = mauvais) ».
  - **Actions** : clic = `aucune (carte) | par défaut de la source | ouvrir… | commande… | action de la source` ; boutons de
    carte = liste éditable (libellé, icône dans la galerie, même choix d'action), ↑ ↓, supprimer.
  - **Groupe** : cases à cocher sur les autres cellules de la pilule ; tête = combo parmi les cochées ; cocher un enfant
    propose « Masquer cet enfant de la pilule ? » (oui par défaut).
- **`SchemaForm.Build(SourceSchema, JsonObject params, Action<string, JsonNode?> onChange) → UIElement`** : une rangée par
  champ, groupées par `Group` ; contrôle par type : `string` → `TextBox`, `number` → `TextBox` numérique (validation
  locale), `bool` → `CheckBox`, `choice` → `ComboBox`, `path` → `TextBox` + « … » (`OpenFileDialog`), `url` → `TextBox`
  avec validation, `secret` → `PasswordBox` + état « défini » + « Effacer ». `SchemaForm.ControlKind(SchemaField)` est pur
  et testé.
- Toute modification → `ConfigEditor` (anti-rebond 300 ms sur le texte). `ConfigException` → bandeau rouge en tête de
  l'éditeur, valeur conservée dans le champ.

### 3.2 Page « Sources »

Une section par type de source ayant des réglages globaux (aucun dans ce plan ; Claude et ClickUp au plan 3 — la page
existe, vide avec une phrase) et la **liste des secrets** : nom, « défini » (pas la valeur), Effacer.

### 3.3 Page « Général »

Emplacement de `cells.json` (chemin, « Parcourir… », « Ouvrir le dossier », mention « prise en compte au redémarrage »),
démarrage automatique (`Autostart` repris de `ClickUp-Extended\Platform.cs` : clé Run — la tâche planifiée vient avec
l'installateur), thème `système | clair | sombre` (`config.json → appearance.theme`, `Theme.Apply` immédiat), « Ouvrir le
journal », version, lien vers le dépôt.

---

## 4. Média

- **`IMediaSession`** (Core, `Sources/Media/IMediaSession.cs`) : `MediaState? Current()` (`Title, Artist, App, Playing`),
  `Task ToggleAsync()`, `Task NextAsync()`, `Task PreviousAsync()`, `event Action? Changed`.
- **`WindowsMediaSession`** (App, `Platform/WindowsMediaSession.cs`) : `GlobalSystemMediaTransportControlsSessionManager`
  (WinRT, TFM `net10.0-windows10.0.19041.0`) — session courante, `MediaPropertiesChanged`/`PlaybackInfoChanged`/
  `CurrentSessionChanged` → `Changed`. Injectée par `Controller` : `CoreSources.Build(mediaSession)` ; sans implémentation
  (tests, autre OS) la source rend `Off` / « Aucune lecture ».
- **`MediaSource`** (type `media`) : `Reading(Text: "Titre — Artiste", Status: Busy si lecture, Off sinon, Detail: [app
  source], Actions: [prev « Précédent », toggle « Lecture/Pause », next « Suivant »])` ; `DefaultRefresh` 5 s ; `Pushed` sur
  `Changed` ; `DefaultAction = "toggle"` ; glyph par défaut `music`.

---

## 5. Défauts et migration

`DefaultCells.Json()` :
```
main (right, 0.5) :
  sys      « Système », glyph cpu, children [cpu, mem, disk, net, battery], headline cpu
  cpu / mem / disk(C:) / net / battery    visible: false
  media    « Média », glyph music
  clickup  lanceur « ClickUp » → https://app.clickup.com
```
Le fichier n'est créé qu'en son absence : un `cells.json` existant n'est pas touché. Pour ce poste, l'ancien défaut est
supprimé au moment de tester.

---

## 6. Erreurs, tests, docs

- Erreurs : `ConfigEditor` n'applique jamais à moitié ; l'UI n'écrit jamais un secret ailleurs que dans `secrets.json` ;
  une exception WinRT dans `WindowsMediaSession` est journalisée et la source passe `Off` (pas de stale : rien n'était lu).
- Tests Core : `ConfigEditorTests` (ajout / suppression / déplacement, routage partagé-local-secret, annulation sur refus,
  nettoyage des `children`), `ConfigValidationTests` (params typés, `Required`, placeholder manquant), `CellClickResolverTests`,
  `MediaSourceTests` (session factice), `CellViewTests` (légende texte tronquée). Tests App : `SchemaFormTests`
  (type → contrôle, valeurs initiales, secret masqué).
- Vérification à l'écran : thème clair et sombre ; ajouter une cellule `http` de bout en bout depuis le catalogue ; un secret
  chiffré dans `secrets.json` et absent de `cells.json` ; le média avec Spotify ou un onglet YouTube ; le groupe Système.
- Docs : ARCHITECTURE (Réglages, média, `ConfigEditor`, schéma), README (« tout se règle dans Réglages… »), CHANGELOG 0.2.0.

---

## 7. Hors périmètre (plan 3)

Claude Code (usage, activité, hooks), ClickUp via ClickUp-Extended, updater, installateur Inno Setup, glisser-déposer dans
l'arbre, position de lecture du média, réglages globaux de sources (la page existe, vide).
