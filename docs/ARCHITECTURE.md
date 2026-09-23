# customNotch — Conception technique

Une pilule noire aux coins inversés, ancrée sur un bord d'écran, dont chaque cellule montre
d'un coup d'œil une source — CPU, mémoire, batterie, une commande, un point JSON d'une API,
ce qui joue… — et dont le survol ouvre une carte de détail avec des actions. Troisième app de
la famille ClickUp-Extended / AutoSort, dont elle reprend le stack, les briques (`Theme`, `Ui`,
`Glyphs`, `Json`, `Log`, `Secrets`) et le patron de livraison (Inno Setup, tâches planifiées,
manifeste de mise à jour). Ce document décrit l'architecture livrée par les versions 0.1.0 à
0.3.1 (le socle, la fenêtre Réglages, la source média, le groupe Système par défaut, la
livraison, la source `claude` et sa page Réglages, puis les mises à jour dans l'application et
le diagnostic `--report`) ; le code du dépôt l'implémente. La conception détaillée et les
décisions sont dans
`docs/superpowers/specs/2026-09-22-customnotch-design.md` (le socle),
`docs/superpowers/specs/2026-09-23-customnotch-settings-media-design.md` (Réglages, média),
`docs/superpowers/specs/2026-09-23-customnotch-claude-design.md` (0.3.0 : jeton, usage,
sessions, page Claude) et
`docs/superpowers/plans/2026-09-23-customnotch-mises-a-jour.md` (0.3.1 : mises à jour,
diagnostic).

---

## 1. Stack

| Besoin | Choix | Pourquoi |
|---|---|---|
| Langage | C# 14 / .NET 10 (`global.json`, `rollForward: latestFeature`) | Même famille que ClickUp-Extended et AutoSort ; un exécutable, démarrage instantané. |
| Interface | WPF (fenêtres transparentes, sans bordure) + WinForms pour `NotifyIcon`/`Screen` seulement | Une fenêtre layered laisse passer les clics sur ses pixels transparents : le click-through de la pilule est natif, sans code de hit-test. |
| Cible (TFM) | `CustomNotch.Core` : `net10.0` nu · `CustomNotch.App` : `net10.0-windows10.0.19041.0` | Le cœur reste portable (aucune dépendance Windows hors `Platform/`, derrière `OperatingSystem.IsWindows()`) ; l'app cible Windows 10 1903+ pour le DPI par moniteur et l'accès WinRT de la source média. |
| HTTP | `HttpClient` (source `http`) | Un client statique et réutilisable (`HttpSource.Http`), remplaçable dans les tests. |
| Secrets | DPAPI (`ProtectedData`), préfixe `dpapi:` | Même format que ClickUp-Extended : un blob copié sur une autre machine ou un autre compte se lit vide plutôt que de planter. |
| Persistance | JSON (`System.Text.Json`), écriture atomique (tmp + remplacement, `Json.WriteAtomic`) | `cells.json` tolère commentaires et virgules finales (`JsonCommentHandling.Skip`) à la lecture ; la fenêtre Réglages le réécrit sans eux (`ConfigEditor`), avec un en-tête fixe qui le dit. |
| Configuration | Trois fichiers : `cells.json` (portable), `cells.<machine>.json` (surcharge locale), `secrets.json` | Ce que l'utilisateur règle est synchronisable ; ce que l'app décide seule (position, écran) et les secrets ne le sont jamais. |
| Instance unique | Port TCP local dérivé du dossier de données (`SingleInstance`) | Même mécanisme que ClickUp-Extended ; une seconde copie lancée à la main réveille la première, qui ouvre ses réglages. |
| Tests | xUnit (`tests/CustomNotch.Core.Tests`, `tests/CustomNotch.App.Tests`) | `Core` est sans interface : tout se teste sans écran. `App` expose ses calculs de rendu (forme, placement, contenu de carte, `SchemaForm`) en fonctions pures, testées sans fenêtre. |

---

## 2. Arborescence

