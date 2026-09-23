# Journal des versions

## 0.3.2 - 2026-09-23

- **La fenêtre Réglages au gabarit de ClickUp-Extended.** Coquille refaite sur le patron de
  `HubWindow.xaml` (sidebar 240 px avec marque et navigation à glyph, en-tête de page dessiné par
  la fenêtre, défilement rembourré, pied Fermer inchangé) et briques refaites sur le patron de
  `SettingsPages` (`Card`, `Row`, `Hint`, `Problem`, `Check`, `Combo`, `Number`, `Text`, `Action`,
  `Field`, `Btn`).
- **Toutes les pages recomposées avec ces briques** — Général, Sources, Claude, Pilules &
  cellules (`PillEditor`, `CellEditor`, `SchemaForm`, `GlyphGallery`) et le catalogue de sources
  (`SourceCatalogDialog`, filtre et liste thémés) : mêmes clés de configuration, mêmes appels
  `ConfigEditor`, mêmes bandeaux d'erreur qu'avant — mais un champ texte commit désormais à
  l'Entrée ou à la perte du focus plutôt qu'après un anti-rebond de 300 ms, comme
  ClickUp-Extended ; seuls le libellé d'une cellule et le tracé SVG d'un glyph gardent la frappe
  en direct. Un nombre refusé dans le formulaire d'une source (`SchemaForm`) le dit sous le champ
  plutôt que par une bordure rouge.

## 0.3.1 - 2026-09-23

- **La carte Claude sur mesure.** Par défaut, elle ne montre plus que la consommation : les
  fenêtres de limite, avec leur date de reset en petit sous chaque barre (« reset le 26/09 à
  12:59 », en heure locale). La répartition hebdomadaire par usage et les sessions Claude Code en
  cours (avec la pastille Occupée / En attente) redeviennent des options de la cellule
  (`breakdown`, `sessions`), réglables aussi par défaut pour tous les `claude` via
  `sources.claude` ; la page Réglages → Claude continue de montrer les sessions, elle. Au passage,
  la carte au survol montre de nouveau les indications en petit sous une ligne (« sur secteur »,
  « reste 1 h 20 » de la carte Système), une régression du redesign de la 0.2.2.
