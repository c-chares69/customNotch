# customNotch — Conception

*Spec validée le 22 septembre 2026. Plan d'implémentation : `docs/superpowers/plans/`.*

Une pilule noire aux coins inversés, ancrée sur un bord d'écran, qui montre d'un coup d'œil
des **cellules** — usage Claude, CPU, timer ClickUp, morceau en cours, lanceurs… — et dont le
survol ouvre une carte de détail avec des actions. Inspirée de [codenotch](https://github.com/vinzdg/codenotch)
(MIT), dont on reprend le langage visuel et la logique Claude ; troisième app de la famille
ClickUp-Extended / AutoSort, dont on reprend le stack, l'architecture et les briques.

---

## 1. Points challengés / décisions prises

| Sujet | Demande initiale | Décision (et pourquoi) |
|---|---|---|
| Stack | fork du port Windows de codenotch (Rust/Tauri) | **C# / .NET 10 / WPF**, comme les deux autres apps. Aucune toolchain Rust sur le poste, réutilisation directe de `Theme`/`Ui`/`Platform`/`Updates`/installeur, ~15 Mo de mémoire au lieu de ~100 Mo (WebView2), et le partage de composants entre les trois apps n'est possible qu'à stack égal. |
| macOS | « Windows d'abord, Mac ensuite » | WPF est Windows-only. **Mac = réécriture de la couche UI** (Avalonia) le jour venu ; en contrepartie `Core` est du .NET pur sans dépendance Windows et se porterait tel quel. Décision assumée. |
| « Remplacer la barre des tâches » | | Une pilule ne remplace ni la bascule de fenêtres ni la zone de notification. customNotch est un **strip de statut et de lancement**, complément de la barre. Aucune fonction de shell Windows (liste de fenêtres…) n'est prévue. |
| Volume de cellules | tout afficher | Deux réponses : **plusieurs pilules** (une par groupe, chacune sur un bord) et **cellules-groupes** (un anneau dans la pilule, N lignes dans la carte). Une cellule peut être masquée sans être supprimée. |
| Providers LLM | conserver les six de codenotch | **Claude seul en v1** (usage + activité + hooks). Les autres ne sont pas portés : chaque port est un sous-projet de ~300 lignes le jour où il sert. Pas de code mort. |
| Configuration | un `config.json` | **Trois fichiers** : `cells.json` (portable, synchronisable entre machines), `cells.<machine>.json` (surcharge locale), `secrets.json` (DPAPI, jamais synchronisé). L'état d'app (positions, écran) va dans la surcharge locale, jamais dans le fichier partagé. |
| Type de cellule | `kind` obligatoire | `kind` **optionnel** : déduit de la lecture (`max` → anneau, `history` → sparkline, pas de valeur → statut). Un override reste possible. |
| ClickUp | intégration API | **Pas de second client ClickUp.** ClickUp-Extended reste la source de vérité et expose un endpoint local (sous-projet côté ClickUp-Extended). Sans lui, la cellule est un simple lanceur. |
| Média | Spotify | **Session média système** (`Windows.Media.Control`) : titre/lecture/play-pause pour Spotify, YouTube, VLC… sans compte ni API. |
| Source `shell` | | Gardée (échappatoire universelle), avec une règle : le serveur local des hooks ne peut **jamais** modifier la configuration — sinon une page web locale exécuterait des commandes. |
| Partage de composants | « customNotch ↔ autres apps » | Les fichiers repris de ClickUp-Extended vivent dans `Shared/` **sans modification** ; les contrôles du notch ne dépendent pas de `Core`. L'extraction en bibliothèque commune est un sous-projet ultérieur : un déplacement, pas une réécriture. |

---

## 2. Stack

| Besoin | Choix | Pourquoi |
|---|---|---|
| Langage | C# 14 / .NET 10 (`global.json` comme AutoSort) | Même famille que les deux autres apps ; un exécutable, démarrage instantané. |
| Interface | WPF (fenêtres transparentes, `DynamicResource`, DPI par moniteur) + WinForms pour `NotifyIcon`/`Screen` seulement | Pattern `OverlayWindow` de ClickUp-Extended : `AllowsTransparency`, `Topmost`, `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`. Une fenêtre layered laisse passer les clics sur ses pixels transparents : le click-through est natif. |
| TFM | `net10.0-windows10.0.19041.0` | Donne accès à WinRT (`Windows.Media.Control`) sans package. |
| HTTP | `HttpClient` | Claude, source `http`, endpoint ClickUp-Extended. |
| Secrets | DPAPI (`ProtectedData`), préfixe `dpapi:` | Même format que ClickUp-Extended. |
| Persistance | JSON (`System.Text.Json`), écriture atomique (tmp + remplacement) | `Json.cs` repris. |
| Hooks Claude Code | `CustomNotch.Hook.exe` (console, < 100 ms) → `HttpListener` local | Même contrat que `codenotch-hook` : ne jamais bloquer Claude Code, échec silencieux, lance l'app si absente. |
| Installation | Inno Setup (`scripts/installer.iss`) + `install.ps1` | Copiés d'AutoSort : par utilisateur, tâches planifiées (démarrage + watchdog), désinstallation propre. |
| Mises à jour | `Updates.cs` repris, manifeste `latest.json` sur les releases GitHub du dépôt customNotch | |
| Tests | xUnit (`tests/CustomNotch.Core.Tests`) + faux serveurs HTTP | `Core` est sans interface : tout se teste sans écran. |

---

## 3. Arborescence

```text
customNotch/
├── CustomNotch.sln, Directory.Build.props, global.json
├── src/
│   ├── CustomNotch.Core/              # sans interface, sans dépendance Windows (sauf Platform/)
│   │   ├── App.cs, Paths.cs, Log.cs, Json.cs, Secrets.cs, Diagnostics.cs
│   │   ├── Config/                    # CellsFile, Overrides, Merge, Placeholders (${env:}, ${secret:}), Schema, Validation
│   │   ├── Model/                     # Pill, Cell, Reading, DetailRow, Status, ActionSpec, Threshold
│   │   ├── Sources/
│   │   │   ├── ISource.cs, SourceRegistry.cs, Scheduler.cs, SourceSchema.cs
│   │   │   ├── Claude/                # Credentials, UsageClient, TokenRenewal, HooksInstall, ActivityStore, EventServer
│   │   │   ├── System/                # Cpu, Memory, Disk, Network, Battery
│   │   │   ├── HttpSource.cs, ShellSource.cs, LauncherSource.cs
│   │   │   ├── MediaSource.cs         # WinRT — derrière IMediaSession (cfg Windows)
│   │   │   └── ClickUpSource.cs       # endpoint local de ClickUp-Extended, sinon lanceur
│   │   ├── Actions/                   # ActionRunner : open / shell / source
│   │   └── Platform/                  # SingleInstance, Autostart, Idle (repris)
│   ├── CustomNotch.App/               # WPF
│   │   ├── App.xaml(.cs), Controller.cs, TrayIcon.cs, Native.cs, Screens.cs
│   │   ├── Notch/                     # PillWindow, PillShape, EdgePlacement, HoverCard, DragHandle
│   │   ├── Cells/                     # RingCell, ValueCell, StatusCell, SparklineCell, GroupCard, ActivityArc, GlyphLibrary
│   │   ├── Settings/                  # SettingsWindow (sidebar) : PillsPage, CellsPage, SourcesPage, ClaudePage, GeneralPage ; SchemaForm
│   │   └── Shared/                    # Theme.cs, Ui.cs, Glyphs.cs, Updates.cs — copiés de ClickUp-Extended, NON modifiés
│   └── CustomNotch.Hook/              # Program.cs : POST /event?e=<event>&ppid=<pid>, corps = stdin
├── tests/CustomNotch.Core.Tests/
├── assets/                            # icône, glyphes SVG convertis (attribution codenotch)
├── scripts/                           # publish.ps1, package.ps1, installer.iss, install.ps1, uninstall.ps1, register_tasks.ps1
├── docs/ARCHITECTURE.md, docs/superpowers/{specs,plans}/
└── README.md, CHANGELOG.md, LICENSE (MIT), NOTICE.md (codenotch)
```

Données utilisateur : `%APPDATA%\customNotch\` — `config.json` (état d'app : chemin de `cells.json`, langue, thème),
`cells.<machine>.json`, `secrets.json`, `logs\`. `cells.json` est **où l'utilisateur le décide** (défaut : ce même dossier ;
typiquement un dossier synchronisé). Surcharge : `--home <dossier>`, variable `CUSTOMNOTCH_HOME`, mode portable.

---

## 4. Modèle

### 4.1 Pilule, cellule, lecture

```text
Pill   { id, edge: left|right|top|bottom, along: 0..1, screen?: string, scale: 1.0, visible: true, cells: [Cell] }
Cell   { id, source: "system.cpu", label?, glyph?, kind?: ring|value|status|sparkline|group,
         refresh?: "2s", thresholds?: { warn: 70, crit: 90, invert?: false },
         params: { …propres à la source… }, children?: [cellId], headline?: cellId,
         actions?: { click?: Action, card?: [Action] }, visible: true }
Action { label?, icon?, open?: string | shell?: string | source?: string }
```

Chaque source produit un `Reading`, seule chose que l'interface connaît :

```csharp
sealed record Reading(
    double? Value,             // chiffre principal
    double? Max,               // présent → anneau en %
    string? Unit,              // "°C", "€", "tâches"
    string? Text,              // titre en cours, nom de tâche
    Status Status,             // Ok | Warn | Crit | Off | Busy | Attention
    IReadOnlyList<DetailRow> Detail,   // lignes de la carte : Label, Text, Fraction 0..1 ?, Hint ("reset dans 51 min")
    IReadOnlyList<ActionSpec> Actions, // boutons dynamiques (ex. Stop si un timer tourne)
    IReadOnlyList<(long Ms, double V)> History,   // sparkline
    long? StaleSinceMs);       // la source ne répond plus : cellule grisée + âge
```

- **Type de rendu** : `kind` explicite, sinon `group` si `children`, sinon `sparkline` si `History` non vide,
  sinon `ring` si `Max`, sinon `value` si `Value`, sinon `status`.
- **Statut dérivé** : si la source ne fixe pas `Status`, les `thresholds` s'appliquent à `Value/Max` (ou `Value`) ;
  `invert: true` pour les grandeurs où bas = mauvais (batterie).
- **Couleur d'anneau** : Ok = vert, Warn = jaune, Crit = orange-rouge ; Busy = arc fin qui tourne par-dessus ;
  Attention = pulse ambre ; Off = glyph seul, gris ; stale = opacité .55 + âge dans la carte.
- **Cellule-groupe** : l'anneau de la pilule montre l'enfant `headline` (défaut : l'enfant au pire statut) ;
  la carte empile les enfants (label, barre, valeur, actions).

### 4.2 Fichiers de configuration

- `cells.json` : `{ "version": 1, "pills": [Pill], "sources": { … réglages globaux par source … } }`. Portable : aucun chemin
  machine, aucun secret. Placeholders résolus au chargement : `${env:NAME}`, `${secret:name}`, `${home}`.
- `cells.<hostname>.json` : même forme, **fusionné par-dessus** par `id` (pilule et cellule). Reçoit ce que l'app écrit
  seule : `along`, `screen`, `visible` après un drag ou un clic tray. Un champ présent ici masque celui du fichier partagé.
- `secrets.json` : `{ "clickup_token": "dpapi:…" }`. Écrit par Settings uniquement.
- **Rechargement à chaud** : `FileSystemWatcher` sur les trois fichiers, anti-rebond 300 ms, validation ; une config
  invalide est **refusée** (journal + notice dans le tray), la précédente reste en service. L'app n'écrit jamais
  `cells.json` sauf depuis Settings (et n'y écrit que des champs portables).
- Settings sait dans quel fichier va chaque champ : portable par défaut, local pour position/écran/visibilité, secrets
  pour tout champ déclaré `secret` par le schéma de la source.

### 4.3 Sources v1

| Source | Params | Reading | Actions exposées |
|---|---|---|---|
| `claude` | `account?` (dossier `~/.claude` alternatif) | anneau = fenêtre session (5 h) ; Detail = toutes les fenêtres (`limits[]` + repli `five_hour`/`seven_day`, dédoublonnées comme codenotch) avec reset ; Busy quand une session travaille, Attention quand une attend l'utilisateur | `refresh`, `sign-in` (lance `claude auth login`) |
| `system.cpu` | — | `Value` %, `Max` 100 | — |
| `system.memory` | — | `Value` Go utilisés, `Max` Go, Detail : utilisé/libre | — |
| `system.disk` | `drive: "C:"` | `Value`/`Max` Go | `open` = explorateur |
| `system.network` | `iface?` | `Value` Mo/s (↓+↑), `Unit`, `History` | — |
| `system.battery` | — | `Value` %, `invert`, Detail : temps restant, secteur | — |
| `http` | `url`, `headers{}`, `method`, `path` (JSON pointer ou notation pointée), `max?`, `unit?`, `textPath?` | selon extraction | — |
| `shell` | `command`, `parse: number|json`, `path?`, `timeout: 5s` | selon sortie | — |
| `launcher` | `open` | Off, glyph seul | `open` |
| `media` | — | `Text` = titre — artiste, Busy si lecture, Detail : app source | `toggle`, `next`, `prev` |
| `clickup` | `endpoint?` (défaut : lu dans `%APPDATA%\ClickUp - Extended\config.json`), `fallbackOpen` | timer courant : `Text` tâche, `Value` écoulé (avance localement à la seconde), Busy ; totaux jour/semaine en Detail | `toggle`, `show` ; sans endpoint : `open` |

Chaque source implémente `ISource` :

```csharp
interface ISource {
    string Type { get; }                          // "system.cpu"
    SourceSchema Schema { get; }                  // champs (nom, type, requis, secret, aide) → formulaire Settings et validation
    TimeSpan DefaultRefresh { get; }
    Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct);
    Task InvokeAsync(string action, CellContext ctx, CancellationToken ct);   // "toggle", "refresh"…
    event Action<string>? Pushed;                 // mise à jour poussée hors cadence (hooks Claude, média)
}
```

### 4.4 Ordonnanceur

Une boucle par cellule à son `refresh` (défaut : `DefaultRefresh` de la source), `PeriodicTimer` + `Task`. Résultat dans
`ReadingStore` (dictionnaire `cellId → Reading`, thread-safe), événement `Changed(cellId)` vers le `Controller` WPF
(`Dispatcher`). Une exception de source = `StaleSinceMs` posé sur la dernière lecture valide, journal, **jamais** une cellule
vide ni un crash. Backoff exponentiel 2× jusqu'à 10 min sur échec répété ; réinitialisé au premier succès. `Pushed` court-circuite
la cadence. À l'inactivité (`IdleWatcher`, > 5 min), les cadences < 30 s passent à 30 s.

---

## 5. Fenêtre et rendu

- **`PillWindow`** (une par pilule) : transparente, `Topmost`, `ShowActivated=false`, `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`,
  hors barre des tâches et hors Alt-Tab. Taille = pilule + marge pour la carte ; le fond transparent ne capte pas les clics.
- **Placement** : `EdgePlacement` calcule la position depuis `edge`, `along`, `screen` et l'aire de travail (`Screens.cs`) ;
  recalcul sur `SystemEvents.DisplaySettingsChanged` et changement d'aire de travail. Écran absent → écran principal.
- **Forme** : reprise de codenotch — largeur 70 px, coins 20 px côté libre, deux **fillets inversés** de 38,7 px côté bord
  (`PathGeometry` : rectangle moins deux cercles), liseré 1 px `#2e2e2e`, fond `#000`. Orientée selon le bord (verticale à
  gauche/droite, horizontale en haut/bas). `scale` par pilule.