```text
customNotch/
├── CustomNotch.sln, Directory.Build.props, global.json
├── src/
│   ├── CustomNotch.Core/              # le cœur : sans interface, sans dépendance Windows (sauf Platform/)
│   │   ├── CustomNotch.Core.csproj    # cible net10.0 nu : doit rester portable, aucune dépendance Windows
│   │   ├── App.cs                     # identité : nom, AppUserModelID, version (Directory.Build.props), variable de dossier de données
│   │   ├── Paths.cs                   # dossier de données : CUSTOMNOTCH_HOME, mode portable, %APPDATA%, chemins des trois fichiers, Bureau (--report)
│   │   ├── Json.cs                    # mise en forme JSON commune, écriture atomique (tmp + remplacement, réessais)
│   │   ├── Log.cs                     # journal.log + errors.log, caviardage des tokens (Bearer, pk_, sk-, ya29.)
│   │   ├── Secrets.cs                 # chiffrement DPAPI (préfixe dpapi:), entropie liée à l'application
│   │   ├── Updates.cs                 # latest.json (manifeste), téléchargement vérifié SHA-256, setup silencieux ; UpdateChecker (0.3.1)
│   │   ├── Diagnostics.cs             # archive de diagnostic (--report) : journaux, configuration, informations système, jamais secrets.json (0.3.1)
│   │   ├── Config/
│   │   │   ├── AppConfig.cs           # config.json : état d'app (chemin de cells.json, thème, langue), clés à points
│   │   │   ├── CellsFile.cs           # le modèle de configuration : PillConfig, CellConfig, ActionConfig, CellsFile
│   │   │   ├── CellsJson.cs           # lecture tolérante (commentaires, virgules finales, casse indifférente), sérialisation
│   │   │   ├── ConfigEditor.cs        # seule porte d'écriture de la fenêtre Réglages : relit, mute, écrit, recharge, annule si refusé
│   │   │   ├── ConfigMerge.cs         # cells.<machine>.json par-dessus cells.json, fusion par « id »
│   │   │   ├── ConfigStore.cs         # cycle de vie des trois fichiers : lecture, fusion, placeholders, validation, surveillance
│   │   │   ├── ConfigValidation.cs    # une configuration fausse est refusée en bloc (chemin exact du champ) ; params typés d'après le schéma de la source
│   │   │   ├── DefaultCells.cs        # le cells.json du premier lancement : groupe Système, média, lanceur ClickUp
│   │   │   ├── Placeholders.cs        # ${env:NAME}, ${secret:name}, ${home}, résolus dans toutes les chaînes
│   │   │   └── SecretsFile.cs         # secrets.json, jamais synchronisé
│   │   ├── Model/
│   │   │   ├── CellClickResolver.cs   # ce que fait un clic : action de la config, sinon celle par défaut de la source, sinon la carte
│   │   │   ├── CellView.cs            # ce que l'interface dessine : dérivation du genre, du statut, de la fraction, des groupes, légende (dont le texte tronqué)
│   │   │   ├── Reading.cs             # ce qu'une source produit : Value, Max, Unit, Text, Detail, Actions, History, StaleSinceMs
│   │   │   ├── Status.cs              # Status (Ok, Warn, Crit, Off, Busy, Attention), CellKind (Ring, Value, Status, Sparkline, Group)
│   │   │   └── Thresholds.cs          # seuils warn/crit sur la valeur (ou le %), invert pour les grandeurs où bas = mauvais
│   │   ├── Sources/
│   │   │   ├── ISource.cs             # le contrat ISource, CellContext (params + réglages globaux), SchemaField (types, Group), SourceSchema (DefaultAction)
│   │   │   ├── SourceRegistry.cs      # les sources enregistrées, par type
│   │   │   ├── Scheduler.cs           # une boucle par cellule, backoff exponentiel, ralenti à l'inactivité, Pushed
│   │   │   ├── ReadingStore.cs        # la dernière lecture de chaque cellule (ConcurrentDictionary), événement Changed
│   │   │   ├── CoreSources.cs         # assemble le registre livré avec le cœur (système + http + shell + launcher + media)
│   │   │   ├── HttpSource.cs          # URL + extraction JSON (JsonPath), en-têtes avec ${secret:…}
│   │   │   ├── ShellSource.cs         # une commande (cmd.exe /c), sa sortie lue comme nombre, JSON ou texte
│   │   │   ├── LauncherSource.cs      # pas de donnée : un glyph et une action « open »
│   │   │   ├── JsonPath.cs            # « data.items[0].n » : le strict nécessaire pour pointer une valeur dans un JSON
│   │   │   ├── Claude/
│   │   │   │   ├── ClaudeCredentials.cs # .credentials.json (lecture seule), jeton masqué au journal
│   │   │   │   ├── UsageModel.cs      # LimitWindow, UsageBreakdown, UsageSnapshot
│   │   │   │   ├── UsageParser.cs     # limits[]/five_hour/seven_day → fenêtres dédoublonnées, libellés français
│   │   │   │   ├── UsageClient.cs     # GET oauth/usage, UsageException typée (RateLimited/Unauthorized/NoLimits/Network)
│   │   │   │   ├── Backoff.cs         # attente après 429, doublée, plafonnée, persistée (claude-backoff.json)
│   │   │   │   ├── TokenRenewal.cs    # claude -p, marge 4 min, un essai par jeton, cooldown doublé
│   │   │   │   ├── SessionRecord.cs   # un fichier ~/.claude/sessions/<pid>.json, parse tolérant
│   │   │   │   ├── SessionRegistry.cs # vivacité par pid + heure de démarrage, sessions terminées gardées 10 min
│   │   │   │   ├── ClaudeSource.cs    # type claude : assemble jeton, usage et sessions en une seule Reading
│   │   │   │   └── ClaudeStatus.cs    # l'instantané pour la page Réglages → Claude
│   │   │   ├── Media/
│   │   │   │   ├── IMediaSession.cs   # le contrat côté cœur : MediaState (Title, Artist, App, Playing), Toggle/Next/PreviousAsync, Changed
│   │   │   │   └── MediaSource.cs     # type media : titre — artiste, Busy en lecture, actions prev/toggle/next, Off sans session
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
│   │       ├── Autostart.cs           # copie de ClickUp-Extended (Platform.cs) : démarrage automatique par la clé Run
│   │       ├── Idle.cs                # millisecondes depuis la dernière frappe ou le dernier mouvement souris
│   │       ├── SingleInstance.cs      # port local dérivé du dossier de données : verrou + canal (show, ping), stopped_by_user
│   │       └── SystemInfo.cs          # les appels Win32 bruts : temps CPU, mémoire, alimentation
│   ├── CustomNotch.App/               # l'application WPF (+ WinForms pour NotifyIcon/Screen seulement)
│   │   ├── CustomNotch.App.csproj     # cible net10.0-windows10.0.19041.0, WPF + WinForms (NotifyIcon/Screen), AllowUnsafeBlocks (LibraryImport)
│   │   ├── app.manifest               # DPI par moniteur
│   │   ├── App.xaml / App.xaml.cs     # styles globaux, ligne de commande (--version, --home, --auto, --startup, --report), instance unique, exception non gérée
│   │   ├── Controller.cs              # assemblage et cycle de vie : config → ordonnanceur → pilules, tray, fenêtre Réglages, tic d'une seconde, UpdateChecker (mises à jour, 0.3.1)
│   │   ├── IPillHost.cs               # ce qu'une PillWindow demande au contrôleur (vues, actions, position, schéma) — testable avec un hôte factice
│   │   ├── TrayIcon.cs                # icône de la zone de notification (dessinée au lancement), menu (dont Réglages), bulle de notice
│   │   ├── Styles.xaml                # styles WPF partagés (boutons de la carte…)
│   │   ├── Notch/
│   │   │   ├── PillMetrics.cs         # les dimensions : tout le dessin part d'ici
│   │   │   ├── PillShape.cs           # la silhouette : coins arrondis + fillets inversés, retournée selon le bord
│   │   │   ├── EdgePlacement.cs       # position sur l'écran depuis edge / along / la zone de travail (fonctions pures)
│   │   │   ├── FullScreenDetector.cs  # la fenêtre au premier plan couvre-t-elle l'écran de la pilule ?
│   │   │   ├── PillWindow.cs          # la fenêtre : layout, position, drag, carte, menu contextuel, visibilité, clic (CellClickResolver)
│   │   │   ├── HoverCard.cs           # la carte de détail, dessinée dans la même fenêtre que la pilule
│   │   │   └── CardContent.cs         # le contenu de la carte, calculé sans WPF (CardModel) — testable sans écran
│   │   ├── Cells/
│   │   │   ├── CellFace.cs            # base commune : glyph au centre, arc d'activité (Busy tourne, Attention pulse)
│   │   │   ├── CellHost.cs            # une cellule dans la pilule : face + légende, pression au clic (scale .93)
│   │   │   ├── GlyphLibrary.cs        # les glyphes par nom (dont le logo Claude et music, repris de codenotch)
│   │   │   ├── RingArc.cs             # l'arc d'un anneau, de 0 à 100 %
│   │   │   ├── RingCell.cs            # anneau + glyph (system.cpu, system.memory, system.disk, system.battery…)
│   │   │   ├── ValueCell.cs           # disque + glyph, la valeur est dans la légende sous la cellule
│   │   │   ├── StatusCell.cs          # glyph + pastille de statut (launcher, media, une cellule sans valeur)
│   │   │   ├── SparklineCell.cs       # les 30 derniers points à l'échelle du maximum observé (system.network)
│   │   │   └── StatusPalette.cs       # les couleurs fixes de la pilule : son identité, indépendante du thème
│   │   ├── Settings/                  # la fenêtre Réglages (§7)
│   │   │   ├── SettingsWindow.cs      # sidebar (Pilules & cellules, Sources, Général) + page, barre « Fermer », une seule instance
│   │   │   ├── SettingsContext.cs     # ConfigStore, ConfigEditor, schémas et UpdateChecker (Controller.Updates) partagés par les pages
│   │   │   ├── PageBase.cs            # titre, sous-titre, rangées ; abonnement à ConfigStore.Changed, sélection préservée
│   │   │   ├── PillsPage.cs           # arbre pilule → cellules, barre d'outils (+ pilule, + cellule, ↑ ↓, masquer, supprimer)
│   │   │   ├── PillEditor.cs          # bord, écran, position le long du bord, échelle, visible
│   │   │   ├── CellEditor.cs          # source, affichage, actions, groupe — quatre sections
│   │   │   ├── SchemaForm.cs          # le formulaire d'une cellule, généré depuis SourceSchema / SchemaField
│   │   │   ├── SourceCatalogDialog.cs # le catalogue des sources (titre — description), filtrable, pour + Cellule et « Changer… »
│   │   │   ├── GlyphGallery.cs        # la galerie des glyphes nommés + champ « tracé SVG »
│   │   │   ├── EditorBanner.cs        # bandeau rouge en tête de l'éditeur quand ConfigEditor refuse ; la valeur reste dans le champ
│   │   │   ├── SourcesPage.cs         # réglages globaux par type de source (aucun dans cette version) et liste des secrets
│   │   │   ├── ClaudePage.cs          # compte, jeton, dernière lecture, sessions ; boutons Se connecter / Relire maintenant
│   │   │   ├── GeneralPage.cs         # emplacement de cells.json, apparence, mises à jour, journal et diagnostic (--report), démarrage automatique, thème, lien vers le dépôt
│   │   │   └── Bricks.cs              # briques de formulaire communes aux pages (Section, Card, Row, Combo, Btn…)
│   │   ├── Platform/
│   │   │   ├── WindowsMediaSession.cs # IMediaSession pour Windows : GlobalSystemMediaTransportControlsSessionManager (WinRT)
│   │   │   └── ClaudeCli.cs           # trouve/lance le CLI claude (caché pour le renouvellement, visible pour la connexion), heure de démarrage d'un processus
│   │   └── Shared/                    # repris de ClickUp-Extended / AutoSort, non modifiés
│   │       ├── Controls.xaml          # copie de ClickUp-Extended : styles WPF des contrôles de formulaire (champs, combos, cases)
│   │       ├── Glyphs.cs              # les icônes tracées des boutons (lecture, pause, coche, crayon…)
│   │       ├── Native.cs              # GetWindowLong/SetWindowLong (WS_EX_NOACTIVATE | TOOLWINDOW)
│   │       ├── Screens.cs             # écrans et conversions pixels physiques → unités WPF
│   │       ├── Theme.cs               # palettes claire/sombre, accent système, chrome de fenêtre sombre
│   │       ├── TrayMenuRenderer.cs    # le menu du tray (WinForms) peint avec la palette : le rendu Windows est toujours clair
│   │       └── Ui.cs                  # les briques communes : pastille, bouton, ligne de formulaire — utilisées par Settings
├── tests/
│   ├── CustomNotch.Core.Tests/        # xUnit : configuration, édition, validation, modèle, sources, média, chemins — tout sans écran
│   └── CustomNotch.App.Tests/         # xUnit : les fonctions pures de rendu (forme, placement, glyphes, contenu de carte, SchemaForm)
├── scripts/
│   ├── publish.ps1                    # dotnet publish → dist\customNotch\ (signature facultative)
│   ├── package.ps1                    # installateur Inno Setup + zip portable + latest.json
│   ├── installer.iss, install_tasks.ps1, remove_tasks.ps1
│   ├── install.ps1, Install.cmd, uninstall.ps1
│   ├── make-icon.ps1                  # dessine assets\customnotch.ico
│   └── release.ps1                    # release GitHub : setup, zip, manifeste
├── assets/                            # icône (.ico, .png)
├── docs/
│   ├── ARCHITECTURE.md, cells.example.json, apercu-pilule.png
│   └── superpowers/{specs,plans}/     # les spécifications validées, les plans d'implémentation
└── LICENSE, NOTICE.md, README.md, CHANGELOG.md
```

