# customNotch 0.3.1 — mises à jour dans l'application et diagnostic — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** L'application lit `latest.json` (publié à chaque release), annonce une version plus récente, la télécharge, vérifie son empreinte et lance le setup silencieux ; `--report` dépose une archive de diagnostic sans secret sur le Bureau. Tout est repris de ClickUp-Extended (`Updates.cs`, `UpdateChecker`, `Diagnostics.cs`, carte « Mises à jour »).

**Architecture:** `Core/Updates.cs` et `Core/Diagnostics.cs` sont des copies adaptées (namespace, `AppConfig` à la place de `Config`, horloge Unix locale, pas de migration Python) ; `Controller` planifie la vérification (90 s après le démarrage, puis toutes les 30 min si l'intervalle est écoulé) et relie `Available` → bulle tray + carte Réglages, `ReadyToInstall` → `LaunchInstaller` ; `GeneralPage` reçoit les cartes « Mises à jour » et « Journal et diagnostic ».

**Tech Stack:** C# 14 / .NET 10, `HttpClient`, SHA-256, `ZipFile`, WPF.

**Spec:** ce document (conception = celle de ClickUp-Extended, validée par le user : « respecter tous les process comme sur ClickUp-Extended »).

## Global Constraints

- Commits en français, sans `Co-Authored-By`, sans mention d'IA (règle du user).
- `TreatWarningsAsErrors` ; docs XML en français qui disent pourquoi ; copies fidèles de ClickUp-Extended sauf les adaptations listées.
- Processus allégé : une seule tâche, un implémenteur, doc dans la tâche, pas de revue (l'orchestrateur relit le diff).
- Ne pas lancer l'application ni un vrai téléchargement.

---

### Task 1: Updates, Diagnostics, Controller, page Général, --report, docs, version 0.3.1

**Files:**
- Create: `src/CustomNotch.Core/Updates.cs` (copie de `C:\Users\Coco_FW\Desktop\ClickUp-Extended\src\ClickUpExtended.Core\Updates.cs`), `src/CustomNotch.Core/Diagnostics.cs` (copie adaptée de `...\ClickUpExtended.Core\Diagnostics.cs`)
- Modify: `src/CustomNotch.App/Controller.cs`, `src/CustomNotch.App/App.xaml.cs` (`--report`), `src/CustomNotch.App/Settings/GeneralPage.cs`, `src/CustomNotch.Core/Paths.cs` (`DesktopDir()`), `Directory.Build.props` (0.3.1), `CHANGELOG.md`, `README.md`, `docs/ARCHITECTURE.md`
- Test: `tests/CustomNotch.Core.Tests/UpdatesTests.cs`, `tests/CustomNotch.Core.Tests/DiagnosticsTests.cs`

**Adaptations de `Updates.cs`** (sinon identique) : namespace `CustomNotch.Core` ; `App.Name`/`App.Version` (existent) ; `GoogleAuth.UnixNow()` → `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` (un `private static double UnixNow()` local) ; `Config` → `AppConfig` (`GetBool`, `GetString`, `GetDouble`, `Set`, `Save` existent) ; préfixe du dossier temporaire `customnotch-maj-` ; `Log.Info/Warning("maj", …)` (catégorie conservée). `UpdateChecker.Check` : quand `updates.url` est vide, utiliser **l'adresse par défaut** `https://github.com/c-chares69/customNotch/releases/latest/download/latest.json` (constante `Updates.DefaultManifestUrl`) au lieu d'échouer — c'est le dépôt public de l'app ; `Due()` de même (une URL vide ne désactive plus). Le reste (`ParseVersion`, `IsNewer`, `ParseManifest`, `FetchManifestAsync`, `DownloadAsync`, `Extract` (attend `install.ps1` + `customNotch\customNotch.exe` : c'est la forme de notre zip), `InstallerCommand`, `LaunchInstaller`, `Skip`, `Prepare`) inchangé.

**Adaptations de `Diagnostics.cs`** : namespace ; `RedactConfig` supprimé (notre `config.json` ne contient aucun secret ; `secrets.json` n'est **jamais** copié ; `cells.json` ne contient que des placeholders `${secret:…}`) ; l'archive contient `info.txt` (SystemInfo + `extra` : version installée, dossier de l'exe, tâches planifiées présentes ? non — rester simple), `logs\journal.log*`, `logs\errors.log*`, `config.json`, `cells.json`, `cells.<machine>.json`, `claude-backoff.json` s'ils existent, et la liste des fichiers du dossier de données (`fichiers.txt`, noms et tailles seulement). Nom : `customNotch-diagnostic-<horodatage>.zip` sur le Bureau (`Paths.DesktopDir()` = `Environment.GetFolderPath(SpecialFolder.DesktopDirectory)`).

**Controller** : `public UpdateChecker Updates { get; }` construit avec `_config.App` ; `Updates.Available += r => Ui.BeginInvoke(() => OnUpdateAvailable(r))` (bulle tray « Mise à jour <version> disponible — Réglages → Général pour l'installer » + mémorise `_pendingRelease` que la page lit) ; `Updates.ReadyToInstall += stage => Ui.BeginInvoke(() => Core.Updates.LaunchInstaller(stage))` (le setup arrête l'app, la remplace, la relance) ; dans le tic d'une seconde : à `+90 s` après le démarrage puis toutes les 30 min, `if (Updates.Due()) Updates.Check(manual: false)`. `IPillHost`/`SettingsContext` : la page accède au `Controller.Updates` via `SettingsContext` (ajouter `UpdateChecker Updates`).

**GeneralPage** : carte « Mises à jour » (avant « Poste ») : case « Vérifier automatiquement » (`updates.enabled`, défaut true), champ « Adresse du manifeste » (`updates.url`, vide = adresse par défaut affichée en gris), « Toutes les » N h (`updates.interval_hours`, défaut 24, 1–720), boutons **Vérifier maintenant** (`Updates.Check(manual: true)`) et **Installer** (visible quand `Updates.Latest` non null : `Updates.Prepare(latest)`), **Ignorer cette version** (`Skip`), ligne de statut (« Vérification… », « À jour (0.3.1) », « 0.3.2 disponible », « Téléchargement 43 % », erreur) alimentée par `UpToDate`/`Available`/`Failed`/`Progress` (Dispatcher ; désabonnement dans `Detach`). Carte « Journal et diagnostic » : boutons existants « Ouvrir le journal » + **Signaler un problème** (`Diagnostics.MakeReport()` sur un `Task.Run`, puis bulle/texte « Rapport déposé sur le Bureau : <nom> »). `App.xaml.cs` : `--report` comme ClickUp-Extended (`MessageBox` avec le nom du fichier, puis `Shutdown`), placé avant le verrou d'instance unique.

**Tests** : `UpdatesTests` — `ParseVersion("0.3.1+abc") == (0,3,1)`, `IsNewer("0.3.2", "0.3.1")`, `!IsNewer("0.3.1", "0.3.1")`, `ParseManifest` : URL relative résolue contre l'adresse du manifeste, sha mal formé → `UpdateException`, sans version → `UpdateException`, `Installable` false sans sha ; `InstallerCommand("…\\x.exe")` commence par le setup + `/VERYSILENT`. `DiagnosticsTests` — dossier temp avec `secrets.json`, `cells.json`, `logs\journal.log` : l'archive contient `info.txt`, `cells.json`, `journal.log`, **pas** `secrets.json`.

**Docs** : `CHANGELOG.md` `## 0.3.1 - <date>` (mises à jour dans l'app ; diagnostic `--report` / « Signaler un problème ») ; `README.md` section « Mises à jour » sur le modèle de ClickUp-Extended (sans le paragraphe migration Python ; adresse par défaut déjà renseignée ; `--report`) ; `docs/ARCHITECTURE.md` §2 (`Updates.cs`, `Diagnostics.cs`), § Livraison (le lecteur de manifeste existe désormais), § Réglages (cartes), §10 « Ce qui vient ensuite » (0.4.0 ClickUp, puis GPU/températures, plusieurs comptes Claude, macOS) ; `Directory.Build.props` 0.3.1.

- [ ] **Step 1: Tests** (écrire d'abord) — [ ] **Step 2: Copies et adaptations** — [ ] **Step 3: Controller, page, --report** — [ ] **Step 4: Docs, version** — [ ] **Step 5: `dotnet build` 0 avertissement, `dotnet test` vert, commit**
```powershell
git add -A
git commit -m "feat: mises à jour depuis l'application (manifeste, empreinte, setup silencieux) et diagnostic --report ; version 0.3.1"
```