- **Mises à jour depuis l'application.** *Réglages → Général → Mises à jour* : vérification
  automatique (case, adresse du manifeste - vide = le dépôt public par défaut -, cadence en
  heures), **Vérifier maintenant**, **Installer** (téléchargement vérifié par empreinte SHA-256
  puis setup silencieux `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`, comme l'installateur),
  **Ignorer cette version** ; une bulle du tray prévient quand une version plus récente est
  trouvée (vérification à 90 s après le démarrage, puis toutes les 30 min). Repris de
  ClickUp-Extended (`Updates.cs`, `UpdateChecker`).
- **Diagnostic `--report`.** *Réglages → Général → Journal et diagnostic → Signaler un
  problème…* (ou `customNotch.exe --report`, avant même l'instance unique) dépose sur le Bureau
  une archive `customNotch-diagnostic-<horodatage>.zip` : informations système, journaux,
  `config.json`, `cells.json`, `cells.<machine>.json`, `claude-backoff.json`, et la liste
  (noms, tailles) des fichiers du dossier de données - jamais `secrets.json`, jamais un jeton.
  Repris de ClickUp-Extended (`Diagnostics.cs`).

## 0.3.0 - 2026-09-23

- **Le jeton et l'usage Claude Code, lus sans jamais rien inventer.** `~/.claude/.credentials.json`
  donne le jeton (jamais journalisé) ; `GET …/oauth/usage` donne les fenêtres de limite (session,
  semaine tous modèles, semaine par modèle) et la répartition hebdomadaire par surface, avec un
  backoff persisté après un 429 et un renouvellement automatique (`claude -p`) avant expiration.
- **Le registre des sessions Claude Code en cours** (`~/.claude/sessions/<pid>.json`) : vivacité
  vérifiée par pid et heure de démarrage du processus (le pid n'est pas recyclé), dédoublonnage par
  session, surveillance par `FileSystemWatcher` + tic de 2 s, et une session terminée reste visible
  10 min avant de disparaître.
- **La cellule Claude Code, posée par défaut avant le groupe Système.** L'anneau montre la fenêtre
  de session ; la carte, toutes les fenêtres de limite (avec leur reset), la répartition
  hebdomadaire par surface et les sessions en cours (« occupée depuis … », « en attente de toi »,
  « inactive », « terminée il y a … ») ; **Attention** si une session attend, **Occupée** si une
  travaille. Actions « Actualiser » et « Se connecter » ; une lecture en échec garde la dernière
  valeur connue, marquée périmée avec la raison — jamais un chiffre inventé.
- **Réglages → Claude.** Compte (abonnement, palier), jeton (« valide jusqu'à … » / « expiré »),
  CLI Claude trouvé ou non, dernière lecture, prochain essai après une limite atteinte, sessions
  en cours, dossier `.claude` (autre compte) ; boutons **Se connecter** (`claude auth login
  --claudeai`, terminal visible) et **Relire maintenant**. Le jeton n'apparaît jamais dans
  l'interface.
- **Le projet `CustomNotch.Hook` est retiré** (solution, dépôt, docs) : le contrat qu'il portait
  (serveur d'événements local, hooks installés dans `~/.claude/settings.json`) est remplacé par
  le registre de sessions que Claude Code tient lui-même, lu directement par la cellule.
- **Icônes précédent / suivant refaites** : triangle et barre pleins, lisibles à 16 px (au lieu
  de tracés fins qui s'effaçaient à cette taille).

## 0.2.2 - 2026-09-23

- **Cellules rondes ou carrées, au choix par pilule** (Réglages → pilule → Forme des cellules) : la jauge
  devient un cadre qui se remplit, la pochette et le disque prennent la même forme.
- **La lecture ne fait plus tourner d'anneau.** Occupé et Attention ne colorent que la pastille ; l'anneau
  animé reste disponible (Réglages → Général → Apparence, ou par cellule).
- **Légende masquable** sous chaque cellule ; la cellule média n'affiche plus de légende par défaut (le
  titre est dans la carte).
- **La carte au survol, refaite.** Plus grande (340 px, texte 14/13), lignes séparées, ombre, au thème
  Windows clair/sombre comme la fenêtre Réglages ; échelle réglable jusqu'à 150 %. Les boutons portent
  les icônes du pack ; précédent / lecture-pause / suivant sont des icônes seules.
- **Carte média** : pochette en grand, titre et artiste en tête, position de lecture (barre et temps) qui
  avance à la seconde ; plus de ligne « Application ». Sans lecture, le clic ouvre l'application
  (`fallbackOpen`, `spotify:` par défaut).
- **Carte Système** : sous-titre de synthèse (« tout va bien », « Disque à surveiller ») et valeurs
  absolues à côté du pourcentage.

## 0.2.1 - 2026-09-23

- **La pochette du média.** La cellule `media` montre la pochette de ce qui joue - en disque dans la
  pilule, à la place du glyph, et en grand dans l'en-tête de la carte. C'est l'image que l'application
  donne à Windows (Spotify, navigateur, VLC…) ; sans image, le glyph reste.

## 0.2.0 - 2026-09-23

- **Tout se règle dans la fenêtre Réglages.** Pilules (bord, écran, position, échelle), cellules
  (catalogue de sources, formulaire généré depuis le schéma de la source, libellé, glyph, seuils,
  cadence, actions, groupes), secrets chiffrés, démarrage automatique, thème. Chaque changement
  s'applique tout de suite ; `cells.json` n'a plus besoin d'être ouvert - il reste lisible et
  synchronisable, et un refus (valeur invalide) s'affiche en tête de l'éditeur sans rien perdre.
- **La carte hover sans queue.** Le triangle qui pointait vers la cellule disparaît ; la carte se
  pose à 8 px de la pilule, centrée sur la cellule survolée.
- **Source `media`.** Ce qui joue (Spotify, navigateur, VLC…), lecture/pause au clic, précédent /
  suivant dans la carte ; grisée quand rien ne joue.
- **Groupe « Système » par défaut.** CPU, mémoire, disque, réseau, batterie dans une seule cellule :
  le CPU en tête, le reste dans la carte.
- **Le clic sur une cellule suit l'action par défaut de sa source** (ouvrir, lecture/pause) quand la
  configuration n'en fixe pas.
- **Les params des cellules sont vérifiés d'après le schéma de la source** (requis, nombre, URL, choix).
- **Livraison comme ClickUp-Extended.** `scripts\package.ps1` produit l'installateur Inno Setup
  (`customNotch-<version>-setup.exe`, aucun droit administrateur, runtime .NET 10 Desktop posé s'il
  manque), l'archive portable avec `Install.cmd`, et `latest.json`, le manifeste des mises à jour ;
  `scripts\release.ps1` publie la release GitHub. L'installation pose deux tâches planifiées :
  **customNotch** à l'ouverture de session et **customNotch Watchdog** toutes les 15 minutes, qui
  respecte un « Quitter » volontaire (`--auto`). Icône de l'application (`assets\customnotch.ico`).

## 0.1.0 - 2026-09-22

- **Pilules multiples ancrées à un bord.** Drag pour repositionner, multi-écrans, masquage
  automatique en plein écran.
- **Cellules anneau, valeur, statut, sparkline et groupe.** Carte de détail au survol avec
  détails, barres et actions.
- **Configuration `cells.json` portable, surcharge locale et secrets DPAPI.** Rechargée à
  chaud, sans redémarrer l'application.
- **Huit sources livrées avec le socle.** `system.cpu`, `system.memory`, `system.disk`,
  `system.network`, `system.battery`, `launcher`, `http`, `shell`.