Données utilisateur (hors dépôt) : `%APPDATA%\customNotch\config.json`, `cells.<machine>.json`,
`secrets.json`, `logs\journal.log`, `logs\errors.log`, `stopped_by_user` (posé par « Quitter »
depuis le tray, effacé par tout lancement volontaire ; lu par la tâche de surveillance
`--auto`). `cells.json`, lui, est où l'utilisateur le décide (`cells_path` dans `config.json`)
— par défaut ce même dossier, typiquement un dossier synchronisé entre machines. Surcharge :
`--home <dossier>`, variable `CUSTOMNOTCH_HOME`, ou mode portable (dossier `data\` avec un
fichier vide `portable` à côté de l'exe).

---

## 3. Flux de données

```text
SettingsWindow ── ConfigEditor ──► cells.json / cells.<machine>.json / secrets.json
                                                    │  ConfigStore.Load() : lecture, fusion (ConfigMerge), placeholders, validation
                                                    ▼
                                              ConfigStore.Current (CellsFile)
                                                    │  Changed(file) → Controller.ApplyConfig
                                                    ▼
                   Scheduler.Apply(file) ── ReadAsync ──► Sources (system.*, http, shell, launcher, media)
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
  ex. `launcher.open`, `media.toggle`, `system.disk.open`) — suivi d'un `RefreshNow` pour que
  la cellule se redessine tout de suite.
- **Réglages → ConfigEditor → fichiers → ConfigStore.Changed → tout le reste** : chaque page
  écrit par `ConfigEditor` (jamais directement dans `ConfigStore` ni dans un document en
  mémoire), qui relit le fichier concerné, le mute, l'écrit atomiquement, puis appelle
  `ConfigStore.Load()` — le même chemin de rechargement qu'un `FileSystemWatcher` externe
  (édition à la main, synchronisation). Un chargement dont le texte résolu est identique au
  précédent ne redéclenche pas `Changed` : sans ce garde-fou, le `FileSystemWatcher` qui
  détecte l'écriture que `ConfigEditor` vient de faire referait reconstruire chaque page pour
  rien. Le drag suit le même chemin, côté local : `PillWindow` calcule `along`/`screen`
  pendant le glisser (fonctions pures d'`EdgePlacement`), `IPillHost.SavePosition` appelle
  `ConfigStore.SetPillLocal`, qui écrit `cells.<machine>.json` puis recharge.
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
    IReadOnlyList<(long Ms, double V)>? History, long? StaleSinceMs, string? Error,
    byte[]? Image);