- **Cellule** : anneau 44 px, piste `#303030`, trait ~5 px, glyph 26 px au centre (`Geometry` — glyphes codenotch convertis
  + `Glyphs.cs`), texte 15 px semi-gras dessous (`%`, valeur+unité, ou rien). Pression : scale .93, 300 ms.
- **Carte hover** : fond `#0a0a0a`, rayon 16 px, padding 16 px, largeur max 246 px, queue de 32×36 px vers la cellule,
  dessinée **dans la même fenêtre** (pas de z-order). Contenu : en-tête (glyph + label), lignes de Detail (label / hint à droite,
  barre 4 px, valeur), boutons d'action, notice stale. Ouverture au survol après 150 ms, fermeture 250 ms après la sortie ;
  reste ouverte tant que le pointeur est dans la carte.
- **Interaction** : clic = `actions.click` (défaut : ouvrir la carte) ; clic droit = menu (rafraîchir, masquer, réglages) ;
  poignée de déplacement au survol du bord de la pilule si `drag_enabled` ; le drag écrit `along`/`screen` dans la surcharge locale.
- **Plein écran** : quand la fenêtre au premier plan couvre l'écran de la pilule (jeu, vidéo), la pilule se cache ; v1 = simple
  test de rectangle, comme codenotch.
- **Thème** : la pilule est toujours sombre (c'est son identité). Settings suit `Theme.cs` (sombre/clair, accent système).

---

## 6. Claude Code

Port fidèle de la logique Windows de codenotch (`usage.rs`, `claude_auth.rs`, `hooks_install.rs`, `state.rs`, `server.rs`) :

- **Jeton** : `%USERPROFILE%\.claude\.credentials.json` → `claudeAiOauth.accessToken`, `expiresAt`. Lecture seule. Jamais envoyé
  s'il est expiré. **Renouvellement** : `claude -p` avec stdin nul quand `now + 300 s ≥ expiresAt` (seul le CLI autonome
  réécrit ce fichier). Garde anti-concurrence avec la connexion explicite.
