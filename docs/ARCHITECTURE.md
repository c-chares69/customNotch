# customNotch — Conception technique

Une pilule noire aux coins inversés, ancrée sur un bord d'écran, dont chaque cellule montre
d'un coup d'œil une source — CPU, mémoire, batterie, une commande, un point JSON d'une API… —
et dont le survol ouvre une carte de détail avec des actions. Troisième app de la famille
ClickUp-Extended / AutoSort, dont elle reprend le stack et les briques (`Theme`, `Ui`,
`Glyphs`, `Json`, `Log`, `Secrets`). Ce document décrit l'architecture livrée par le plan 1
(le socle : pilules, cellules, sources système et génériques) ; le code du dépôt l'implémente.
La conception détaillée et les décisions initiales sont dans
`docs/superpowers/specs/2026-09-22-customnotch-design.md`.

---

## 1. Stack

| Besoin | Choix | Pourquoi |
|---|---|---|
| Langage | C# 14 / .NET 10 (`global.json`, `rollForward: latestFeature`) | Même famille que ClickUp-Extended et AutoSort ; un exécutable, démarrage instantané. |
| Interface | WPF (fenêtres transparentes, sans bordure) + WinForms pour `NotifyIcon`/`Screen` seulement | Une fenêtre layered laisse passer les clics sur ses pixels transparents : le click-through de la pilule est natif, sans code de hit-test. |
| Cible (TFM) | `CustomNotch.Core` : `net10.0` nu · `CustomNotch.App` : `net10.0-windows10.0.19041.0` | Le cœur reste portable (aucune dépendance Windows hors `Platform/`, derrière `OperatingSystem.IsWindows()`) ; l'app cible Windows 10 1903+ pour le DPI par moniteur et l'accès WinRT du plan 2 (média). |
| HTTP | `HttpClient` (source `http`) | Un client statique et réutilisable (`HttpSource.Http`), remplaçable dans les tests. |
| Secrets | DPAPI (`ProtectedData`), préfixe `dpapi:` | Même format que ClickUp-Extended : un blob copié sur une autre machine ou un autre compte se lit vide plutôt que de planter. |
| Persistance | JSON (`System.Text.Json`), écriture atomique (tmp + remplacement, `Json.WriteAtomic`) | `cells.json` tolère commentaires et virgules finales (`JsonCommentHandling.Skip`) : c'est un fichier qu'on édite à la main. |
| Configuration | Trois fichiers : `cells.json` (portable), `cells.<machine>.json` (surcharge locale), `secrets.json` | Ce que l'utilisateur règle est synchronisable ; ce que l'app décide seule (position, écran) et les secrets ne le sont jamais. |
| Instance unique | Port TCP local dérivé du dossier de données (`SingleInstance`) | Même mécanisme que ClickUp-Extended ; une seconde copie lancée à la main réveille la première. |
| Tests | xUnit (`tests/CustomNotch.Core.Tests`, `tests/CustomNotch.App.Tests`) | `Core` est sans interface : tout se teste sans écran. `App` expose ses calculs de rendu (forme, placement, contenu de carte) en fonctions pures, testées sans fenêtre. |
| Hook Claude Code | `CustomNotch.Hook.exe` (console, sans dépendance) | Placeholder en plan 1 (`return 0;`) ; le contrat (`HttpListener` local, port 48667) est posé par la spec et rempli au plan 2. |

---

## 2. Arborescence