```

`CellViews.From(cell, reading, nowMs, pill?, appearance?, schema?)` en déduit une `CellView`
(genre, statut, légende, fraction, forme, activité, légende visible) sans aucune dépendance WPF —
c'est ce que `CellHost`/`CellFace` dessinent, et ce que
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
  viennent toujours de la source — la lecture en cours de `media` est `Busy`). Sinon, sans
  `Value` ⇒ `Off` ; avec `Value`, la grandeur jugée est le pourcentage `Value/Max × 100` si
  `Max` est présent, sinon `Value` brut, comparée aux `thresholds` de la cellule ou, à défaut,
  à `Thresholds.RingDefault` (**warn 50, crit 75**) *si* la lecture a un maximum — sans
  maximum et sans seuils déclarés, le statut reste `Ok`. `invert: true` retourne la comparaison
  (bas = mauvais : la batterie).
- **Fraction et légende** : `Fraction` = `Value/Max` borné à `[0, 1]` (`null` sans maximum).
  `Caption` (le texte sous la cellule) : `« 73% »` pour un anneau, la valeur formatée (culture
  `fr-FR`, espace insécable normalisée) suivie de l'unité pour une valeur ou une sparkline ;
  pour un statut, `Text` tronqué à **10 caractères + « … »** s'il est présent (le titre du
  média sous sa cellule), sinon rien.
- **Cellule-groupe (`FromGroup`)** : le statut du groupe est le **pire** de ses enfants, dans
  l'ordre Attention > Crit > Busy > Warn > Ok > Off (`Rank`). L'anneau affiché est celui de
  l'enfant `headline` s'il est déclaré, sinon le pire enfant s'il a une fraction, sinon le
  premier enfant qui en a une, sinon le pire tout court. Le groupe n'est **périmé que si tous
  ses enfants le sont** (l'âge affiché est alors celui du plus ancien).
- **Apparence résolue (`CellViews.Resolve`)**, fonction pure testée sans écran : `CellView.Shape`
  (`round` | `square`) vient de la pilule (`PillConfig.CellsShape`) ; `Activity` (`dot` | `ring`)
  vient de la cellule (`CellConfig.Activity`) sinon de l'apparence globale
  (`AppearanceConfig.Activity`, `cells.json` → `appearance`, défaut `dot`) ; `ShowCaption` vient
  de la cellule (`CellConfig.Caption`) sinon du schéma de sa source (`SourceSchema.DefaultCaption`,
  `false` pour `media`), sinon `true`. Un groupe n'a pas de schéma : `FromGroup` résout sa légende
  sur le défaut `true`. `dot` : Busy/Attention ne colorent que la pastille de la cellule ; `ring` :
  l'arc animé d'aujourd'hui tourne autour d'elle.
- **`Reading.Image` / `CellView.Image`** : une image (PNG/JPEG, ≤ 512 Ko) à montrer à la place du
  glyph — la pochette de `media`, pour l'instant. `CellViews.From` la reprend telle quelle ;
  `FromGroup` montre celle de l'enfant `headline`, comme sa légende. Décodée une seule fois côté
  App, par `CoverImage` (§6).
- **Sources par cellule** : une instance de source sert toutes ses cellules ; l'état propre à
  chacune (delta CPU, historique réseau, ids poussés par `media`…) est gardé dans un
  dictionnaire par `cellId`, jamais dans des champs d'instance partagés.
- **Ce qu'un clic déclenche (`CellClickResolver.Resolve`)**, fonction pure testée sans écran :
  `config.actions.click` s'il est posé (open, shell ou action de source) ; sinon
  `SourceSchema.DefaultAction` de la source (`open` pour `launcher`, `toggle` pour `media`) sur
  une cellule non-groupe ; sinon la carte de détail. `PillWindow.OnCellClicked` ne connaît plus
  le cas particulier `launcher` : il applique le plan que rend le resolver.
- **Schéma d'une source (`SchemaField`/`SourceSchema`)** : chaque champ porte un `Type`
  (`string | number | bool | secret | path | url | choice`), un `Group` (la section du
  formulaire de la fenêtre Réglages, `null` = section par défaut) et un `Default` ; la source
  déclare son `DefaultGlyph` et son `DefaultAction`. `ConfigValidation.Validate(file, schemas)`
  vérifie, pour chaque cellule non-groupe, les champs `Required` présents et le type de chaque
  valeur posée (`number`, `bool`, `url` absolue http(s), `choice` parmi la liste) ; un champ
  requis résolu en chaîne vide (placeholder ou secret manquant) est refusé, un champ absent ne
  l'est pas — la source le dira elle-même à la lecture.

---

## 5. Sources

Les dix sources livrées avec ce plan (`CoreSources.Build`) ; `clickup` reste un sous-projet du
plan 0.4.0 (§10).

| Source | Params | Cadence par défaut | Lecture | Actions |
|---|---|---|---|---|
| `system.cpu` | — | 2 s | `Value` % (0-100, arrondi), `Max` 100, `Detail` (occupation), `History` (60 points) | — |
| `system.memory` | — | 5 s | `Value`/`Max` en Go, `Detail` (utilisée, libre) | — |
| `system.disk` | `drive` (défaut « C: ») | 1 min | `Value`/`Max` en Go, `Detail` (utilisé/total, libre) | `open` : ouvre le lecteur dans l'explorateur |
| `system.network` | `iface?` (vide = toutes les interfaces actives) | 2 s | `Value` = débit total (Ko/s sous 1 Mo/s, Mo/s au-dessus), `Unit`, `Detail` (↓ réception, ↑ émission), `History` | — |
| `system.battery` | — | 30 s | `Value` %, `Max` 100 ; `Status.Off` sans batterie ; `Ok` sur secteur, sinon seuils 20 %/10 % inversés ; `Detail` (charge, secteur ou temps restant) | — |
| `media` | `fallbackOpen` (cible ouverte au clic sans lecture, défaut « spotify: ») | 5 s (+ poussé au changement) | `Text` = « Titre — Artiste » ; `Status.Busy` en lecture, `Off` sans session ; `Detail` = une ligne `position` (temps `m:ss / m:ss`, fraction, hint `timeline:pos:dur:at`) si la session donne une durée ; sans session, une ligne « Lecture : aucune — cliquer ouvre l'application » ; `Image` = la pochette (PNG/JPEG, ≤ 512 Ko), si la session en donne une | `prev`, `toggle` (lecture/pause), `next`, sans libellé, en lecture ; `open` sans session |
| `claude` | `home?` (dossier `.claude` d'un autre compte, défaut `%USERPROFILE%\.claude`), `breakdown?` (répartition hebdomadaire par usage, défaut `false`), `sessions?` (sessions Claude Code en cours dans la carte, défaut `false`) | 5 min (+ poussé au changement d'une session ou après un renouvellement) | `Value` = % de la fenêtre de session (`UsageParser.Headline`), `Max` 100 ; `Detail` = fenêtres de limite (barre, `Hint` « reset le dd/MM à HH:mm » en heure locale) toujours, puis répartition hebdomadaire (libellés « · … ») si `breakdown`, puis sessions (nom, « occupée depuis … » / « en attente de toi » / « inactive » / « terminée il y a … », teinte) si `sessions` ; `Status` Attention si une session attend, Busy si une travaille, sinon `null` — et toujours `null` si `sessions` est faux (les seuils de l'anneau décident) ; sans jeton, expiré, en backoff ou en panne réseau : `Off` explicite ou dernière lecture périmée avec la raison, jamais un chiffre inventé | `refresh`, `sign-in` (« Se connecter ») |
| `http` | `url`\*, `method` (GET/POST), `path`, `textPath`, `max?`, `unit?`, `body?`, `headers{}` | 1 min | `Value`/`Text` extraits du JSON par `path`/`textPath` (`JsonPath` minimal : « data.items[0].n ») ; `Max`/`Unit` s'ils sont fournis ; `Detail` d'une ligne | — |
| `shell` | `command`\*, `parse` (number / json / text), `path?` (si json), `max?`, `unit?`, `timeoutSeconds` (5) | 1 min | selon `parse` : un nombre (+ `Max`/`Unit`), un nœud JSON pointé, ou le texte de sortie (`Detail`) | — |
| `launcher` | `open`\* | 24 h | `Status.Off`, `Text` = la cible | `open` : ouvre la cible (URL, chemin, application) |

\* champ requis. Chaque source déclare un `SourceSchema` (champs, type, aide, section, action
par défaut) — c'est ce que la fenêtre Réglages transforme en formulaire (`SchemaForm`, §7) et
ce que `ConfigValidation` vérifie.

`http` et `shell` sont l'échappatoire universelle : n'importe quelle API ou commande devient une
cellule sans écrire de code. `headers` de `http` porte l'authentification via
`${secret:nom}` — jamais un secret en clair dans `cells.json`.

`media` (`IMediaSession`, §7) lit la session média **système** (`Windows.Media.Control`,
WinRT) — Spotify, un onglet de navigateur, VLC… — celle que Windows choisit comme active ;
sans implémentation (tests, un autre OS) la source rend simplement `Off`. `WindowsMediaSession`
lit aussi sa vignette (`ReadThumbnailAsync`), plafonnée à **512 Ko** (une image plus grande est
ignorée) ; l'égalité de `MediaState` compare la pochette **par contenu**, pas par référence — un
tableau relu à chaque rafraîchissement ne doit pas déclencher `Changed` en boucle. Elle lit aussi
`session.GetTimelineProperties()` (`ReadTimeline`) : position, durée et horodatage de la mesure
(`MediaState.PositionMs/DurationMs/PositionAtMs`), les trois `null` si la session ne publie pas
de durée exploitable (flux en direct, application muette). Spotify et la plupart des
applications ne republient la timeline que toutes les quelques secondes (elle s'abonne aussi à
`TimelinePropertiesChanged`) ; l'avance à la seconde entre deux publications est reconstituée
côté carte (`HoverCard`, §6) à partir de `PositionAtMs`, jamais ici. Sans session (ou avec une
session sans lecture en cours), `MediaSource.InvokeAsync("toggle"|"open")` ouvre `fallbackOpen`
(`ActionRunner.Open`, injectable pour les tests) au lieu de ne rien faire.

`claude` (`Core/Sources/Claude/`) : `ClaudeCredentialsFile.Read(dir)` lit
`.credentials.json` (`claudeAiOauth.{accessToken, expiresAt, subscriptionType, rateLimitTier}`), en
lecture seule, et masque le jeton (`Log.Mask`) avant de le rendre — il n'atteint jamais le journal.
`UsageClient.FetchAsync` appelle `GET …/oauth/usage` (`Authorization: Bearer`,
`anthropic-beta: oauth-2025-04-20`, 15 s) et lève une `UsageException` typée (`RateLimited` avec
`Retry-After`, `Unauthorized`, `NoLimits`, `Network`) plutôt que de rendre un chiffre incertain.
`UsageParser.Parse` est pur : `limits[]` (repli `five_hour`/`seven_day`) devient des `LimitWindow`
dédoublonnées (alias d'id `session`/`five_hour`, `weekly_all`/`seven_day`/`weekly`, puis même reset
et même pourcentage, puis même libellé français), et `seven_day_breakdown.rows[]` devient la
répartition par surface. `Backoff.NextMs` double l'attente après un 429 (`Retry-After` ou 60 s par
défaut) et la plafonne à 1 h ; `Backoff.Save`/`Load` la persistent dans
`claude-backoff.json`, pour survivre à un redémarrage. `TokenRenewal.ShouldRenew` (pur) décide d'un
renouvellement (`claude -p`, marge 4 min avant expiration, un essai par jeton puis une attente
doublée par échec, plafonnée à 1 h) ; `TryRenewAsync` relance le CLI (délégué injecté) et juge sur
l'avancée de `expiresAt` après relecture.

`SessionRegistry` lit le registre que Claude Code tient lui-même dans
`~/.claude/sessions/<pid>.json` (un fichier par processus CLI, lecture seule). `Scan()` est
synchrone et ne lève jamais : un fichier illisible ou à moitié écrit est sauté (journalisé au plus
une fois par nom), un dossier absent (Claude Code jamais lancé) rend une liste vide.
`SessionRecord.Parse` est tolérant — seuls `pid` et `cwd` sont requis, `name` retombe sur le dernier
segment de `cwd`. Vivacité : le pid doit exister (délégué `processStartFileTime` injecté) **et**
son heure de démarrage doit correspondre à celle du fichier — sinon le pid a été recyclé par un
autre processus entre-temps ; comparaison sur `procStart` (FILETIME Windows, ±2 s en unités 100 ns)
quand le fichier le donne, sinon repli sur `startedAt` (ms, ±2 s) contre
`DateTimeOffset.FromFileTime`. Dédoublonnage par `sessionId` (le pid sert de repli), le fichier au
`startedAt` le plus grand gagnant. Une session qui disparaît (fichier supprimé, processus mort)
reste visible **10 min** (`EndedKeepMs`, état `Ended`, `SinceMs` = instant de la disparition) avant
d'être oubliée — « terminée il y a 3 min » plutôt qu'une disparition brutale. `IgnoredPids` (délégué)
exclut les pids que `TokenRenewal` vient de lancer pour renouveler le jeton : ce ne sont pas des
sessions de travail. `Start()` arme un `FileSystemWatcher` (rebond 120 ms, comme `ConfigStore`) et
un tic de **2 s** qui appelle `Scan()` quoi qu'il arrive — nécessaire malgré le watcher pour un
processus qui meurt sans toucher au dossier, et pour `status` réécrit dans le même fichier sans
événement fiable ; le tic sert aussi à retenter la création du watcher si le dossier n'existait pas
encore au démarrage. `Changed` part hors du verrou, seulement quand `Scan()` change le résultat.

`ClaudeSource : SourceBase` (type `claude`) assemble tout ça en une seule `Reading`, jamais un
chiffre inventé : elle garde, par id de cellule, la dernière lecture réussie (`_last`), rendue à
nouveau — marquée périmée (`Reading.AsStale`) avec la raison — sur toute erreur, plutôt qu'une
cellule vide. `ReadAsync` : (1) `sessions.Current` ; (2) un 429 récent (`Backoff.Load` au
démarrage) rend la dernière lecture périmée « limite atteinte, nouvel essai à HH:mm » sans retaper
l'API ; (3) sans jeton (`readCredentials`) → `Status.Off`, texte « Connexion requise », action
`sign-in` seule ; jeton expiré → un essai de `TokenRenewal.TryRenewAsync()` puis relecture, encore
expiré → dernière lecture périmée « jeton expiré — lance claude une fois » ; (4)
`UsageClient.FetchAsync` réussit → `Value`/`Max` de la fenêtre `Headline`, `Detail` = les fenêtres
(barre, `Hint` « reset dans … », formaté par `Countdown(ms)` — « 45 min », « 3 h 12 », « 2 j 22 h »)
puis la répartition (libellés préfixés « · ») puis les sessions (nom, « occupée depuis … » /
« en attente de toi » / « inactive » / « terminée il y a … », teinte Busy/Attention/Off/Off) ;
`Status` Attention si une session attend, Busy si une travaille, sinon `null` (les seuils de
l'anneau jugent alors `Value`/`Max`). Erreurs typées de `UsageException` : `RateLimited` écrit le
backoff (`Backoff.NextMs` sur un compteur d'échecs propre à la source, remis à zéro au premier
succès) et rend une lecture périmée ; `Unauthorized` relit le fichier une fois (un jeton différent
retente l'appel), sinon « Connexion requise » ; `NoLimits` → texte « Aucune limite rapportée »,
`Status.Off` (un état, pas une panne : pas périmée) ; `Network` → dernière lecture périmée avec le
message de l'exception. `InvokeAsync("refresh")` pousse la cellule ; `("sign-in")` appelle le
délégué `SignIn` (posé par le `Controller`, ci-dessous). `RefreshAll()` pousse toutes les cellules
`claude` connues à la demande — c'est ce que la page Réglages appelle pour « Relire maintenant »,
qui n'a pas de cellule à elle pour passer par `InvokeAsync`. Le constructeur câble
`sessions.IgnoredPids = () => renewal.LaunchedPids` et pousse, sur `sessions.Changed`,
`RefreshAll()` — même patron que `MediaSource`. `Status` (propriété, type `ClaudeStatus` : jeton,
dernier instantané d'usage — jamais effacé par une erreur passagère —, dernière erreur, prochain
essai, chemin du CLI, sessions, dernier renouvellement) et l'événement `StatusChanged` alimentent
la page Réglages → Claude (`ClaudePage`, §7) sans qu'elle relise un fichier ou refasse un appel
réseau, et jamais le jeton lui-même. `CoreSources.Build(media, claude)` enregistre l'instance
fournie par le `Controller` (`ClaudeCli` : CLI trouvé et lancé, heure de démarrage d'un processus,
§ App ci-dessous) ; `null` (tests de configuration, catalogue de schémas) retombe sur une instance
interne sans délégué réel (CLI introuvable, aucun pid jamais vivant, aucun fichier ni requête) —
le type `claude` reste connu de la validation et du formulaire de la fenêtre Réglages, sa lecture
n'est simplement jamais sollicitée. `DefaultCells` pose la cellule `claude` en tête de la pilule
par défaut, avant le groupe Système.

`ClaudeCli` (`App/Platform/`) fournit à `Core` ce qu'il ne peut pas faire lui-même : `Find()`
cherche `claude`/`claude.exe`/`claude.cmd` sur le PATH puis dans les emplacements connus
(`%LOCALAPPDATA%\Microsoft\WinGet\Links\claude.exe`, `~/.local/bin/claude.exe`,
`%APPDATA%\npm\claude.cmd`) ; `RunHiddenAsync` lance `claude -p` fenêtre cachée (stdin fermé
aussitôt, stdout/stderr drainés en continu, tué à l'échéance) — passé à `TokenRenewal` en
`runHidden` ; `SignIn` ouvre un terminal visible sur `claude auth login --claudeai` (`cmd /c
start`) — posé sur `ClaudeSource.SignIn` par le `Controller` ; `ProcessStartFileTime(pid)` rend
`Process.GetProcessById(pid).StartTime.ToFileTime()`, `null` sur toute exception (processus
disparu, accès refusé) — passé à `SessionRegistry` pour juger la vivacité d'une session. Le
`Controller` assemble la vraie `ClaudeSource` dans son constructeur (`BuildClaudeSource`) : le
dossier `.claude` par défaut pour le jeton et les sessions, le dossier de données de l'app
(`home`) pour `claude-backoff.json` (ce n'est pas un fichier de Claude Code) ; `TokenRenewal` se
référence lui-même dans son délégué `runHidden` (`NoteLaunched`) pour que `SessionRegistry`
ignore les pids qu'il vient de lancer. `SessionRegistry.Start()` est appelé à la fin de
`Controller.Start()` (comme `ConfigStore.StartWatching()`) et `Dispose()` dans `Stop()`.

---

## 6. Fenêtre et rendu

- **`PillMetrics`** — tout le dessin part d'ici, à l'échelle `scale` de la pilule : largeur
  **70 px**, coins libres **20 px**, fillets inversés **38,7 px**, anneau **44 px**, légende
  **15 px** (semi-gras, hauteur réservée 18 px), écart entre cellules 14 px, padding interne de
  la pilule 18 px de chaque côté du corps. `CardScale` (**1.0** à **1.5**, `Appearance.CardScale`,
  réglable dans Réglages → Général → Apparence) est **indépendante** de `Scale` : elle ne
  grandit que la carte au survol, jamais la pilule. `CardWidth` (la réserve côté fenêtre) =
  **340 × CardScale** ; la carte elle-même garde une largeur de base de 340 (min 240) et se met
  à l'échelle par `LayoutTransform`, pour ne jamais multiplier deux fois. Rayon **18 px**,
  padding **20/18**, ombre portée, posée à **8 px** de la pilule, sans queue.
- **`PillShape.Build`** dessine la silhouette pour le bord droit (un `StreamGeometry` : le
  corps aux coins arrondis côté libre, et deux fillets inversés côté écran — le carré au-dessus
  du corps moins un quart de cercle, comme le `::before` de codenotch), puis applique une
  `MatrixTransform` pour les trois autres bords (miroir à gauche, rotations à `top`/`bottom`).
  Liseré 1 px `#2e2e2e`, fond `#000` — la pilule reste toujours sombre, c'est son identité,
  indépendante du thème de l'application. Les **menus**, eux, suivent le thème de Windows :
  le menu du tray est peint par `TrayMenuRenderer` avec `Theme.CurrentPalette` (le rendu WinForms
  est toujours clair), le menu clic-droit de la pilule par les styles `ContextMenu`/`MenuItem`
  de `Styles.xaml` liés aux pinceaux que `Theme.Apply` pose, et `Controller` repose ces pinceaux
  sur `SystemEvents.UserPreferenceChanged` quand Windows bascule clair/sombre en cours de route
  — la fenêtre Réglages suit le même thème (`Theme.ApplyChrome`).