- **Usage** : `GET https://api.anthropic.com/api/oauth/usage`, `Authorization: Bearer`, `anthropic-beta: oauth-2025-04-20`,
  15 s. Réponse : `limits[{kind, percent, resets_at}]` + repli `five_hour`/`seven_day{utilization, resets_at}` ; dédoublonnage
  par alias / même reset+% / même libellé. Cadence 5 min ; `429` → attente `Retry-After` persistée ; `403` = refus d'accès,
  pas « session expirée ».
- **Connexion** : action `sign-in` = `claude auth login --claudeai` dans un terminal visible, 15 min max ; le CLI seul touche
  aux identifiants.
- **Activité** : hooks installés (fusion, jamais écrasement) dans `~/.claude/settings.json` : `SessionStart`, `UserPromptSubmit`,
  `PreToolUse`/`PostToolUse` (`matcher: "*"`), `Notification`, `Stop`, `SessionEnd` → `CustomNotch.Hook.exe <event>`.
  Le hook POSTe `/event?e=<event>&ppid=<pid>` (corps = JSON du hook, ≤ 256 Ko) sur `127.0.0.1:<port>` (défaut 48667, différent
  de codenotch pour cohabiter), lance l'app si absente, et sort toujours en 0 sous 2 s.
- **Serveur local** : `HttpListener`, POST seulement, refus des requêtes avec `Origin`/`Referer` (CSRF), **aucune autre route**.
  `ActivityStore` : sessions par `session_id` (titre = dossier), états `running → done → dismissed`, `attention` posé par
  `Notification`, balayage des sessions abandonnées ; désinstallation des hooks retirée proprement depuis Settings.
