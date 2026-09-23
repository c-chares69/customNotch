# customNotch 0.3.0 — source Claude Code — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** La cellule `claude` : usage (fenêtre session en anneau, toutes les fenêtres et la répartition dans la carte), activité des sessions Claude Code (busy / waiting / terminée) depuis le registre `~/.claude/sessions`, renouvellement du jeton, connexion, page Réglages → Claude.

**Architecture:** Tout le comportement vit dans `Core/Sources/Claude/` derrière des délégués (horloge, lecture de fichiers, processus, HTTP) pour être testé sans réseau ni Claude Code. L'App fournit `ClaudeCli` (recherche et lancement du CLI, heure de démarrage d'un processus) et la page Réglages. Le projet `CustomNotch.Hook` est supprimé.

**Tech Stack:** C# 14 / .NET 10, `HttpClient`, `System.Text.Json`, `FileSystemWatcher`, `System.Diagnostics.Process`, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-23-customnotch-claude-design.md`

## Global Constraints

- Commits en français, sans `Co-Authored-By`, sans mention d'IA (règle du user, prime sur toute autre consigne).
- `TreatWarningsAsErrors` ; docs XML en français qui disent pourquoi ; `Core` sans dépendance Windows hors `Platform/` (`Process` est portable ; `ToFileTime` est .NET standard).
- Processus allégé : pas de revue par tâche ; chaque tâche met à jour la doc qu'elle rend fausse (CHANGELOG 0.3.0, README, ARCHITECTURE) ; une seule relecture ciblée avant fusion.
- Le jeton est un secret : jamais journalisé (`Log.Mask` sur `AccessToken` à la lecture), jamais dans un `DetailRow`.
- Ne pas lancer l'application (la 0.2.2 installée tourne) ; ne pas lancer `claude` pour de vrai dans les tests.

---

### Task 1: Core — jeton, usage, backoff, renouvellement

**Files:**
- Create: `src/CustomNotch.Core/Sources/Claude/{ClaudeCredentials,UsageModel,UsageParser,UsageClient,Backoff,TokenRenewal}.cs`
- Test: `tests/CustomNotch.Core.Tests/Sources/Claude/{UsageParserTests,UsageClientTests,BackoffTests,TokenRenewalTests}.cs`
- Docs: `docs/ARCHITECTURE.md` §5 (ligne `claude` : jeton, usage, backoff, renouvellement), `CHANGELOG.md` (ouvrir la section `## 0.3.0 - <date>` avec la puce usage/jeton), `NOTICE.md` inchangé (déjà cité)