- **`EdgePlacement`** (fonctions pures, testées sans fenêtre) place le rectangle de la pilule
  contre le bord demandé, à `along` de la longueur disponible (`aire − extent`) ; `AlongFrom`
  fait le calcul inverse pendant un drag. `PillWindow.Reposition` résout l'écran nommé
  (`Pill.Screen`, ou l'écran principal), convertit sa zone de travail en DIP (`Screens.ToDip`)
  et recalcule sur `SystemEvents.DisplaySettingsChanged`.
- **`PillWindow`** — une fenêtre par pilule : transparente, `Topmost`, `ShowActivated = false`,
  `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` posés après `SourceInitialized` (hors barre des tâches,
  hors Alt-Tab). Elle réserve, côté libre, la place de la carte : elle est dessinée **dans la
  même fenêtre** que la pilule, pour ne jamais gérer de z-order. Les
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
  (`CancelHide`/`HideLater`). **Au thème Windows**, comme la fenêtre Réglages (`Surface`, `Fg`,
  `Muted`, `Border`, `Track`, posés par `SetResourceReference`) — la pilule, elle, reste noire.
  En-tête : la pochette (56 px, coins 12) ou une tuile (40 px, coins 10, le glyph) à gauche,
  titre 17 semi-gras et sous-titre 13 gris à droite. Une ligne par `Detail` (label 14 semi-gras,
  valeur 13 à droite, barre 6 px colorée par statut, séparateur 1 px entre deux lignes) ; la
  ligne `position` du média (hint `timeline:pos:dur:at`) n'a pas ce gabarit : barre pleine
  largeur couleur Busy, temps `m:ss` à gauche et durée à droite, ré-animée chaque seconde par un
  `DispatcherTimer` tant que la carte est ouverte (`UpdateTimeline`, avance locale depuis
  `PositionAtMs`) — un hint malformé retombe sur le gabarit normal sans planter. Les enfants
  d'un groupe font une ligne chacun (label, `caption` seule ou `caption + " · " + detail[0].Text`
  quand ils diffèrent, fraction, statut). Boutons : une `UniformGrid` d'icônes seules, centrées
  (le bouton du milieu plus large, 64 px), quand toutes les actions ont un libellé vide — les
  trois du média (`prev`/`toggle`/`next`) — sinon un `WrapPanel` de boutons icône + texte
  (`GlyphLibrary`, style `CardButton` : fond `SurfaceHover`, rayon 10). Une notice de péremption
  si la cellule est périmée. `PillWindow.PlaceCard` la centre sur la cellule côté libre, bornée
  à la fenêtre (empreinte réelle = `ActualWidth/Height × CardScale`), à 8 px de la pilule (sans
  queue vers la cellule : le triangle a été retiré en 0.2.0).
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
  couleur de l'anneau par statut (`StatusPalette`). **Forme** (`view.Shape`, `round` | `square`,
  posée par la pilule) : ronde comme avant, ou carrée — `RingCell` suit alors le contour d'un
  carré à coins arrondis (`SquareArc.Geometry(fraction, size, radius)`, rayon `12/44 × Ring`, une
  géométrie pure testée sans écran, contour parcouru en sens horaire depuis le milieu du bord
  haut — même origine que `RingArc`) au lieu du cercle, et `StatusCell` remplit un `Border`
  `CornerRadius` au lieu d'un disque ; les deux jeux d'éléments existent toujours, `Render`
  bascule juste leur `Visibility`. **Activité** (`view.Activity`, `dot` | `ring`) : `dot` (par
  défaut) — Busy/Attention ne colorent que la pastille de statut ou la teinte de l'anneau, aucun
  arc, aucune animation ; `ring` — l'arc animé d'aujourd'hui, Busy tourne (1,2 s/tour), Attention
  pulse (opacité 1 → 0,25, 0,55 s, aller-retour), en rond ou, en forme carrée, le long du contour
  de `SquareArc` (elle tourne quand même, autour du centre : ça reste lisible) — commun aux
  quatre faces (`CellFace.Activity`). Pression au clic : échelle 93 %, ressort 300 ms
  (`BackEase`). `StatusCell` remplit son disque (ou son carré) de la pochette
  (`CoverImage.Decode(view.Image)`, `ImageBrush` figée) à la place du glyph quand la lecture en
  porte une ; `CoverImage` décode une fois (cache par référence du tableau, dernier décodé
  seulement) et rend `null` sur une image illisible, auquel cas la cellule retombe sur son
  glyph. `HoverCard` fait de même en grand (56×56, coins 12) dans l'en-tête de la carte. La
  légende (`CellHost`) est masquée (`Visibility.Hidden`, pas `Collapsed`) quand
  `view.ShowCaption` est faux : la hauteur réservée ne bouge pas, le pas des cellules reste
  régulier.

