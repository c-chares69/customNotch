# customNotch

Une pilule noire aux coins inversés, ancrée sur un bord d'écran : chaque cellule montre d'un
coup d'œil une source — CPU, mémoire, batterie, une commande, un point JSON d'une API… — et son
survol ouvre une carte de détail avec des actions.

![pilule](docs/apercu-pilule.png)

Conception détaillée : [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Versions :
[CHANGELOG.md](CHANGELOG.md). Attributions (codenotch, ClickUp-Extended, AutoSort) :
[NOTICE.md](NOTICE.md).

## Installation

Un exécutable unique, un raccourci dans le menu Démarrer, et le lancement à l'ouverture de session :

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Shortcut -Autostart -Run
```

Cela produit `dist\customNotch\customNotch.exe` (fichier unique, ~25 Mo ; le runtime .NET 10 Desktop reste
partagé — s'il manque, `winget install Microsoft.DotNet.DesktopRuntime.10`). Sans les options, le script
publie seulement. En développement, `dotnet run --project src/CustomNotch.App` suffit.

L'installateur (Inno Setup, tâches planifiées, mises à jour) arrive avec le plan 2 ; en
attendant, l'application se lance depuis les sources ou un `dotnet publish` fait à la main.

## Configuration

Au premier lancement, customNotch écrit un `cells.json` par défaut dans
`%APPDATA%\customNotch\` (une pilule à droite : CPU, mémoire, disque, réseau, un lanceur
ClickUp). Trois fichiers, trois rôles :

| Fichier | Contenu | Synchronisable |
|---|---|---|
| `cells.json` | les pilules et leurs cellules — la configuration elle-même | oui : portable, aucun chemin de machine, aucun secret |
| `cells.<machine>.json` | la surcharge locale — position après un glisser, écran, visibilité ; écrite par l'application, pas à la main | non |
| `secrets.json` | jetons et mots de passe, chiffrés avec le compte Windows (DPAPI) | non |

`cells.json` peut vivre ailleurs (un dossier synchronisé, par exemple) : `cells_path` dans
`config.json` (`%APPDATA%\customNotch\config.json`) donne son chemin. Les deux autres fichiers
restent toujours dans `%APPDATA%\customNotch\` — ou `--home <dossier>`, la variable
`CUSTOMNOTCH_HOME`, ou le mode portable (dossier `data\` avec un fichier vide `portable` à côté
de l'exe).

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

Exemple complet, commenté champ par champ (deux pilules, un groupe, un exemple `http` en
commentaire) : [docs/cells.example.json](docs/cells.example.json).

## Sources disponibles

| Type | Params | Ce que ça rend |
|---|---|---|
| `system.cpu` | — | anneau, % d'occupation, historique |
| `system.memory` | — | anneau, Go utilisés / total |
| `system.disk` | `drive` (« C: ») | anneau, Go utilisés / total, action « ouvrir » |
| `system.network` | `iface` (facultatif) | débit Ko/s ou Mo/s, historique |
| `system.battery` | — | anneau %, secteur ou temps restant |
| `http` | `url`, `method`, `path`, `textPath`, `max`, `unit`, `headers`, `body` | valeur ou texte extrait d'une réponse JSON |
| `shell` | `command`, `parse`, `path`, `max`, `unit`, `timeoutSeconds` | valeur, JSON pointé, ou texte de sortie d'une commande |
| `launcher` | `open` | rien à lire : un glyph et une action « ouvrir » |

Détail des params de chaque source : [docs/ARCHITECTURE.md §5](docs/ARCHITECTURE.md#5-sources).
`claude`, `media` et `clickup` (usage Claude Code, session média système, timer ClickUp) sont
des sous-projets du plan 2.

## Développement

Il faut le [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0), rien d'autre.

```powershell
dotnet build CustomNotch.sln
dotnet test
```

`CustomNotch.Core.Tests` couvre la configuration, le modèle et les sources sans écran ;
`CustomNotch.App.Tests` couvre les fonctions pures de rendu (forme de la pilule, placement,
glyphes, contenu de la carte) — le dessin lui-même se vérifie à l'œil.