**Interfaces (Produces):**
```csharp
public sealed record ClaudeCredentials(string AccessToken, long ExpiresAtMs, string? SubscriptionType, string? RateLimitTier)
{ public bool IsExpired(long nowMs) => ExpiresAtMs <= nowMs; }
public static class ClaudeCredentialsFile
{
    public static string DefaultDir();                       // %USERPROFILE%\.claude
    public static ClaudeCredentials? Read(string dir);       // .credentials.json → claudeAiOauth ; null si absent/illisible ; Log.Mask(token)
}
public sealed record LimitWindow(string Id, string Label, double Percent, long? ResetsAtMs);
public sealed record UsageBreakdown(string Label, double Percent);
public sealed record UsageSnapshot(IReadOnlyList<LimitWindow> Windows, IReadOnlyList<UsageBreakdown> Breakdown, long ReadAtMs, string? Plan);
public static class UsageParser
{
    public static (List<LimitWindow> Windows, List<UsageBreakdown> Breakdown) Parse(JsonNode root);
    public static string Label(string kind, string? model);   // session → « Session (5 h) », weekly_all/seven_day → « Semaine (tous modèles) », weekly_scoped → « Semaine (<model>) », autre → kind lisible
    public static LimitWindow? Headline(IReadOnlyList<LimitWindow> w);   // id session, sinon five_hour, sinon la première
}
public enum UsageFailure { RateLimited, Unauthorized, NoLimits, Network }
public sealed class UsageException(UsageFailure kind, string message, long? retryAfterMs = null) : Exception(message) { public UsageFailure Kind { get; } public long? RetryAfterMs { get; } }
public sealed class UsageClient(HttpClient http, Func<long> now)
{ public const string Endpoint = "https://api.anthropic.com/api/oauth/usage"; public Task<UsageSnapshot> FetchAsync(string token, string? plan, CancellationToken ct); }
public static class Backoff
{
    public static long NextMs(int failures, long? retryAfterMs);   // retryAfter ?? 60 s, × 2^(failures-1), plafond 3 600 000
    public static long? Load(string path); public static void Save(string path, long? untilMs);   // claude-backoff.json { "until": ms }
}
public sealed class TokenRenewal(Func<ClaudeCredentials?> read, Func<string?> findCli, Func<string, Task<int>> runHidden, Func<long> now)
{
    public const long MarginMs = 4 * 60_000, CooldownMs = 10 * 60_000, CooldownCapMs = 3_600_000;
    public static bool ShouldRenew(long? expiresAtMs, long nowMs, long? attemptedForMs, long? lastAttemptMs, int failures);
    public Task<RenewalOutcome> TryRenewAsync();   // Idle | Renewed(untilMs) | Failed(message)
    public IReadOnlyCollection<int> LaunchedPids { get; }   // pids des `claude -p` lancés (ignorés par le registre)
    public string? CliPath { get; }
}
```
`runHidden(cliPath)` lance `claude -p` avec stdin vide, fenêtre cachée, 30 s max, et rend le code de sortie — fourni par l'App (Task 4) ; le pid lancé est communiqué par un `Action<int> launched` optionnel.