---

## 7. Réglages

`SettingsWindow` (`App/Settings/`) : sidebar 220 px (**Pilules & cellules**, **Sources**,
**Claude**, **Général**), une page à droite, barre du bas avec **Fermer**. `Theme.ApplyChrome` pour la
barre de titre ; **une seule instance** : le tray, le menu clic-droit de la pilule et le
`show` reçu par `SingleInstance` (une seconde copie lancée à la main) ouvrent ou ramènent la
même fenêtre (`Controller.ShowSettings`).

- **Page « Pilules & cellules »** — maître-détail : un arbre pilule → cellules à gauche
  (`PillsPage`, cellule masquée grisée, enfants d'un groupe indentés dessous) avec une barre
  d'outils (**+ Pilule**, **+ Cellule** via `SourceCatalogDialog`, **↑ ↓**, **Masquer /
  Afficher**, **Supprimer**) ; à droite `PillEditor` (bord, écran, position le long du bord,
  échelle, **forme des cellules** — Combo « Rondes » / « Carrées arrondies », écrit
  `PillConfig.CellsShape`, partagé —, visible) ou `CellEditor`, quatre sections : **Source**
  (type, description, bouton « Changer… », puis le formulaire généré par `SchemaForm`),
  **Affichage** (libellé, glyph via `GlyphGallery` ou un tracé SVG, **légende sous la cellule**
  — Combo « Selon la source » / « Toujours » / « Jamais », `CellConfig.Caption` —, **activité**
  — Combo « Par défaut » / « Pastille seule » / « Anneau animé », `CellConfig.Activity` —, type
  de rendu, cadence, seuils warn/crit + inversé), **Actions** (clic et boutons de carte, chacun
  une action au choix), **Groupe** (cases sur les autres cellules de la pilule, tête parmi les
  cochées).
