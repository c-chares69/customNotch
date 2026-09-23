# customNotch 0.3.0 — source Claude Code

La raison d'être de l'app (codenotch) : l'usage Claude Code en anneau, et l'activité des sessions en cours.
Conception validée en discussion le 2026-09-23 ; elle remplace le §6 de la spec du socle (hooks + serveur local)
par le **registre de sessions** que Claude Code tient lui-même (comme codenotch macOS). Le projet
`CustomNotch.Hook` disparaît.

## 1. Décisions

| Sujet | Décision |
|---|---|
| Jeton | `~/.claude/.credentials.json` → `claudeAiOauth.{accessToken, expiresAt (ms), subscriptionType, rateLimitTier}`. Lecture seule. Jamais envoyé s'il est expiré. Dossier alternatif : param `home` de la cellule (défaut `%USERPROFILE%\.claude`). |
| Usage | `GET https://api.anthropic.com/api/oauth/usage`, `Authorization: Bearer`, `anthropic-beta: oauth-2025-04-20`, 15 s. Réponse : `limits[{kind, percent, resets_at, scope.model.display_name}]`, repli `five_hour`/`seven_day{utilization, resets_at}`, et `seven_day_breakdown.rows[{display_name, percent}]`. Dédoublonnage codenotch : alias d'id (`session`↔`five_hour`, `weekly_all`↔`seven_day`), même reset + même % , même libellé. Cadence **5 min**. 429 → attente `Retry-After` (défaut 60 s) doublée à chaque récidive, plafonnée 1 h, persistée dans `%APPDATA%\customNotch\claude-backoff.json`. 401/403 → relecture du fichier une fois puis « Connexion requise ». 200 sans fenêtre → « Aucune limite rapportée ». Erreur réseau → dernière lecture périmée avec la raison (jamais un chiffre inventé). |
| Renouvellement | `claude -p` (stdin vide, fenêtre cachée, 30 s max) quand `expiresAt − now < 4 min` ; succès = `expiresAt` a avancé ; **un essai par jeton**, puis attente doublée (10 min → 1 h). Le CLI : `claude` sur le PATH, sinon `%LOCALAPPDATA%\Microsoft\WinGet\Links\claude.exe`, `~/.local/bin/claude.exe`, `%APPDATA%\npm\claude.cmd`. Sans CLI : note « lance claude une fois dans un terminal ». |
| Connexion | action `sign-in` : `claude auth login --claudeai` dans un terminal visible (`cmd /c start`). |
| Sessions | `~/.claude/sessions/<pid>.json` : `pid`, `cwd`, `name`, `status` (`busy`/`waiting`/`idle`), `sessionId`, `startedAt` (ms), `statusUpdatedAt`/`updatedAt` (ms), `entrypoint`, `procStart` (FILETIME Windows). Vivante si le processus existe **et** que son heure de démarrage correspond (`Process.StartTime.ToFileTime()` ±2 s) ; dédoublonnée par `sessionId` (la plus récente). Surveillance : `FileSystemWatcher` + tic 2 s. Une session disparue reste **10 min** dans la carte (« terminée il y a 3 min »). Les pids lancés par customNotch (renouvellement) sont ignorés. |
| Cellule `claude` | Anneau = fenêtre session (5 h), légende « 3 % ». Statut : **Attention** si une session `waiting`, sinon **Busy** si une `busy`, sinon seuils 60/85 % (`Thresholds.RingDefault`). Carte : fenêtres (barre, « reset dans 3 h 12 »), répartition hebdo par surface, sessions (nom, état, depuis), note de péremption. Actions : `refresh`, `sign-in` (icône `refresh`, libellé « Se connecter »). Clic = carte. `Pushed` au changement d'état d'une session ou après un renouvellement. |
| Défaut | `DefaultCells` : cellule `claude` (`label` « Claude », `glyph` `claude`) avant le groupe Système. Le `cells.json` de ce poste : ajout par le catalogue (l'orchestrateur l'ajoute au fichier après l'installation). |
| Réglages → page Claude | Compte (`subscriptionType`, tier), jeton (« valide jusqu'à 18:32 » / « expiré »), dernière lecture, prochain essai (backoff), CLI trouvé ou non, sessions en cours ; boutons **Se connecter**, **Relire maintenant** ; champ `home`. |
| Hook | `CustomNotch.Hook` retiré de la solution, du dépôt et des docs. |
| Attribution | `NOTICE.md` cite codenotch pour cette logique (déjà). |

## 2. Modèle

`Core/Sources/Claude/` :
- `ClaudeCredentials` (record `AccessToken, ExpiresAtMs, SubscriptionType?, RateLimitTier?`) + `ClaudeCredentialsFile.Read(dir) → ClaudeCredentials?` (null si absent/illisible).
- `LimitWindow` (record `Id, Label, Percent, ResetsAtMs?`), `UsageBreakdown` (record `Label, Percent`), `UsageSnapshot` (record `Windows, Breakdown, ReadAtMs, Plan?`).
- `UsageParser.Parse(JsonNode) → (List<LimitWindow>, List<UsageBreakdown>)` : pur, dédoublonnage, libellés français (« Session (5 h) », « Semaine (tous modèles) », « Semaine (Fable) » depuis `scope.model.display_name`).
- `UsageClient(HttpClient, Func<long> now)` : `FetchAsync(token, ct) → UsageSnapshot`, lève `UsageException(Kind: RateLimited(retryAfterMs) | Unauthorized | NoLimits | Network)`.
- `Backoff` (pur) : `Next(failures, retryAfterMs?) → ms`, persistance `claude-backoff.json` (`{ "until": ms }`).
- `TokenRenewal(Func<ClaudeCredentials?> read, Func<string?> findCli, Func<string, Task<int>> run, Func<long> now)` : `ShouldRenew(...)` pur ; `TryRenewAsync()` ; `LaunchedPids`.
- `SessionRecord` (record depuis JSON), `SessionRegistry(dir, Func<int, long?> processStartFileTime, Func<long> now)` : `Scan() → IReadOnlyList<ClaudeSession>` (vivantes + terminées < 10 min), `Changed` event, `IgnoredPids`.
- `ClaudeSource : SourceBase` (`Type "claude"`, schéma : `home` path non requis ; `DefaultRefresh` 5 min ; `ReadAsync` assemble usage + sessions ; `InvokeAsync("refresh"|"sign-in")`).
- `ClaudeStatus` (pour la page Réglages) : `Credentials`, `LastSnapshot`, `LastError`, `NextAttemptMs`, `CliPath`, `Sessions`.

`App` : `Platform/ClaudeCli.cs` (recherche du CLI, lancement caché / visible, `Process.StartTime` → FILETIME), `Settings/ClaudePage.cs`, entrée `("claude", "Claude")` dans `SettingsWindow.Nav` après « Sources ».

## 3. Tests (Core, réponses fixées)

`UsageParserTests` (réponse réelle capturée le 2026-09-23 : session 3 %, weekly_all 36 %, weekly_scoped Fable 29 %, breakdown Claude Code 94 %) ; `limits` vide + `five_hour` seul ; doublons (`session` + `five_hour` même reset) ; `TokenRenewalTests.ShouldRenew` (pas de jeton → false ; > 4 min → false ; même jeton déjà tenté → false ; cooldown) ; `BackoffTests` (60 s, 120 s, …, plafond 1 h ; `Retry-After` prime) ; `SessionRegistryTests` (dossier temp : pid vivant/mort par délégué, dédoublonnage, session terminée gardée 10 min puis oubliée, `waiting` → Attention) ; `ClaudeSourceTests` (statut Attention/Busy/seuils ; jeton expiré → lecture périmée avec raison ; sans jeton → « Connexion requise »).

## 4. Hors périmètre

Hooks, serveur local, plusieurs comptes (v1.1), lecture des transcripts, notification Windows quand une session attend (plus tard), `claude /usage` en repli.