- [ ] **Step 1: Tests** (écrire d'abord) — `UsageParserTests` : la réponse réelle ci-dessous → 3 fenêtres (`session` 3 %, `weekly_all` 36 %, `weekly_scoped` 29 % libellé « Semaine (Fable) »), 4 lignes de breakdown, `Headline` = session ; `limits: []` + `five_hour`/`seven_day` seuls → 2 fenêtres ids `session`/`weekly_all` ; `limits` avec `session` **et** `five_hour` au même reset → 1 fenêtre ; réponse sans rien → 0 fenêtre. `UsageClientTests` avec un `HttpMessageHandler` factice : 200 → snapshot ; 429 `Retry-After: 30` → `UsageException(RateLimited, retryAfterMs 30 000)` ; 401 → Unauthorized ; 200 `{}` → NoLimits ; en-têtes `Authorization: Bearer <token>` et `anthropic-beta: oauth-2025-04-20` présents. `BackoffTests` : `NextMs(1, null) == 60 000`, `NextMs(3, null) == 240 000`, `NextMs(20, null) == 3 600 000`, `NextMs(1, 30 000) == 30 000` ; `Save`/`Load` aller-retour. `TokenRenewalTests` : `ShouldRenew` — null → false ; 10 min restantes → false ; 3 min restantes, jamais tenté → true ; même `attemptedFor` → false ; 3 min restantes, tenté il y a 2 min avec 1 échec → false (cooldown 10 min) ; `TryRenewAsync` : `read` rend `expiresAt` inchangé après `runHidden` → `Failed` ; rend un `expiresAt` plus grand → `Renewed`.

Réponse réelle (à mettre dans un fichier `tests/.../Claude/usage-2026-09-23.json` chargé par les tests) :
```json
{"five_hour":{"utilization":3.0,"resets_at":"2026-09-23T16:39:59.736361+00:00"},"seven_day":{"utilization":36.0,"resets_at":"2026-09-26T10:59:59.736392+00:00"},
 "limits":[{"kind":"session","group":"session","percent":3,"severity":"normal","resets_at":"2026-09-23T16:39:59.736361+00:00","scope":null,"is_active":false},
  {"kind":"weekly_all","group":"weekly","percent":36,"severity":"normal","resets_at":"2026-09-26T10:59:59.736392+00:00","scope":null,"is_active":true},
  {"kind":"weekly_scoped","group":"weekly","percent":29,"severity":"normal","resets_at":"2026-09-26T10:59:59.736685+00:00","scope":{"model":{"id":null,"display_name":"Fable"},"surface":null},"is_active":false}],
 "seven_day_breakdown":{"as_of":"2026-09-23T12:39:23.760428+00:00","rows":[{"key":"claude_code","display_name":"Claude Code","percent":94},{"key":"chat","display_name":"Chats","percent":0},{"key":"cowork","display_name":"Cowork","percent":6},{"key":"other","display_name":"Other","percent":0}]}}
```

- [ ] **Step 2: Implémenter** — `resets_at` ISO 8601 → ms (`DateTimeOffset.Parse` invariant) ; `percent` peut être entier ou flottant ; dédoublonnage : alias (`session`↔`five_hour`, `weekly_all`↔`seven_day`↔`weekly`), puis même `ResetsAtMs/1000` **et** même % , puis même libellé. `UsageClient` : `HttpRequestMessage`, `Accept: application/json`, 15 s via `CancellationTokenSource` lié ; 401/403 → Unauthorized ; 429 → `Retry-After` (secondes ou date) ; `HttpRequestException`/`TaskCanceledException` → Network ; 2xx sans fenêtre → NoLimits. `TokenRenewal` : une tentative par jeton (`attemptedFor`), cooldown doublé par échec, plafonné ; `TryRenewAsync` relit après le run et juge sur `expiresAt`.

- [ ] **Step 3: Docs et commit** — `dotnet build` 0 avertissement, `dotnet test` vert.
```powershell
git add -A
git commit -m "feat(claude): jeton, usage (oauth/usage), backoff persisté, renouvellement par claude -p"
```

---

### Task 2: Core — le registre des sessions Claude Code

**Files:**
- Create: `src/CustomNotch.Core/Sources/Claude/{SessionRecord,SessionRegistry}.cs`
- Test: `tests/CustomNotch.Core.Tests/Sources/Claude/SessionRegistryTests.cs`
- Docs: `docs/ARCHITECTURE.md` §5 (sessions : registre, vivacité, 10 min), `CHANGELOG.md` 0.3.0 (puce sessions)

**Interfaces (Produces):**
```csharp
public enum SessionState { Busy, Waiting, Idle, Ended }
public sealed record SessionRecord(int Pid, string Cwd, string Name, string RawStatus, string? SessionId, long? StartedAtMs, long? UpdatedAtMs, string? Entrypoint, long? ProcStartFileTime)
{ public static SessionRecord? Parse(JsonNode root); }   // tolérant : pid + cwd requis, le reste optionnel ; name ?? dernier segment de cwd
public sealed record ClaudeSession(string Id, string Name, SessionState State, long SinceMs, string Surface, int Pid);   // Surface : « Terminal », « VS Code », « Desktop », « Agent »
public sealed class SessionRegistry : IDisposable
{
    public const long EndedKeepMs = 10 * 60_000;
    public SessionRegistry(string dir, Func<int, long?> processStartFileTime, Func<long> now);   // processStartFileTime(pid) : FILETIME du processus vivant, null s'il n'existe pas
    public Func<IReadOnlyCollection<int>> IgnoredPids { get; set; } = () => Array.Empty<int>();
    public IReadOnlyList<ClaudeSession> Scan();     // vivantes (dédoublonnées par SessionId, la plus récente) + terminées < 10 min (State Ended, SinceMs = disparition)
    public IReadOnlyList<ClaudeSession> Current { get; }
    public event Action? Changed;                   // après un Scan dont le résultat diffère
    public void Start(); public void Dispose();     // FileSystemWatcher (Created/Deleted/Changed/Renamed, rebond 120 ms) + Timer 2 s → Scan
}
```
Vivacité : `processStartFileTime(pid)` non null **et** `|value − ProcStartFileTime| ≤ 2 s` (en unités FILETIME : 20 000 000) quand `ProcStartFileTime` est connu ; sinon (pas de `procStart` dans le fichier) : `StartedAtMs` ±2 s contre `DateTimeOffset.FromFileTime`. `State` : `waiting` → Waiting, `busy` → Busy, sinon Idle. `Surface` depuis `entrypoint` (`cli` → Terminal, `claude-vscode` → VS Code, `claude-desktop*` → Desktop, `local-agent` → Agent, défaut Terminal).

- [ ] **Step 1: Tests** (dossier temporaire ; le délégué de vivacité est un dictionnaire pid → FILETIME) : un fichier vivant `busy` → 1 session Busy, nom = `name` ; fichier dont le pid n'existe pas → ignoré ; pid recyclé (FILETIME différent de 1 h) → ignoré ; deux fichiers même `sessionId`, `startedAt` différents → la plus récente ; pid ignoré → absent ; une session vivante au premier `Scan`, fichier supprimé au second → `Ended` avec `SinceMs = now` ; 11 min plus tard (horloge factice) → absente ; `Changed` levé quand le résultat change et pas quand il est identique ; `Parse` tolère un JSON sans `procStart`/`sessionId`.

- [ ] **Step 2: Implémenter** — `Scan()` est synchrone et sans exception (un fichier illisible est sauté, journalisé au plus une fois par nom) ; le watcher et le timer appellent `Scan` sous un verrou ; `Changed` hors du verrou. Documenter pourquoi le tic de 2 s existe malgré le watcher (un processus mort sans toucher au dossier ; `status` réécrit sans événement fiable).

- [ ] **Step 3: Docs et commit**
```powershell
git add -A
git commit -m "feat(claude): registre des sessions Claude Code — vivacité par pid et heure de démarrage, sessions terminées gardées 10 min"
```

---

### Task 3: Core — la source `claude`, le défaut, le statut pour les Réglages

**Files:**
- Create: `src/CustomNotch.Core/Sources/Claude/{ClaudeSource,ClaudeStatus}.cs`
- Modify: `src/CustomNotch.Core/Sources/CoreSources.cs` (`Build(IMediaSession? media, ClaudeSource? claude)`), `src/CustomNotch.Core/Config/DefaultCells.cs` (cellule `claude` en tête), `src/CustomNotch.App/Cells/GlyphLibrary.cs` (vérifier que `claude` existe — oui, listé)
- Test: `tests/CustomNotch.Core.Tests/Sources/Claude/ClaudeSourceTests.cs`, mise à jour `ConfigStoreTests`/`DocsExampleTests` si le défaut change leur attente (`media` d'index 6 → 7…)
- Docs: `README.md` (tableau des sources : ligne `claude` avec `home`), `docs/ARCHITECTURE.md` §5 (`ClaudeSource`), `docs/cells.example.json` (cellule `claude`), `CHANGELOG.md`

**Interfaces (Produces):**
```csharp
public sealed class ClaudeSource : SourceBase
{
    public ClaudeSource(Func<string, ClaudeCredentials?> readCredentials, UsageClient usage, TokenRenewal renewal, SessionRegistry sessions, string backoffPath, Func<long> now);
    public override string Type => "claude";   // schéma : Title « Claude Code », Fields : home (path, non requis, « Dossier .claude (autre compte) »), DefaultGlyph « claude », DefaultCaption true
    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(5);
    public ClaudeStatus Status { get; }        // instantané pour la page Réglages
    public event Action? StatusChanged;
}
public sealed record ClaudeStatus(ClaudeCredentials? Credentials, UsageSnapshot? LastSnapshot, string? LastError, long? NextAttemptMs, string? CliPath, IReadOnlyList<ClaudeSession> Sessions, long? LastRenewalMs);
```
`ReadAsync` : (1) sessions = `sessions.Current` ; (2) si `now < backoffUntil` → lecture précédente marquée périmée « limite atteinte, nouvel essai à HH:mm » ; (3) jeton : `readCredentials(dir)` ; absent → `Reading(Status Off, Text « Connexion requise », Detail « Lance « Se connecter » », Actions sign-in)` ; expiré → `renewal.TryRenewAsync()` puis relecture ; toujours expiré → dernière lecture périmée « jeton expiré — lance claude une fois » ; (4) `usage.FetchAsync` → `Reading(Value = headline.Percent, Max = 100, Detail = fenêtres (Fraction, Hint « reset dans 3 h 12 ») + breakdown (« Semaine par usage » : Claude Code 94 %…) + sessions (« Sessions » : « customNotch — occupée depuis 2 min », Tone Busy/Attention/Off), Actions refresh + sign-in, Status = Attention si waiting, Busy si busy, sinon null (seuils))`. Erreurs : RateLimited → backoff sauvegardé + périmée ; Unauthorized → relire le jeton une fois, sinon « Connexion requise » ; NoLimits → Text « Aucune limite rapportée » Status Off ; Network → périmée avec la raison. `InvokeAsync("refresh")` → `Push(cellId)` ; `("sign-in")` → délégué `signIn` fourni par l'App (`Action` optionnel dans le constructeur). `sessions.Changed` → `Push` de toutes les cellules `claude` connues (comme `MediaSource`).

- [ ] **Step 1: Tests** (`UsageClient` sur handler factice, `SessionRegistry` sur dossier temp, horloge factice, `renewal` avec `runHidden` factice) : jeton valide + réponse réelle → `Value 3`, 3 lignes fenêtres, breakdown, `Status` null ; session `waiting` → `Status.Attention` ; `busy` → `Busy` ; jeton absent → `Off` + action `sign-in` ; jeton expiré et renouvellement qui échoue → lecture périmée (`StaleSinceMs` non null, `Error` contient « expiré ») en gardant la valeur précédente ; 429 → backoff écrit et lecture périmée « nouvel essai » ; `refresh` → `Pushed`.

- [ ] **Step 2: Implémenter**, puis le défaut : `{ "id": "claude", "source": "claude", "label": "Claude", "glyph": "claude" }` en première cellule ; `docs/cells.example.json` idem (commenté : `home` optionnel).

- [ ] **Step 3: Docs et commit**
```powershell
git add -A
git commit -m "feat(claude): la source claude — anneau de la fenêtre session, carte des fenêtres, répartition et sessions ; cellule par défaut"
```

---

### Task 4: App — CLI, page Claude, suppression du Hook, version 0.3.0

**Files:**
- Create: `src/CustomNotch.App/Platform/ClaudeCli.cs`, `src/CustomNotch.App/Settings/ClaudePage.cs`
- Modify: `src/CustomNotch.App/Controller.cs` (construction de `ClaudeSource` avec `ClaudeCli` ; `Stop()` dispose le registre), `src/CustomNotch.App/Settings/SettingsWindow.cs` (`Nav` + page), `CustomNotch.sln` (retirer `CustomNotch.Hook`), `Directory.Build.props` (0.3.0)
- Delete: `src/CustomNotch.Hook/`
- Docs: `docs/ARCHITECTURE.md` (§2 arborescence sans Hook, ligne « Hook Claude Code » du tableau retirée, section Réglages : page Claude, « Ce qui vient ensuite » : 0.3.1 mises à jour + `--report`, 0.4.0 ClickUp), `README.md` (page Claude, `--auto`/`--startup` inchangés), `CHANGELOG.md` 0.3.0 finalisé (puces : usage, sessions, page Claude, Hook retiré)

**Interfaces:**
```csharp
public static class ClaudeCli
{
    public static string? Find();                         // PATH (« claude », « claude.exe », « claude.cmd »), puis %LOCALAPPDATA%\Microsoft\WinGet\Links\claude.exe, ~/.local/bin/claude.exe, %APPDATA%\npm\claude.cmd
    public static Task<int> RunHiddenAsync(string cli, Action<int>? launched, TimeSpan timeout);   // `-p`, stdin fermé, CreateNoWindow, kill à l'échéance
    public static void SignIn(string cli);                // cmd /c start "" "<cli>" auth login --claudeai
    public static long? ProcessStartFileTime(int pid);    // Process.GetProcessById(pid).StartTime.ToFileTime(), null si absent/accès refusé
}
```
`ClaudePage` (PageBase) : cartes « Compte » (plan, tier, jeton valide jusqu'à / expiré, CLI : chemin ou « introuvable — winget install Anthropic.ClaudeCode »), « Lecture » (dernière lecture HH:mm, erreur, prochain essai), « Sessions » (liste nom — état — depuis), boutons **Se connecter** (`ClaudeCli.SignIn`), **Relire maintenant** (`source.InvokeAsync("refresh")` pour chaque cellule claude — via un délégué `Refresh` posé par le Controller) ; champ `home` (Debounced → `SetSourceGlobal("claude", …)`) ; rafraîchie sur `source.StatusChanged` (Dispatcher).

- [ ] **Step 1: Implémenter** (`Controller` : `var claude = new ClaudeSource(dir => ClaudeCredentialsFile.Read(dir), new UsageClient(HttpSource.Http, now), new TokenRenewal(read, ClaudeCli.Find, cli => ClaudeCli.RunHiddenAsync(cli, pid => …, 30 s), now), new SessionRegistry(Path.Combine(ClaudeCredentialsFile.DefaultDir(), "sessions"), ClaudeCli.ProcessStartFileTime, now), Path.Combine(home, "claude-backoff.json"), now) { SignIn = () => ClaudeCli.SignIn(ClaudeCli.Find() ?? "claude") }` ; `registry.Start()` après `Start()` ; `Stop()` → `Dispose`), supprimer le projet Hook, docs, version.

- [ ] **Step 2: Vérifier et committer** — `dotnet build` 0 avertissement, `dotnet test` vert, `git status` propre (le dossier Hook absent).
```powershell
git add -A
git commit -m "feat(app): page Réglages Claude, CLI claude (renouvellement caché, connexion), projet Hook retiré ; version 0.3.0"
```

---

## Auto-revue du plan

- **Spec** — §1 jeton/usage/renouvellement/connexion (T1, T3, T4), sessions (T2), cellule (T3), défaut (T3), page Claude (T4), Hook (T4), attribution (déjà). §2 modèle : T1–T3. §3 tests : T1–T3.
- **Types** — `TokenRenewal(read, findCli, runHidden, now)` (T1) construit en T4 avec `ClaudeCli` ; `SessionRegistry(dir, processStartFileTime, now)` (T2) construit en T4 ; `ClaudeSource(readCredentials, usage, renewal, sessions, backoffPath, now)` (T3) ; `ClaudeStatus` (T3) lu par `ClaudePage` (T4) ; `CoreSources.Build(media, claude)` (T3) appelé en T4.
- **Ordre** — T1, T2, T3, T4.
- **Points d'attention** — `Process.GetProcessById` lève sur un pid absent : attraper `ArgumentException`/`InvalidOperationException`/`Win32Exception` ; `StartTime` d'un processus d'un autre utilisateur → accès refusé → null (session ignorée, acceptable) ; `FileSystemWatcher` sur un dossier absent (Claude Code jamais lancé) : le créer n'est pas à nous — attendre qu'il existe (retenter au tic) ; `HttpSource.Http` partagé (même `HttpClient`).