- **`SchemaForm.Build(SourceSchema, params, onChange)`** génère une rangée par champ, groupée
  par `SchemaField.Group` ; contrôle par type : `string` → champ texte, `number` → champ
  numérique validé, `bool` → case, `choice` → liste, `path` → champ + « … », `url` → champ
  validé, `secret` → champ masqué avec état « défini » + « Effacer ». `SchemaForm.ControlKind`
  est une fonction pure, testée sans fenêtre.
- **Page « Sources »** : une section par type de source ayant des réglages globaux (aucun dans
  cette version — ClickUp en aura un, plan 0.4.0 §10 — la page le dit) et la liste des secrets de
  `secrets.json` : nom, « défini » (jamais la valeur), Effacer.
- **Page « Claude »** (`ClaudePage`) — carte **Compte** : abonnement, palier, jeton (« valide
  jusqu'à 18:32 » / « expiré » / « aucun jeton »), CLI trouvé (chemin) ou non (« introuvable —
  winget install Anthropic.ClaudeCode »), champ **dossier .claude** (autre compte, réglage global
  de la source, `ConfigEditor.SetSourceGlobal("claude", …)`) ; boutons **Se connecter**
  (`ClaudeSource.SignIn`) et **Relire maintenant** (`ClaudeSource.RefreshAll()`) ; carte
  **Lecture** : dernière lecture, dernière erreur, prochain essai (backoff), dernier
  renouvellement ; carte **Sessions** : une ligne par session (nom — état — depuis). Se
  rafraîchit sur `ClaudeSource.StatusChanged` (marshalé sur le `Dispatcher`, désabonné à
  `Detach()` comme `OnStoreChanged`) — jamais un minuteur propre, jamais le jeton affiché.
- **Page « Général »** : emplacement de `cells.json` (chemin, Parcourir…, Ouvrir le dossier,
  « prise en compte au redémarrage »), une carte **Apparence** —
  **indicateur d'activité** (Combo « Pastille seule » / « Anneau animé », `Appearance.Activity`,
  partagé) et **échelle de la carte** (curseur 100 % à 150 % par pas de 5, `Appearance.CardScale`)
  —, une carte **Mises à jour** (0.3.1, ci-dessous), une carte **Journal et diagnostic**
  (0.3.1, ci-dessous), puis démarrage automatique (`Autostart`, case reflétant la clé Run — la
  tâche planifiée de l'installateur, §8, fait le même effet sans cette case), thème système/
  clair/sombre (`config.json → appearance.theme`, `Theme.Apply` immédiat), version, lien vers le
  dépôt.
- **Carte « Mises à jour »** (0.3.1) : case *Vérifier automatiquement* (`updates.enabled`,
  vraie par défaut), champ *Adresse du manifeste* (`updates.url` — vide, l'adresse par défaut
  s'affiche en gris en dessous, `Updates.DefaultManifestUrl` : le dépôt public GitHub, §8),
  curseur *Toutes les … h* (`updates.interval_hours`, 1-720, 24 par défaut), boutons
  **Vérifier maintenant** (`UpdateChecker.Check(manual: true)`), **Installer** (visible quand
  `UpdateChecker.Latest` n'est pas nul — `Prepare(latest)`, téléchargement vérifié puis
  décompression) et **Ignorer cette version** (`Skip`), ligne de statut alimentée par
  `UpToDate`/`Available`/`Failed`/`Progress` (marshalés sur le `Dispatcher`, désabonnés dans
  `Detach()` via `OnDetach`, comme `ClaudePage`). `Controller.Updates` (`UpdateChecker`,
  construit avec `ConfigStore.App`) vérifie 90 s après le démarrage puis toutes les 30 min si
  `Due()` (`updates.enabled` et la cadence, une adresse vide ne désactive rien) ; `Available`
  ouvre une bulle du tray, `ReadyToInstall` appelle `Updates.LaunchInstaller(stage)` — le setup
  (`/CLOSEAPPLICATIONS`) arrête l'application, la remplace, la relance.
- **Carte « Journal et diagnostic »** (0.3.1) : Ouvrir le journal (inchangé), **Signaler un
  problème…** — `Diagnostics.MakeReport()` sur un `Task.Run` (jamais sur le thread UI), puis un
  texte « Rapport déposé sur le Bureau : … » ; voir « `--report` » ci-dessous et §8.
- **Toute modification passe par `ConfigEditor`** (anti-rebond 300 ms sur les champs texte),
  qui route chaque champ vers le bon fichier : le partagé (`cells.json`, un choix de
  configuration), le local (`cells.<machine>.json` — visibilité d'une cellule, comme la
  position d'un drag) ou `secrets.json` (champ `secret` : la valeur y va chiffrée, `params`
  garde `${secret:<cellId>.<champ>}`). Une écriture refusée (`ConfigException`) affiche un
  bandeau rouge en tête de l'éditeur (`EditorBanner`) sans perdre la valeur saisie, qui reste
  dans son champ pour être corrigée ; le focus clavier est restauré après la reconstruction
  d'un champ modifié pendant la frappe (curseurs, `TextBox`), pour qu'une flèche ou une lettre
  suivante ne parte pas dans le vide. `cells.json` est réécrit avec un en-tête fixe de deux
  lignes ; ses commentaires manuels ne sont plus conservés (System.Text.Json ne les garde pas)
  — assumé : le fichier n'est plus fait pour être édité à la main, même s'il reste lisible et
  synchronisable.
- **Ce qui n'est pas dans l'UI** : rien — chaque champ de `cells.json`, `cells.<machine>.json`
  et `secrets.json` se règle depuis une page.

---

## 8. Livraison

Même patron que ClickUp-Extended, adapté au nom de l'application (`scripts/`, §2) :
`publish.ps1` (`dotnet publish` win-x64, fichier unique, signature `signtool` facultative via
`CUSTOMNOTCH_SIGN_THUMBPRINT`/`CUSTOMNOTCH_SIGN_PFX`+`CUSTOMNOTCH_SIGN_PASSWORD`) → `package.ps1`
(installateur Inno Setup, archive portable, manifeste) → `release.ps1` (release GitHub) →
`install.ps1`/l'installateur (poste de l'utilisateur).

- **`package.ps1`** publie puis produit `dist\customNotch-<version>-setup.exe` (Inno Setup,
  `installer.iss`), `dist\customNotch-<version>-win64.zip` (l'application + `install.ps1` +
  `Install.cmd` + `uninstall.ps1`, pour qui préfère dézipper), et `dist\latest.json` (le
  manifeste des mises à jour : version, URL, empreinte SHA-256 du setup et du zip, notes,
  date). Aucun droit administrateur, ni pour construire ni pour installer.
- **`installer.iss`/`install.ps1`** installent dans `%LOCALAPPDATA%\Programs\customNotch`,
  posent le raccourci du menu Démarrer (qui donne son nom et son icône aux notifications
  Windows), inscrivent l'application dans *Paramètres > Applications* avec une désinstallation
  propre (`uninstall.ps1`, garde les données sauf `-RemoveData`), et posent **deux tâches
  planifiées** (`install_tasks.ps1`) : **customNotch** à l'ouverture de session (relancée
  jusqu'à 3 fois en cas d'échec) et **customNotch Watchdog** toutes les 15 minutes, qui relance
  l'application si elle s'est arrêtée de façon inattendue. `--auto` (la tâche de surveillance)
  ne fait rien si l'application tourne déjà ou si `stopped_by_user` existe — posé par
  « Quitter » depuis le tray, effacé par tout lancement volontaire : un « Quitter » explicite
  n'est jamais annulé par le watchdog avant le prochain lancement voulu. La tâche de session
  démarre avec `--startup` : comme un lancement manuel (démarre même après un « Quitter », lève
  le marqueur), sauf que si elle perd la course à l'ouverture de session contre une autre copie
  (le watchdog peut partir en même temps), elle s'efface sans ouvrir les réglages. L'installateur pose le
  **runtime .NET 10 Desktop** s'il manque (téléchargé chez Microsoft, une fois par poste) —
  c'est ce qui garde l'application à quelques mégaoctets sans l'embarquer.