```text
customNotch/
├── CustomNotch.sln, Directory.Build.props, global.json
├── src/
│   ├── CustomNotch.Core/              # le cœur : sans interface, sans dépendance Windows (sauf Platform/)
│   │   ├── App.cs                     # identité : nom, AppUserModelID, version (Directory.Build.props), variable de dossier de données
│   │   ├── Paths.cs                   # dossier de données : CUSTOMNOTCH_HOME, mode portable, %APPDATA%, chemins des trois fichiers
│   │   ├── Json.cs                    # mise en forme JSON commune, écriture atomique (tmp + remplacement, réessais)
│   │   ├── Log.cs                     # journal.log + errors.log, caviardage des tokens (Bearer, pk_, sk-, ya29.)
│   │   ├── Secrets.cs                 # chiffrement DPAPI (préfixe dpapi:), entropie liée à l'application
│   │   ├── Config/
│   │   │   ├── AppConfig.cs           # config.json : état d'app (chemin de cells.json, thème, langue), clés à points
│   │   │   ├── CellsFile.cs           # le modèle de configuration : PillConfig, CellConfig, ActionConfig, CellsFile
│   │   │   ├── CellsJson.cs           # lecture tolérante (commentaires, virgules finales, casse indifférente), sérialisation
│   │   │   ├── ConfigMerge.cs         # cells.<machine>.json par-dessus cells.json, fusion par « id »
│   │   │   ├── ConfigStore.cs         # cycle de vie des trois fichiers : lecture, fusion, placeholders, validation, surveillance
│   │   │   ├── ConfigValidation.cs    # une configuration fausse est refusée en bloc, avec le chemin exact du champ
│   │   │   ├── DefaultCells.cs        # le cells.json du premier lancement
│   │   │   ├── Placeholders.cs        # ${env:NAME}, ${secret:name}, ${home}, résolus dans toutes les chaînes
│   │   │   └── SecretsFile.cs         # secrets.json, jamais synchronisé
│   │   ├── Model/
│   │   │   ├── CellView.cs            # ce que l'interface dessine : dérivation du genre, du statut, de la fraction, des groupes
│   │   │   ├── Reading.cs             # ce qu'une source produit : Value, Max, Unit, Text, Detail, Actions, History, StaleSinceMs
│   │   │   ├── Status.cs              # Status (Ok, Warn, Crit, Off, Busy, Attention), CellKind (Ring, Value, Status, Sparkline, Group)
│   │   │   └── Thresholds.cs          # seuils warn/crit sur la valeur (ou le %), invert pour les grandeurs où bas = mauvais
│   │   ├── Sources/
│   │   │   ├── ISource.cs             # le contrat ISource, CellContext (params + réglages globaux), SourceSchema
│   │   │   ├── SourceRegistry.cs      # les sources enregistrées, par type
│   │   │   ├── Scheduler.cs           # une boucle par cellule, backoff exponentiel, ralenti à l'inactivité, Pushed
│   │   │   ├── ReadingStore.cs        # la dernière lecture de chaque cellule (ConcurrentDictionary), événement Changed
│   │   │   ├── CoreSources.cs         # assemble le registre livré avec le cœur (système + http + shell + launcher)
│   │   │   ├── HttpSource.cs          # URL + extraction JSON (JsonPath), en-têtes avec ${secret:…}
│   │   │   ├── ShellSource.cs         # une commande (cmd.exe /c), sa sortie lue comme nombre, JSON ou texte
│   │   │   ├── LauncherSource.cs      # pas de donnée : un glyph et une action « open »
│   │   │   ├── JsonPath.cs            # « data.items[0].n » : le strict nécessaire pour pointer une valeur dans un JSON
│   │   │   └── System/
│   │   │       ├── SystemSources.cs   # enregistre les cinq sources système
│   │   │       ├── CpuSource.cs       # % d'occupation (GetSystemTimes), historique 60 points
│   │   │       ├── MemorySource.cs    # Go utilisés / total (GlobalMemoryStatusEx)
│   │   │       ├── DiskSource.cs      # Go utilisés / total d'un lecteur, action « open » (l'explorateur)
│   │   │       ├── NetworkSource.cs   # débit total (Ko/s ou Mo/s), historique, détail ↓ réception / ↑ émission
│   │   │       ├── BatterySource.cs   # %, secteur ou temps restant (GetSystemPowerStatus)
│   │   │       └── Units.cs           # conversions : octets → Go, débit lisible, % CPU depuis deux relevés
│   │   ├── Actions/
│   │   │   └── ActionRunner.cs        # les trois actions d'une cellule : open (URL/fichier/app), shell, source
│   │   └── Platform/
│   │       ├── Idle.cs                # millisecondes depuis la dernière frappe ou le dernier mouvement souris
│   │       ├── SingleInstance.cs      # port local dérivé du dossier de données : verrou + canal (show, ping)
│   │       └── SystemInfo.cs          # les appels Win32 bruts : temps CPU, mémoire, alimentation
│   ├── CustomNotch.App/               # l'application WPF (+ WinForms pour NotifyIcon/Screen seulement)
│   │   ├── app.manifest               # DPI par moniteur
│   │   ├── App.xaml / App.xaml.cs     # styles globaux, ligne de commande (--version, --home), instance unique, exception non gérée
│   │   ├── Controller.cs              # assemblage et cycle de vie : config → ordonnanceur → pilules, tray, tic d'une seconde
│   │   ├── IPillHost.cs               # ce qu'une PillWindow demande au contrôleur (vues, actions, position) — testable avec un hôte factice
│   │   ├── TrayIcon.cs                # icône de la zone de notification (dessinée au lancement), menu, bulle de notice
│   │   ├── Styles.xaml                # styles WPF partagés (boutons de la carte…)
│   │   ├── Notch/
│   │   │   ├── PillMetrics.cs         # les dimensions : tout le dessin part d'ici
│   │   │   ├── PillShape.cs           # la silhouette : coins arrondis + fillets inversés, retournée selon le bord
│   │   │   ├── EdgePlacement.cs       # position sur l'écran depuis edge / along / la zone de travail (fonctions pures)
│   │   │   ├── FullScreenDetector.cs  # la fenêtre au premier plan couvre-t-elle l'écran de la pilule ?
│   │   │   ├── PillWindow.cs          # la fenêtre : layout, position, drag, carte, menu contextuel, visibilité
│   │   │   ├── HoverCard.cs           # la carte de détail, dessinée dans la même fenêtre que la pilule
│   │   │   └── CardContent.cs         # le contenu de la carte, calculé sans WPF (CardModel) — testable sans écran
│   │   ├── Cells/
│   │   │   ├── CellFace.cs            # base commune : glyph au centre, arc d'activité (Busy tourne, Attention pulse)
│   │   │   ├── CellHost.cs            # une cellule dans la pilule : face + légende, pression au clic (scale .93)
│   │   │   ├── GlyphLibrary.cs        # les glyphes par nom (dont le logo Claude, repris de codenotch)
│   │   │   ├── RingArc.cs             # l'arc d'un anneau, de 0 à 100 %
│   │   │   ├── RingCell.cs            # anneau + glyph (system.cpu, system.memory, system.disk, system.battery…)
│   │   │   ├── ValueCell.cs           # disque + glyph, la valeur est dans la légende sous la cellule
│   │   │   ├── StatusCell.cs          # glyph + pastille de statut (launcher, une cellule sans valeur)
│   │   │   ├── SparklineCell.cs       # les 30 derniers points à l'échelle du maximum observé (system.network)
│   │   │   └── StatusPalette.cs       # les couleurs fixes de la pilule : son identité, indépendante du thème
│   │   └── Shared/                    # repris de ClickUp-Extended / AutoSort, non modifiés
│   │       ├── Glyphs.cs              # les icônes tracées des boutons (lecture, pause, coche, crayon…)
│   │       ├── Native.cs              # GetWindowLong/SetWindowLong (WS_EX_NOACTIVATE | TOOLWINDOW)
│   │       ├── Screens.cs             # écrans et conversions pixels physiques → unités WPF
│   │       ├── Theme.cs               # palettes claire/sombre, accent système, chrome de fenêtre sombre
│   │       └── Ui.cs                  # les briques communes : pastille, bouton, ligne de formulaire (pour Settings, plan 2)
│   └── CustomNotch.Hook/
│       └── Program.cs                 # placeholder (`return 0;`) : le client des hooks Claude Code vient au plan 2
├── tests/
│   ├── CustomNotch.Core.Tests/        # xUnit : configuration, modèle, sources, chemins — tout sans écran
│   └── CustomNotch.App.Tests/         # xUnit : les fonctions pures de rendu (forme, placement, glyphes, contenu de carte)
├── docs/
│   ├── ARCHITECTURE.md, cells.example.json, apercu-pilule.png
│   └── superpowers/{specs,plans}/     # la spec validée, le plan d'implémentation
└── LICENSE, NOTICE.md, README.md, CHANGELOG.md
```

