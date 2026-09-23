# customNotch 0.2.2 — apparence et flexibilité

Retour du user après la 0.2.1 : la lecture média ne doit pas faire tourner un anneau (seule la pastille change) ;
les cartes au survol sont trop petites et « font tache » ; les cellules pourraient être carrées ; il faut pouvoir
choisir. Maquettes validées dans le compagnon visuel (`.superpowers/brainstorm/450-1790168101/content/`) :
formes **A rondes et B carrées arrondies** au choix, carte **mise en page B** (grande, aérée) **au thème Windows**
(clair / sombre comme la fenêtre Réglages), boutons de carte en icônes du pack, carte Spotify sans ligne
« Application » et avec la position de lecture.

## 1. Décisions

| Sujet | Décision |
|---|---|
| Forme des cellules | `PillConfig.Cells` : `"round"` (défaut) ou `"square"` (carré arrondi, rayon 12/44 de la cellule). **Par pilule**, partagé (`cells.json`). Choix dans l'éditeur de pilule. |
| Indicateur d'activité | `Appearance.Activity` : `"dot"` (défaut : Busy/Attention colorent seulement la pastille) ou `"ring"` (l'arc animé d'aujourd'hui). Global dans `cells.json` → `appearance`, surcharge par cellule `CellConfig.Activity`. |
| Légende sous la cellule | `CellConfig.Caption` : `null` = automatique (comme aujourd'hui), `false` = jamais. `SourceSchema.DefaultCaption` (bool, défaut true) : `media` = false. Le défaut d'une source s'applique quand `Caption` est null. |
| Carte au survol | Mise en page B : largeur max **340**, min 240 ; padding 18/20 ; en-tête = tuile 40 (glyph) ou pochette 56 + titre 17 semi-gras + sous-titre 13 ; lignes 14 avec séparateur 1 px, valeur à droite 13, barre 6 px ; boutons pleine largeur 13 ; ombre 0 12 40 ; rayon 18 ; **au thème** (`Theme` : `Surface`, `Fg`, `Muted`, `Border`, `Track`, `Accent`) et non plus `#0a0a0a`. Échelle `Appearance.CardScale` 1.0–1.5 (Réglages → Général → Apparence). |
| Boutons de carte | `CardAction.Icon` connu de `GlyphLibrary` → icône ; libellé « utile » (différent de l'id et non vide) → icône + texte ; sinon texte. Les actions média (`prev`, `toggle`, `next`) portent un libellé vide → icône seule, centrées, lecture/pause en 64 px. |
| Carte média | En-tête : pochette 56 (ou tuile `music`), titre, artiste. Corps : barre de position + temps `m:ss / m:ss` quand la session donne une timeline ; **plus de lignes Titre / Artiste / Application**. Actions icône seule. |
| `media.fallbackOpen` | Nouveau param (`string`, défaut `spotify:`) : sans session, le clic ouvre cette cible (`ActionRunner.Open`) au lieu de ne rien faire ; la carte dit « Aucune lecture — cliquer ouvre Spotify ». |
| Carte Système (groupe) | Sous-titre de synthèse : « tout va bien » / « <enfant> à surveiller » (pire enfant Warn) / « <enfant> critique » (Crit) ; chaque ligne montre `Caption` (le %) **et** le premier `DetailRow.Text` absolu quand il diffère (« 61 % · 9,8 / 16 Go »). |
| Thème de la carte | Suit `appearance.theme` (system/light/dark) déjà en place : la carte lit les mêmes `DynamicResource` que Réglages. La pilule reste noire. |
| Ce poste | `cells.json` du user : rien à changer (la cellule `media` sans légende vient du défaut de la source ; formes rondes tant qu'il ne change pas). |

## 2. Modèle et configuration

`cells.json` :
```json
{
  "version": 1,
  "appearance": { "activity": "dot", "card_scale": 1.0 },
  "pills": [ { "id": "main", "edge": "right", "cells_shape": "square", "cells": [
      { "id": "media", "source": "media", "label": "Spotify", "caption": false, "activity": "ring", "params": { "fallbackOpen": "spotify:" } } ] } ]
}
```
- `CellsFile.Appearance : AppearanceConfig { Activity = "dot", CardScale = 1.0 }` (validé : `activity` ∈ dot|ring, `card_scale` ∈ [1, 1.5]).
- `PillConfig.CellsShape : string = "round"` (validé ∈ round|square). Nom JSON `cells_shape` (pas `cells`, déjà pris par la liste).
- `CellConfig.Caption : bool?`, `CellConfig.Activity : string?` (validé ∈ dot|ring).
- `SourceSchema.DefaultCaption : bool = true` ; `MediaSource` → false.
- `CellView` gagne `Shape` (round|square), `Activity` (dot|ring), `ShowCaption` (bool) — calculés dans `CellViews.From/FromGroup` depuis pilule + cellule + schéma + appearance. `CellViews.From(cell, r, nowMs, pill, appearance, schema)` : signature étendue avec valeurs par défaut pour ne pas casser les tests.
- `CardModel` gagne `Subtitle` déjà là ; `CardRow.Text` reçoit « % · absolu » pour les enfants de groupe ; `CardAction.Label` peut être vide.
- `MediaState` gagne `PositionMs`, `DurationMs`, `PositionAtMs` (horodatage de la mesure) — null si la session ne les donne pas ; `MediaSource` produit `Reading.Value/Max` ? **Non** (cela ferait un anneau) : la position va dans `Detail` sous une forme dédiée : `DetailRow("position", Text: "2:31 / 5:55", Fraction: 0.42, Hint: "timeline")` reconnue par la carte (label `position`) — la pilule ignore les Detail.

`ConfigEditor` : `SetPillShared(pillId, p => p["cells_shape"] = …)`, `SetCell` pour `caption`/`activity`, `SetAppearance(Action<JsonObject>)` (nouveau, partagé).

## 3. Rendu (App)

- `CellFace` : `Shape` connu au `Render` ; `RingCell` carré = `RectangleGeometry` arrondie pour la piste + arc de progression le long du contour (géométrie calculée par `SquareArc.Geometry(fraction, size, radius)` — pur, testé : longueur = périmètre du carré arrondi, départ en haut au milieu, sens horaire) ; `StatusCell` carré = `Border` `CornerRadius` 12 avec fond piste ou `ImageBrush` pochette ; `ValueCell`/`SparklineCell` inchangés (pas de contour).
- `CellFace.Activity(status, m)` : si `Activity == "dot"`, l'arc `_activity` reste caché (la pastille seule dit Busy/Attention) ; sinon comme aujourd'hui.
- `CellHost` : la légende est masquée (`Collapsed`) quand `ShowCaption` est false ; la hauteur de la cellule dans la pilule ne change pas (l'alignement entre cellules reste régulier).
- `HoverCard` : reconstruit selon la mise en page B ; `Background = DynamicResource Surface`, `BorderBrush = Border`, texte `Fg`/`Muted`, barre `Track`/statut ; `LayoutTransform = ScaleTransform(CardScale)` ; `PillMetrics.CardWidth` = 340 × CardScale (la réserve de la fenêtre suit).
- Boutons : `CardButton` restylé (fond `SurfaceHover`, texte `Fg`, 13, padding 8/12, rayon 10) ; contenu = `StackPanel` horizontal icône (16, `Fg`) + texte.
- `WindowsMediaSession.RefreshAsync` : `session.GetTimelineProperties()` → `Position`, `EndTime`, `LastUpdatedTime` ; la carte avance la position localement à la seconde tant que `Playing` (le tick d'une seconde existe déjà dans `Controller`).

## 4. Réglages

- Éditeur de pilule : « Forme des cellules » (Combo : Rondes / Carrées).
- Éditeur de cellule, section Affichage : « Légende » (Auto / Aucune), « Activité » (Par défaut / Pastille / Anneau animé).
- Général → nouvelle carte « Apparence » : « Indicateur d'activité » (Pastille / Anneau animé), « Échelle de la carte » (curseur 100–150 %). Écrit dans `cells.json` → `appearance` (partagé).

## 5. Tests

`CellViewTests` : forme/activité/légende résolues (pilule carrée → Shape square ; cellule `activity` surcharge l'appearance ; `caption:false` et défaut média → ShowCaption false) ; `ConfigValidationTests` : valeurs refusées (`cells_shape: "hex"`, `card_scale: 3`) ; `SquareArcTests` : fraction 0 → vide, 1 → périmètre complet, 0.25 → un quart, géométrie figée ; `CardContentTests` : sous-titre de synthèse, « % · absolu », actions média sans libellé ; `MediaSourceTests` : ligne `position` présente seulement avec timeline, `fallbackOpen` sans session → action par défaut `open`.

## 6. Hors périmètre

Thème de la pilule elle-même (reste noire), animations, glisser-déposer des cellules, 0.3.0 Claude Code.
