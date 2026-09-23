# Journal des versions

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