Données utilisateur (hors dépôt) : `%APPDATA%\customNotch\config.json`, `cells.<machine>.json`,
`secrets.json`, `logs\journal.log`, `logs\errors.log`. `cells.json`, lui, est où l'utilisateur
le décide (`cells_path` dans `config.json`) — par défaut ce même dossier, typiquement un
dossier synchronisé entre machines. Surcharge : `--home <dossier>`, variable
`CUSTOMNOTCH_HOME`, ou mode portable (dossier `data\` avec un fichier vide `portable` à côté
de l'exe).

---

## 3. Flux de données

```text
cells.json + cells.<machine>.json + secrets.json
                    │  ConfigStore.Load() : lecture, fusion (ConfigMerge), placeholders, validation
                    ▼
              ConfigStore.Current (CellsFile)
                    │  Changed(file) → Controller.ApplyConfig
                    ▼
   Scheduler.Apply(file) ── ReadAsync ──► Sources (system.*, http, shell, launcher)
        │  une boucle par cellule, à sa cadence                │
        │  Set(cellId, Reading)                                │ Pushed(cellId) — hors cadence
        ▼                                                      │
   ReadingStore ── Changed(cellId) ──────────────────────────────► Controller (Dispatcher WPF)
                                                                        │  View(cellId) = CellViews.From / FromGroup
                                                                        ▼
                                                                  PillWindow
                                                                    ├─► CellHost (face + légende)
                                                                    └─► HoverCard (carte de détail, au survol)
```

- **Un sens pour la lecture** : chaque source tourne dans sa propre boucle (`Scheduler`),
  écrit dans `ReadingStore`, qui prévient le `Controller` — toujours sur le `Dispatcher` WPF,
  jamais depuis le thread de la source. `Controller.View` recalcule la `CellView` à la volée à
  partir de la dernière config et de la dernière lecture : rien n'est mis en cache côté
  interface.
- **Les actions remontent en sens inverse** : un clic sur une cellule ou un bouton de la carte
  appelle `IPillHost.RunActionAsync` / `InvokeSourceAsync` (`Controller`), qui délègue à
  `ActionRunner` (open, shell) ou à `Scheduler.InvokeAsync` (action de la source elle-même,
  ex. `launcher.open`, `system.disk.open`) — suivi d'un `RefreshNow` pour que la cellule se
  redessine tout de suite.
- **Le drag écrit la surcharge locale** : `PillWindow` calcule `along`/`screen` pendant le
  glisser (fonctions pures d'`EdgePlacement`), puis `IPillHost.SavePosition` appelle
  `ConfigStore.SetPillLocal`, qui écrit `cells.<machine>.json` puis recharge — le
  `FileSystemWatcher` referait le même travail si l'écriture venait d'ailleurs (un éditeur, une
  synchro).
- **Rien ne bloque l'interface** : les sources sont asynchrones (`Task`), l'ordonnanceur tourne
  hors du thread WPF, et tout ce qui touche une fenêtre repasse par
  `Application.Current.Dispatcher` (`Controller.Ui`).

---

## 4. Le contrat Reading / CellView

Chaque source rend un `Reading` — la seule chose que l'interface connaît :

```csharp
sealed record Reading(
    double? Value, double? Max, string? Unit, string? Text, Status? Status,
    IReadOnlyList<DetailRow>? Detail, IReadOnlyList<ActionSpec>? Actions,
    IReadOnlyList<(long Ms, double V)>? History, long? StaleSinceMs, string? Error);
```

`CellViews.From(cell, reading, nowMs)` en déduit une `CellView` (genre, statut, légende,
fraction) sans aucune dépendance WPF — c'est ce que `CellHost`/`CellFace` dessinent, et ce que
`tests/CustomNotch.Core.Tests/Model` vérifie sans écran.

- **Genre de rendu (`DeriveKind`), dans cet ordre** :
  1. **`children` présent ⇒ groupe**, toujours — même si un `kind` a été forcé par erreur dans
     la config. Une cellule-groupe est structurelle : sa carte liste ses enfants, elle n'a rien
     d'autre à décider.
  2. sinon le **`kind` explicite** de la config (`ring`, `value`, `status`, `sparkline`) s'il
     est posé ;
  3. sinon déduit de la lecture : **`ring` si `Max` et `Value` sont présents — même avec un
     historique** (le CPU a les deux ; l'anneau l'emporte sur la sparkline) ; **`sparkline`**
     si `History` a plus d'un point et pas de maximum (le réseau) ; **`value`** si `Value` seul ;
     sinon **`status`**.
- **Statut (`DeriveStatus`)** : si la source fixe `Status`, il fait autorité (Busy/Attention
  viennent toujours de la source). Sinon, sans `Value` ⇒ `Off` ; avec `Value`, la grandeur jugée
  est le pourcentage `Value/Max × 100` si `Max` est présent, sinon `Value` brut, comparée aux
  `thresholds` de la cellule ou, à défaut, à `Thresholds.RingDefault` (**warn 50, crit 75**)
  *si* la lecture a un maximum — sans maximum et sans seuils déclarés, le statut reste `Ok`.
  `invert: true` retourne la comparaison (bas = mauvais : la batterie).
- **Fraction et légende** : `Fraction` = `Value/Max` borné à `[0, 1]` (`null` sans maximum).
  `Caption` (le texte sous la cellule) : `« 73% »` pour un anneau, la valeur formatée (culture
  `fr-FR`, espace insécable normalisée) suivie de l'unité pour une valeur ou une sparkline,
  rien pour un statut.
- **Cellule-groupe (`FromGroup`)** : le statut du groupe est le **pire** de ses enfants, dans
  l'ordre Attention > Crit > Busy > Warn > Ok > Off (`Rank`). L'anneau affiché est celui de
  l'enfant `headline` s'il est déclaré, sinon le pire enfant s'il a une fraction, sinon le
  premier enfant qui en a une, sinon le pire tout court. Le groupe n'est **périmé que si tous
  ses enfants le sont** (l'âge affiché est alors celui du plus ancien).
- **Sources par cellule** : une instance de source sert toutes ses cellules ; l'état propre à
  chacune (delta CPU, historique réseau…) est gardé dans un dictionnaire par `cellId`, jamais
  dans des champs d'instance partagés.

---

## 5. Sources

Les huit sources livrées avec ce plan (`CoreSources.Build`) ; `claude`, `media` et `clickup`
sont des sous-projets du plan 2 (§8).

| Source | Params | Cadence par défaut | Lecture | Actions |
|---|---|---|---|---|
| `system.cpu` | — | 2 s | `Value` % (0-100, arrondi), `Max` 100, `Detail` (occupation), `History` (60 points) | — |
| `system.memory` | — | 5 s | `Value`/`Max` en Go, `Detail` (utilisée, libre) | — |
| `system.disk` | `drive` (défaut « C: ») | 1 min | `Value`/`Max` en Go, `Detail` (utilisé/total, libre) | `open` : ouvre le lecteur dans l'explorateur |
| `system.network` | `iface?` (vide = toutes les interfaces actives) | 2 s | `Value` = débit total (Ko/s sous 1 Mo/s, Mo/s au-dessus), `Unit`, `Detail` (↓ réception, ↑ émission), `History` | — |
| `system.battery` | — | 30 s | `Value` %, `Max` 100 ; `Status.Off` sans batterie ; `Ok` sur secteur, sinon seuils 20 %/10 % inversés ; `Detail` (charge, secteur ou temps restant) | — |
| `http` | `url`\*, `method` (GET/POST), `path`, `textPath`, `max?`, `unit?`, `body?`, `headers{}` | 1 min | `Value`/`Text` extraits du JSON par `path`/`textPath` (`JsonPath` minimal : « data.items[0].n ») ; `Max`/`Unit` s'ils sont fournis ; `Detail` d'une ligne | — |
| `shell` | `command`\*, `parse` (number / json / text), `path?` (si json), `max?`, `unit?`, `timeoutSeconds` (5) | 1 min | selon `parse` : un nombre (+ `Max`/`Unit`), un nœud JSON pointé, ou le texte de sortie (`Detail`) | — |
| `launcher` | `open`\* | 24 h | `Status.Off`, `Text` = la cible | `open` : ouvre la cible (URL, chemin, application) |

\* champ requis. Chaque source déclare un `SourceSchema` (champs, type, aide) — c'est ce que la
fenêtre Settings du plan 2 transformera en formulaire ; en attendant, il documente les params
acceptés et sert à `ConfigValidation`.

`http` et `shell` sont l'échappatoire universelle : n'importe quelle API ou commande devient une
cellule sans écrire de code. `headers` de `http` porte l'authentification via
`${secret:nom}` — jamais un secret en clair dans `cells.json`.

---

## 6. Fenêtre et rendu

- **`PillMetrics`** — tout le dessin part d'ici, à l'échelle `scale` de la pilule : largeur
  **70 px**, coins libres **20 px**, fillets inversés **38,7 px**, anneau **44 px**, légende
  **15 px** (semi-gras, hauteur réservée 18 px), écart entre cellules 14 px, padding interne de
  la pilule 18 px de chaque côté du corps. Carte : largeur max **246 px**, rayon **16 px**,
  padding **16 px**, queue **32 × 36 px**.
- **`PillShape.Build`** dessine la silhouette pour le bord droit (un `StreamGeometry` : le
  corps aux coins arrondis côté libre, et deux fillets inversés côté écran — le carré au-dessus
  du corps moins un quart de cercle, comme le `::before` de codenotch), puis applique une
  `MatrixTransform` pour les trois autres bords (miroir à gauche, rotations à `top`/`bottom`).
  Liseré 1 px `#2e2e2e`, fond `#000` — la pilule reste toujours sombre, c'est son identité,
  indépendante du thème de l'application.
- **`EdgePlacement`** (fonctions pures, testées sans fenêtre) place le rectangle de la pilule
  contre le bord demandé, à `along` de la longueur disponible (`aire − extent`) ; `AlongFrom`
  fait le calcul inverse pendant un drag. `PillWindow.Reposition` résout l'écran nommé
  (`Pill.Screen`, ou l'écran principal), convertit sa zone de travail en DIP (`Screens.ToDip`)
  et recalcule sur `SystemEvents.DisplaySettingsChanged`.
- **`PillWindow`** — une fenêtre par pilule : transparente, `Topmost`, `ShowActivated = false`,
  `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` posés après `SourceInitialized` (hors barre des tâches,
  hors Alt-Tab). Elle réserve, côté libre, la place de la carte et de sa queue : les deux sont
  dessinées **dans la même fenêtre** que la pilule, pour ne jamais gérer de z-order. Les
  cellules visibles sont réutilisées par id (`RebuildCells`) tant que leur source ne change pas,
  pour ne pas couper une animation en cours.
- **Drag** : une poignée apparaît au survol du bord libre de la pilule ; `OnDragMove` suit le
  curseur le long du bord de l'écran qui se trouve sous la souris (le drag peut changer
  d'écran) ; `OnDragEnd` **recalcule depuis la position réelle du curseur**, jamais depuis un
  état laissé par le dernier `MouseMove`, pour qu'un clic sans déplacement ou un survol rapide
  d'un autre écran juste avant le relâché ne fige pas une position ou un écran périmés. La
  position (`along`, `screen`) part alors dans `cells.<machine>.json`.
- **Carte hover** (`HoverCard` + `CardContent.Build`) : s'ouvre **150 ms** après l'entrée dans
  une cellule, se ferme **250 ms** après la sortie, reste ouverte tant que le pointeur y est
  (`CancelHide`/`HideLater`). Contenu calculé sans WPF : en-tête (glyph + label), une ligne par
  `Detail` (ou une seule ligne label/valeur si la source n'en donne pas), les enfants pour un
  groupe (label, légende, fraction, l'indication du premier `Detail`), les boutons de la config
  (`actions.card`) puis ceux déclarés par la source (`Reading.Actions`), une notice de
  péremption si la cellule est périmée. `PillWindow.PlaceCard` la centre sur la cellule côté
  libre, bornée à la fenêtre ; la queue chevauche la carte de 4 px pour ne laisser aucun jour.
- **Plein écran** (`FullScreenDetector`) : au tic d'une seconde (`Controller._tick`), la
  fenêtre au premier plan est comparée aux limites de l'écran de la pilule (le bureau et le
  shell — `Progman`, `WorkerW`, `Shell_TrayWnd` — ne comptent jamais). La visibilité de
  `PillWindow` a **un seul propriétaire** : `SetWanted` (ce que la config/le tray demandent) et
  `SetFullScreen` (ce que le plein écran autorise) alimentent tous deux `Refresh()`, qui seul
  appelle `Show`/`Hide` — sans ce partage, un rechargement de config pendant un plein écran
  ramenait la pilule par-dessus le jeu et le tic suivant ne la recachait pas.
- **Cellule** : anneau (`RingCell`), valeur (`ValueCell`), statut (`StatusCell`) ou sparkline
  (`SparklineCell`) — la face change si le genre déduit change (`CellHost.Render`). Glyph 26 px
  au centre d'un carré de 44 px (`Geometry`, glyphes codenotch convertis + `GlyphLibrary`),
  couleur de l'anneau par statut (`StatusPalette`) ; Busy dessine un arc fin qui tourne
  par-dessus (1,2 s/tour), Attention une pulsation ambre (opacité 1 → 0,25, 0,55 s,
  aller-retour) — communs aux quatre faces (`CellFace.Activity`). Pression au clic : échelle
  93 %, ressort 300 ms (`BackEase`).

---

## 7. Stratégie de gestion des erreurs

| Situation | Comportement |
|---|---|
| Une source échoue | Dernière lecture conservée, marquée périmée (`StaleSinceMs`) ; légende et anneau à 55 % d'opacité, notice dans la carte (« Lecture … · erreur ») ; journal `WARN` — jamais une cellule vide. |
| Échecs répétés d'une source | Backoff ×2 à chaque échec, plafonné à 10 min ; réinitialisé au premier succès. |
| Inactivité (> 5 min sans souris ni clavier) | Les cadences sous 30 s remontent à 30 s (`Scheduler.IdleFloor`) : pas de travail pour rien devant un écran éteint. |
| `cells.json` ou `cells.<machine>.json` invalide | Refusé **en bloc** (`ConfigValidation`), message avec le chemin exact du champ, notice tray ; la configuration précédente reste en service. |
| `cells.json` JSON malformé | `ConfigException` avec le message du parseur ; même refus, même repli. |
| Placeholder sans valeur (`${secret:x}` absent) | Résolu en chaîne vide, avertissement journal — ne bloque pas le chargement, contrairement à un champ hors norme. |
| `secrets.json` ou `config.json` illisible | Valeurs vides ou par défaut, avertissement journal, jamais de crash. |
| Fichier verrouillé à l'écriture | `Json.WriteAtomic` réessaie 6 fois avec délai croissant, puis abandonne proprement (avertissement) ; le fichier d'origine n'est jamais tronqué. |
| Deux instances | La seconde envoie `show` sur le port local dérivé du dossier de données et s'arrête ; la première répond (ouvre les réglages, en attendant la fenêtre du plan 2). |
| Exception non gérée sur le thread UI | `DispatcherUnhandledException` la journalise et la marque traitée : l'application continue. |
| Une action échoue (clic, bouton de carte) | Capturée, journalisée (catégorie « action ») ; l'utilisateur ne la voit aujourd'hui que dans le journal — un retour visible viendra avec Settings (plan 2). |
| Secrets | Jamais en clair sur disque (DPAPI, préfixe `dpapi:`) ; résolus en mémoire seulement (`Placeholders.Resolve` ne réécrit jamais `cells.json`). |

---

## 8. Ce qui vient ensuite

Hors périmètre de ce plan, posé par la spec (`docs/superpowers/specs/2026-09-22-customnotch-design.md` §6-§8, §12) :

- **Claude Code** : lecture du jeton (`~/.claude/.credentials.json`), usage (`GET …/oauth/usage`),
  renouvellement par `claude -p`, connexion (`claude auth login`), hooks installés dans
  `~/.claude/settings.json`, serveur d'événements local (`HttpListener`, port **48667**),
  `ActivityStore` (sessions, `running → done → dismissed`, `attention`).
- **Source média** : session média système (`Windows.Media.Control`, WinRT) — titre, lecture,
  play/pause pour Spotify, YouTube, VLC…
- **Source ClickUp** : endpoint local exposé par ClickUp-Extended (son propre sous-projet) ;
  repli en simple lanceur tant qu'il ne répond pas.
- **Fenêtre Settings** (sidebar + pages, `Ui.Form`) : Pilules, Cellules (formulaire généré par
  `SourceSchema`), Sources (réglages globaux, secrets), Claude, Général.
- **Mises à jour**, **diagnostic** (`--report`), **démarrage automatique**, **installateur**
  Inno Setup, **icône** `.ico`.
- **v1.1** : GPU et températures, détection plein écran affinée, plusieurs comptes Claude,
  export/import de `cells.json` depuis Settings.