- **Attribution** : `NOTICE.md` cite codenotch (MIT) pour la logique Claude et les glyphes.

---

## 7. ClickUp-Extended

Sous-projet côté ClickUp-Extended (sa propre spec) : un `HttpListener` local, port dans son `config.json`, jeton aléatoire dans
son dossier de données, routes `GET /snapshot`, `POST /timer/toggle|start|stop`, `POST /show`. customNotch lit le port et le
jeton dans ce dossier (même utilisateur Windows). Tant que l'endpoint n'existe pas ou ne répond pas, `clickup` se comporte
comme `launcher` avec `fallbackOpen` (défaut : l'exe de ClickUp-Extended `--show`, sinon `https://app.clickup.com`).

---

## 8. Réglages, tray, identité

- **Settings** (pattern AutoSort : sidebar + pages, `Ui.Form`) : **Pilules** (liste, bord, écran, échelle, visible, ajouter/
  supprimer) · **Cellules** (par pilule : ordre ↑↓, visible, ajouter depuis le catalogue de sources, éditer — formulaire généré
  par `SourceSchema`, seuils, actions, groupe) · **Sources** (réglages globaux : dossier Claude, endpoint ClickUp, secrets) ·
  **Claude** (état de connexion, hooks installés/désinstaller, dernière lecture, bouton connexion) · **Général** (emplacement
  de `cells.json`, démarrage automatique, thème, langue fr/en, mises à jour, diagnostic, journal).
- **Tray** : icône ; clic gauche = afficher/masquer toutes les pilules ; menu = pilules (cocher), rafraîchir tout, réglages,
  à propos, quitter. Notices (config refusée, jeton Claude expiré) en infobulle tray.
- **Identité** : nom « customNotch », `AppUserModelID`, icône, version ; `--version`, `--report` (zip de diagnostic sans secrets),
  `--home`, `--show-settings`. Instance unique (port local, comme les autres apps) : relance = afficher les réglages.

---

## 9. Stratégie de gestion des erreurs

Même doctrine que les deux autres apps :

- **Rien ne bloque l'interface** : sources sur `Task`, retour par `Dispatcher`.
- **Une source qui échoue s'affiche stale**, jamais absente ; l'erreur est dans la carte (une ligne) et le journal.
- **Config invalide = refusée, jamais appliquée à moitié** ; message avec chemin et champ.
- **Réseau** : timeouts courts, backoff, `429` respecté, aucun retry en boucle.
- **Secrets** : jamais en clair sur disque, jamais dans le diagnostic ni les journaux.
- **Hooks** : le hook exe ne peut pas faire échouer Claude Code (sortie 0 systématique).
- Journaux : `logs\journal.log` (rotation), `logs\errors.log`.

---

## 10. Tests

- `Core.Tests` (xUnit) : fusion `cells.json` + surcharge, placeholders, validation/refus, dérivation de `kind` et de `Status`,
  seuils (dont `invert`), dédoublonnage des fenêtres Claude sur des réponses fixées, backoff et `Retry-After`, ordonnanceur
  (stale, push, idle), `HttpSource` et `ClickUpSource` contre un faux serveur, `ShellSource` avec une commande fixée,
  `ActivityStore` (transitions, balayage), refus CSRF du serveur d'événements, écriture atomique.
- Rendu : les contrôles WPF exposent une fonction pure `CellView From(Cell, Reading)` (type, couleur, texte, lignes) testée en
  xUnit sans écran ; le dessin lui-même est vérifié à l'œil et par captures dans `docs/`.
- Vérification manuelle avant release : deux écrans, changement de DPI, plein écran, drag, perte réseau, jeton expiré.

---

## 11. Installation et publication

`scripts/publish.ps1` (`dotnet publish` win-x64, un exe par programme, dépendant du runtime .NET Desktop 10),
`scripts/package.ps1` (Inno Setup → `customNotch-<version>-Setup.exe`, zip portable, `latest.json`), tâches planifiées
démarrage + watchdog, désinstallation propre avec choix de garder les données. Signature Authenticode optionnelle par
variables d'environnement, comme ClickUp-Extended.

---

## 12. Hors périmètre v1 / suites prévues

- **v1.1** : GPU (NVML) et températures ; détection plein écran affinée ; plusieurs comptes Claude ; export/import de `cells.json`
  depuis Settings.
- **Sous-projets séparés** : endpoint local ClickUp-Extended ; extraction de `Shared/` + contrôles du notch en bibliothèque commune
  aux trois apps ; portage d'autres providers LLM (Codex, Cursor…) sur demande ; macOS (Avalonia).
- **Jamais** : fonctions de shell Windows (bascule de fenêtres, zone de notification), lecture de cookies de navigateur,
  requêtes ClickUp directes depuis le notch.
