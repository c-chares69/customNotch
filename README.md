# customNotch

Une pilule noire aux coins inversés, ancrée sur un bord d'écran : chaque cellule montre d'un
coup d'œil une source — CPU, mémoire, batterie, une commande, un point JSON d'une API… — et son
survol ouvre une carte de détail avec des actions.

![pilule](docs/apercu-pilule.png)

Conception détaillée : [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Versions :
[CHANGELOG.md](CHANGELOG.md). Attributions (codenotch, ClickUp-Extended, AutoSort) :
[NOTICE.md](NOTICE.md).

## Installation

### Option A - l'installateur (recommandé, c'est lui que l'on partage)

Télécharge `customNotch-<version>-setup.exe` depuis les
[releases GitHub](https://github.com/c-chares69/customNotch/releases) et double-clique.
Il est produit par `scripts\package.ps1` avec [Inno Setup 6](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup`). Trois écrans, aucun droit administrateur :

- installe dans `%LOCALAPPDATA%\Programs\customNotch`, raccourci du menu Démarrer, entrée
  *Paramètres > Applications* avec désinstallation propre (qui propose de garder ou d'effacer
  tes données) ;
- pose le **runtime .NET 10 Desktop** s'il manque sur le poste (téléchargé chez Microsoft, une
  fois par poste) : c'est ce qui garde l'application à quelques mégaoctets ;
- case *Lancer à l'ouverture de session* : tâche planifiée relancée jusqu'à 3 fois en cas
  d'échec, et tâche de surveillance toutes les 15 minutes (`--auto`, qui respecte un
  « Quitter » volontaire) ;
- arrête l'application en cours avant de remplacer ses fichiers, la relance à la fin ;
- silencieux avec `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` : c'est ainsi que la mise à jour
  automatique le lance depuis *Réglages → Général → Mises à jour* (voir plus bas).

```powershell
winget install Microsoft.DotNet.SDK.10                          # une fois
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1    # l'exécutable (dist\customNotch\)
powershell -ExecutionPolicy Bypass -File scripts\package.ps1    # + setup.exe + zip + latest.json
```

`publish.ps1 -SelfContained` embarque le runtime dans l'exécutable (≈ 64 Mo) pour un poste qui
ne peut rien télécharger.

### Option A bis - l'archive portable et `install.ps1`

`customNotch-<version>-win64.zip` contient l'application, `install.ps1`, `Install.cmd` et
`uninstall.ps1` : dézipper, double-cliquer `Install.cmd`. Même résultat que l'installateur,
sans assistant (le runtime .NET 10 Desktop doit être présent :
`winget install Microsoft.DotNet.DesktopRuntime.10`). `install.ps1` (aucun droit administrateur) :

- copie l'application dans `%LOCALAPPDATA%\Programs\customNotch` ;
- crée le raccourci du menu Démarrer - c'est aussi ce qui donne son nom et son icône aux
  notifications Windows ;
- inscrit l'application dans *Paramètres > Applications* avec une désinstallation propre
  (`uninstall.ps1`, qui conserve tes données sauf `-RemoveData`) ;
- enregistre deux tâches planifiées : **customNotch** à l'ouverture de session (relancée
  jusqu'à 3 fois en cas d'échec) et **customNotch Watchdog** toutes les 15 minutes, qui la
  relance si elle s'est arrêtée de façon inattendue. Si tu la quittes toi-même depuis l'icône,
  le watchdog respecte ce choix jusqu'au prochain lancement volontaire. La tâche de session
  démarre avec `--startup` : comme un lancement manuel, sauf que la copie en trop (les deux
  tâches peuvent partir ensemble à l'ouverture de session) s'efface sans ouvrir les réglages.

`scripts\package.ps1` produit les trois à la fois : l'installateur, l'archive portable et
`dist\latest.json`, le manifeste des mises à jour (voir plus bas), qui pointe sur
l'installateur.

### Signature de l'exécutable

Sans signature, SmartScreen avertit au premier lancement sur un nouveau poste. `publish.ps1`
signe l'exe si un certificat est désigné par variable d'environnement, puis horodate la
signature (elle reste valable après expiration du certificat) :

| Variable | Rôle |
| --- | --- |
| `CUSTOMNOTCH_SIGN_THUMBPRINT` | empreinte d'un certificat du magasin personnel (carte, HSM, certificat installé) |
| `CUSTOMNOTCH_SIGN_PFX` + `CUSTOMNOTCH_SIGN_PASSWORD` | ou un fichier `.pfx` |

Sans certificat, la publication réussit et le dit : « non signé ».

### Mises à jour

Le manifeste `latest.json` (version, URL, empreinte SHA-256, notes) est produit par
`scripts\package.ps1` et publié avec le setup et l'archive. *Réglages → Général → Mises à
jour* : case *Vérifier automatiquement* (`updates.enabled`, activée par défaut, vérification
90 s après le démarrage puis toutes les *N* heures, `updates.interval_hours`, 24 par défaut),
adresse du manifeste (`updates.url` - vide, c'est déjà l'adresse par défaut ci-dessous),
boutons **Vérifier maintenant**, **Installer** (téléchargement, empreinte SHA-256 vérifiée,
puis le setup silencieux `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` - il arrête l'application,
la remplace, la relance) et **Ignorer cette version**. Une bulle du tray prévient quand une
version plus récente est trouvée.

Publier une version : `scripts\package.ps1` puis `scripts\release.ps1`, qui crée la release
GitHub `v<version>` (notes tirées de `CHANGELOG.md`) et y joint le setup, le zip et
`latest.json`, avec le jeton que Git détient déjà. Le manifeste publié est à
`https://github.com/c-chares69/customNotch/releases/latest/download/latest.json` - c'est
l'adresse par défaut, déjà connue de l'application sans rien régler (dépôt public requis ;
sinon `package.ps1 -BaseUrl <adresse>`, déposer les fichiers sur un serveur ou un partage, et
renseigner cette adresse dans *Réglages → Général → Mises à jour*).

### Diagnostic

*Réglages → Général → Journal et diagnostic → Signaler un problème…* (ou
`customNotch.exe --report`, en ligne de commande, avant même l'instance unique) dépose sur le
Bureau `customNotch-diagnostic-<horodatage>.zip` : informations système, `logs\journal.log`
et `logs\errors.log`, `config.json`, `cells.json`, `cells.<machine>.json`,
`claude-backoff.json` s'ils existent, et la liste (noms, tailles) des fichiers du dossier de
données - jamais `secrets.json`, jamais un jeton ni un token, à joindre telle quelle à une
demande d'aide.

En développement, `dotnet run --project src/CustomNotch.App` suffit.

## Configuration

Tout se règle dans **Réglages…** (icône du tray, ou clic droit sur une pilule). Au premier
lancement, customNotch écrit un `cells.json` par défaut dans `%APPDATA%\customNotch\` (une
pilule à droite : Claude Code, le groupe Système, ce qui joue, un lanceur ClickUp). Trois
fichiers, trois rôles — c'est ce que Réglages écrit :

| Fichier | Contenu | Synchronisable |
|---|---|---|
| `cells.json` | les pilules et leurs cellules — la configuration elle-même | oui : portable, aucun chemin de machine, aucun secret |
| `cells.<machine>.json` | la surcharge locale — position après un glisser, écran, visibilité ; écrite par l'application, pas à la main | non |
| `secrets.json` | jetons et mots de passe, chiffrés avec le compte Windows (DPAPI) | non |

`cells.json` peut vivre ailleurs (un dossier synchronisé, par exemple) : `cells_path` dans
`config.json` (`%APPDATA%\customNotch\config.json`) donne son chemin. Les deux autres fichiers
restent toujours dans `%APPDATA%\customNotch\` — ou `--home <dossier>`, la variable
`CUSTOMNOTCH_HOME`, ou le mode portable (dossier `data\` avec un fichier vide `portable` à côté
de l'exe). `cells.json` reste lisible et synchronisable ; l'éditer à la main reste possible
mais ses commentaires disparaissent à la première sauvegarde par l'interface.

Les champs de `cells.json` acceptent des placeholders, résolus au chargement : `${env:NOM}`
(une variable d'environnement), `${secret:nom}` (une valeur de `secrets.json`), `${home}` (le
dossier de données). Une configuration invalide est **refusée en bloc** — l'ancienne reste
active — avec le motif exact dans `logs\journal.log`.

Une cellule par source livrée avec le socle :

```jsonc
{ "id": "cpu",     "source": "system.cpu" }
{ "id": "mem",     "source": "system.memory" }
{ "id": "disk",    "source": "system.disk",    "params": { "drive": "C:" } }
{ "id": "net",     "source": "system.network" }
{ "id": "batt",    "source": "system.battery" }
{ "id": "api",     "source": "http",           "params": { "url": "https://…", "path": "data.count" } }
{ "id": "build",   "source": "shell",          "params": { "command": "echo 42", "parse": "number" } }
{ "id": "clickup", "source": "launcher",       "params": { "open": "https://app.clickup.com" } }
```

`appearance` (racine, partagé) : `{ "activity": "dot|ring", "cardScale": 1.0-1.5 }` — l'indicateur
d'activité (pastille seule ou anneau animé) et l'échelle de la carte au survol, par défaut pour toutes
les cellules. `cellsShape` (par pilule, `"round"` ou `"square"`), `caption` et `activity` (par cellule,
`bool`/`string`, surchargent l'apparence globale) : forme, légende et indicateur propres à une pilule
ou une cellule.

Exemple complet, commenté champ par champ (deux pilules, un groupe, un exemple `http` en
commentaire) : [docs/cells.example.json](docs/cells.example.json).

### Claude Code

La cellule `claude` (posée par défaut) montre, en anneau, la fenêtre de session (5 h) ; sa carte
liste toutes les fenêtres de limite avec leur reset, la répartition hebdomadaire par surface, et
les sessions Claude Code en cours (occupée / en attente / inactive / terminée). Le jeton vient de
`~/.claude/.credentials.json` (lecture seule, jamais journalisé) et se renouvelle tout seul avant
d'expirer (`claude -p`, fenêtre cachée) ; sans jeton valide, la cellule dit « Connexion requise ».

**Réglages → Claude** réunit tout ce qu'il y a à savoir sans jamais montrer le jeton lui-même :
compte (abonnement, palier), jeton (« valide jusqu'à … » / « expiré »), le CLI trouvé ou non
(`winget install Anthropic.ClaudeCode` sinon), dernière lecture, prochain essai après une limite
atteinte, sessions en cours, et le dossier `.claude` à utiliser (utile avec un autre compte).
Le bouton **Se connecter** ouvre un terminal sur `claude auth login --claudeai` ; **Relire
maintenant** relit tout de suite, sans attendre la prochaine cadence (5 min).

## Sources disponibles

| Type | Params | Ce que ça rend |
|---|---|---|
| `system.cpu` | — | anneau, % d'occupation, historique |
| `system.memory` | — | anneau, Go utilisés / total |
| `system.disk` | `drive` (« C: ») | anneau, Go utilisés / total, action « ouvrir » |
| `system.network` | `iface` (facultatif) | débit Ko/s ou Mo/s, historique |
| `system.battery` | — | anneau %, secteur ou temps restant |
| `media` | `fallbackOpen` (cible ouverte au clic sans lecture, « spotify: » par défaut) | titre — artiste de ce qui joue, occupée en lecture, position de lecture (dans la carte), actions précédent / lecture-pause / suivant, avec la pochette |
| `claude` | `home` (facultatif : dossier `.claude` d'un autre compte, par défaut `%USERPROFILE%\.claude`), `breakdown` (facultatif : répartition hebdomadaire par usage, défaut non), `sessions` (facultatif : sessions Claude Code en cours dans la carte, défaut non) | anneau = fenêtre de session (5 h) ; carte : toutes les fenêtres de limite avec leur reset (en petit, sous chaque barre) ; avec `breakdown`, la répartition hebdomadaire par surface ; avec `sessions`, les sessions Claude Code en cours (occupée / en attente / inactive / terminée) et **Attention** / **Occupée** selon leur état (sinon les seuils de l'anneau décident) ; actions « Actualiser », « Se connecter » |
| `http` | `url`, `method`, `path`, `textPath`, `max`, `unit`, `headers`, `body` | valeur ou texte extrait d'une réponse JSON |
| `shell` | `command`, `parse`, `path`, `max`, `unit`, `timeoutSeconds` | valeur, JSON pointé, ou texte de sortie d'une commande |
| `launcher` | `open` | rien à lire : un glyph et une action « ouvrir » |

Détail des params de chaque source : [docs/ARCHITECTURE.md §5](docs/ARCHITECTURE.md#5-sources).
`clickup` (timer ClickUp) reste un sous-projet du plan 0.4.0.

## Développement

Il faut le [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0), rien d'autre.

```powershell
dotnet build CustomNotch.sln
dotnet test
```

`CustomNotch.Core.Tests` couvre la configuration, le modèle et les sources sans écran ;
`CustomNotch.App.Tests` couvre les fonctions pures de rendu (forme de la pilule, placement,
glyphes, contenu de la carte) — le dessin lui-même se vérifie à l'œil.