- **Installation silencieuse** : `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` — c'est ce que la
  mise à jour automatique lancera une fois le lecteur de manifeste écrit (ci-dessous).
- **`release.ps1`** crée (ou complète) la release GitHub `v<version>` du dépôt
  `c-chares69/customNotch` (notes tirées de `CHANGELOG.md`), y joint le setup, le zip et
  `latest.json`, avec le jeton que Git détient déjà (`GITHUB_TOKEN`/`GH_TOKEN`, sinon le
  gestionnaire d'identifiants). L'adresse du manifeste devient alors
  `https://github.com/c-chares69/customNotch/releases/latest/download/latest.json`.
- **`latest.json` est produit et publié dès 0.2.0**, et lu depuis 0.3.1 : `Updates.cs`
  (`Core/`, repris de ClickUp-Extended) parse le manifeste (`ParseManifest`, une URL relative
  résolue contre celle du manifeste), télécharge et vérifie l'empreinte SHA-256 avant de rendre
  le fichier (`DownloadAsync`), décompresse une archive zip (`Extract`, attend `install.ps1` et
  `customNotch\customNotch.exe` — c'est la forme produite par `package.ps1` ci-dessus, protégé
  du « zip slip ») et lance l'installateur silencieux détaché (`LaunchInstaller`,
  `InstallerCommand` choisit le setup `/VERYSILENT …` ou `install.ps1 -Source …` selon ce qui a
  été téléchargé). `UpdateChecker` (même fichier) orchestre ça hors du thread d'interface :
  `Check`/`Prepare` partent sur `Task.Run`, `Busy` empêche un second départ pendant qu'un premier
  tourne ; `updates.url` vide retombe sur `Updates.DefaultManifestUrl` (l'adresse ci-dessus)
  plutôt que d'échouer. `Controller.Updates`, construit avec `ConfigStore.App`, est le seul
  exemplaire ; *Réglages → Général → Mises à jour* (§7) le pilote.
- **Diagnostic (`--report`)** : `Diagnostics.MakeReport()` (`Core/`, repris de
  ClickUp-Extended) écrit `customNotch-diagnostic-<horodatage>.zip` sur le Bureau
  (`Paths.DesktopDir()`) — informations système, `logs\journal.log*`/`errors.log*`,
  `config.json`, `cells.json`, `cells.<machine>.json`, `claude-backoff.json` s'ils existent, et
  `fichiers.txt` (noms et tailles des fichiers du dossier de données) — **jamais**
  `secrets.json` : contrairement à ClickUp-Extended, `config.json` et `cells.json` ne portent
  jamais de secret en clair (`${secret:…}` reste un placeholder), donc pas de caviardage à
  faire. `App.xaml.cs --report` (ligne de commande, avant le verrou d'instance unique — une
  copie déjà lancée ne doit pas bloquer un rapport) et *Réglages → Général → Journal et
  diagnostic → Signaler un problème…* (§7) appellent la même fonction.
- **Icône** : `assets/customnotch.ico` (dessinée par `make-icon.ps1`) — menu Démarrer, barre
  des tâches, notifications, propriétés du fichier.

---

## 9. Stratégie de gestion des erreurs

| Situation | Comportement |
|---|---|
| Une source échoue | Dernière lecture conservée, marquée périmée (`StaleSinceMs`) ; légende et anneau à 55 % d'opacité, notice dans la carte (« Lecture … · erreur ») ; journal `WARN` — jamais une cellule vide. |
| Échecs répétés d'une source | Backoff ×2 à chaque échec, plafonné à 10 min ; réinitialisé au premier succès. |
| Inactivité (> 5 min sans souris ni clavier) | Les cadences sous 30 s remontent à 30 s (`Scheduler.IdleFloor`) : pas de travail pour rien devant un écran éteint. |
| `cells.json` ou `cells.<machine>.json` invalide | Refusé **en bloc** (`ConfigValidation`), message avec le chemin exact du champ, notice tray ; la configuration précédente reste en service. |
| `cells.json` JSON malformé | `ConfigException` avec le message du parseur ; même refus, même repli. |
| Une modification refusée par la fenêtre Réglages | `ConfigEditor` annule (le document précédent est réécrit, ou effacé s'il n'existait pas) et l'erreur remonte en bandeau (`EditorBanner`) — jamais appliquée à moitié, jamais silencieuse. |
| Placeholder sans valeur (`${secret:x}` absent) | Résolu en chaîne vide, avertissement journal — ne bloque pas le chargement, contrairement à un champ hors norme ou requis vide. |
| `secrets.json` ou `config.json` illisible | Valeurs vides ou par défaut, avertissement journal, jamais de crash. |
| Fichier verrouillé à l'écriture | `Json.WriteAtomic` réessaie 6 fois avec délai croissant, puis abandonne proprement (avertissement) ; le fichier d'origine n'est jamais tronqué. `ConfigEditor` transforme aussi un verrou en `ConfigException` explicite au lieu d'une exception brute. |
| Deux instances | La seconde envoie `show` sur le port local dérivé du dossier de données et s'arrête ; la première répond en ouvrant ses réglages. |
| Exception non gérée sur le thread UI | `DispatcherUnhandledException` la journalise et la marque traitée : l'application continue. |
| Une action échoue (clic, bouton de carte) | Capturée, journalisée (catégorie « action ») ; l'utilisateur ne la voit aujourd'hui que dans le journal — aucun retour visible n'est prévu à ce stade. |
| La session média lève une exception (WinRT) | Journalisée, la source `media` passe `Off` — jamais périmée, puisqu'aucune lecture n'avait eu lieu. |
| Secrets | Jamais en clair sur disque (DPAPI, préfixe `dpapi:`) ; résolus en mémoire seulement (`Placeholders.Resolve` ne réécrit jamais `cells.json`) ; un champ `secret` de la fenêtre Réglages n'écrit sa valeur nulle part ailleurs que `secrets.json`. |

---

## 10. Ce qui vient ensuite

Hors périmètre des versions livrées jusqu'ici (0.1.0 à 0.3.1), posé par les specs
(`docs/superpowers/specs/2026-09-22-customnotch-design.md` §6-§8, §12 ;
`docs/superpowers/specs/2026-09-23-customnotch-settings-media-design.md` §7 ;
`docs/superpowers/specs/2026-09-23-customnotch-claude-design.md` §4) :

- **0.4.0 — Source ClickUp** : endpoint local exposé par ClickUp-Extended (son propre
  sous-projet) ; la cellule `launcher` déjà posée par défaut (`DefaultCells`) reste un simple
  lien tant qu'il ne répond pas.
- **Plus tard** : GPU et températures, plusieurs comptes Claude (plusieurs cellules `claude`,
  chacune avec son `home` — v1.1), macOS (codenotch existe déjà côté Mac ; portage de la pilule
  et de `Platform/`, le cœur reste inchangé), glisser-déposer dans l'arbre de la page Pilules &
  cellules (↑ ↓ suffisent pour l'instant), détection plein écran affinée, export/import de
  `cells.json` depuis Réglages.
