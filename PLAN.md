# Mental-o (untitled-MM) — Plan de projet

> Logiciel de carte mentale (mind-map) pour Windows, inspiré de Scapple : simple, efficace, entièrement personnalisé. Interface 100 % en français.

## 1. Choix technique recommandé

**C# + WPF sur .NET Framework 4.8** (préinstallé sur Windows 10/11 → zéro dépendance à installer, ni pour toi ni pour les utilisateurs).

Pourquoi WPF plutôt qu'autre chose :

| Besoin de la spec | Ce que WPF offre nativement |
|---|---|
| Canevas déplaçable avec éléments draggables | `Canvas` + transformations (pan/zoom fluides, rendu vectoriel retenu) |
| Texte riche formaté (couleur, taille, police, variantes, alignement) | `RichTextBox` / `FlowDocument` — tout est intégré, c'est LE point fort de WPF |
| Boîtes personnalisables (couleur, bordure) | `Border` stylable (épaisseur, pointillés, coins arrondis, fond) |
| Liens personnalisables (couleur, type de trait) | `Path` / courbes de Bézier avec `StrokeDashArray` |
| Import d'images redimensionnables | `Image` + `BitmapImage`, redimensionnement par poignées |
| Groupes de boîtes | Conteneurs logiques + rectangle englobant |

### Alternatives écartées
- **WinForms (C#)** : plus simple, mais le texte riche et le dessin vectoriel (GDI+) rendraient les liens et le formatage laborieux. Mauvais rapport effort/résultat pour un mind-map.
- **Electron / web** : très lourd en dépendances (Node, Chromium ~200 Mo). Contraire à l'objectif.
- **C++ / Direct2D** : performance maximale mais 5 à 10× plus de travail pour le même résultat.
- **.NET 8 + WPF** : moderne, mais exige l'installation du SDK/runtime .NET 8. Option possible si tu préfères du C# récent (voir « Décisions ouvertes »).

### Compilation
Deux voies possibles avec le Framework 4.8 (déjà sur ta machine) :
1. **`csc` pur, sans XAML** — toute l'interface en code C#, comme Stargazer. Un seul `.cs` → un `.exe`. Cohérent avec tes habitudes.
2. **`msbuild` intégré au Framework** (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe`) avec XAML — plus idiomatique WPF, mais structure de projet plus lourde.

**Recommandation : voie 1 (csc, code-only)** — WPF fonctionne très bien sans XAML, et ça reste dans l'esprit « léger » du projet.

## 2. Architecture

Séparation nette **document / rendu / interactions** :

```
untitled-MM/
├── src/
│   ├── Modele/          # Données pures (sérialisables)
│   │   ├── Document.cs      # la carte : liste de nœuds, liens, groupes
│   │   ├── Noeud.cs         # position, taille, contenu (texte riche ou image), style
│   │   ├── Lien.cs          # source, cible, style (couleur, trait, flèche ?)
│   │   └── Groupe.cs        # ensemble de nœuds + style du cadre
│   ├── Vue/             # Rendu WPF
│   │   ├── ToileCanvas.cs   # canevas principal : pan, zoom, rendu des éléments
│   │   ├── NoeudVisuel.cs   # boîte à l'écran (texte riche / image)
│   │   └── LienVisuel.cs    # tracé des connexions
│   ├── Interactions/    # Souris & clavier
│   │   ├── Deplacement.cs   # drag des boîtes, du canevas
│   │   ├── Connexion.cs     # drag-and-drop boîte → boîte = lien
│   │   └── Selection.cs     # simple, multiple, rectangle de sélection
│   ├── Persistance/     # Ouvrir / enregistrer
│   └── Programme.cs     # point d'entrée, fenêtre principale, barre d'outils
├── PLAN.md
└── build.bat            # compilation csc en une commande
```

### Format de fichier
**Conteneur ZIP** avec extension personnalisée (ex. `.carte`) :
```
fichier.carte (zip)
├── document.json    # nœuds, liens, groupes, styles, positions
└── images/          # images importées (fichiers originaux, ≤ 5 Mo chacun)
```
- JSON lisible et déboggable ; images stockées telles quelles (pas de base64 qui gonfle de 33 %).
- Le texte riche est sérialisé en « runs » : `[{texte, police, taille, couleur, gras, italique, ...}]` — indépendant de WPF, portable si un jour tu veux un port (comme Stargazer/mac).

## 3. Fonctionnalités — découpage en versions

### v0.1 — Le canevas ✏️
- Fenêtre principale, canevas infini avec pan (clic molette / espace+drag) et zoom (Ctrl+molette).
- Double-clic sur le vide → crée une boîte, on tape du texte (comme Scapple).
- Déplacement des boîtes à la souris ; suppression (Suppr).
- Enregistrer / Ouvrir (`.carte`), Nouveau, indicateur de modifications non enregistrées.

### v0.2 — Les liens 🔗
- Glisser une boîte **sur** une autre = créer un lien (comportement Scapple).
- Alternative : poignée de connexion au survol (à trancher à l'usage).
- Style des liens : couleur, épaisseur, trait (plein / pointillé / tirets), flèche (aucune / simple / double).
- Suppression d'un lien (sélection + Suppr).

### v0.3 — Texte riche 🎨
- Édition dans la boîte via `RichTextBox` intégrée.
- Barre de formatage : police, taille, **gras**, *italique*, souligné, barré, couleur du texte, surlignage, alignement (gauche/centre/droite).
- Formatage partiel (une sélection dans la boîte, pas toute la boîte).

### v0.4 — Style des boîtes 🖌️
- Couleur de fond, couleur/épaisseur/type de bordure (pleine, pointillée, aucune), coins arrondis.
- Styles par défaut + « recopier le style » d'une boîte à l'autre.

### v0.5 — Images 🖼️
- Import par bouton, glisser-déposer depuis l'Explorateur, ou coller (Ctrl+V).
- Limite 5 Mo (message d'erreur clair sinon).
- Redimensionnement par poignées, proportions conservées (Maj pour libérer).
- L'image vit dans une boîte : elle profite des styles de boîte et des liens.

### v0.6 — Groupes 📦
- Sélection multiple (Ctrl+clic, rectangle de sélection) → « Grouper ».
- Le groupe se déplace d'un bloc ; cadre visuel stylable ; Dégrouper.

### v0.7 — Confort ⚙️ *(complété au fil de l'eau)*
- Annuler / Rétablir (Ctrl+Z / Ctrl+Y) — journal de commandes.
- Copier / coller / dupliquer des boîtes (avec styles).
- Alignement automatique optionnel (magnétisme léger), raccourcis clavier.

### v0.8 — Modes d'interaction 🎛️ *(décidé le 31/07/2026, après les fonctions cœur)*
- Sélecteur en haut de la fenêtre pour basculer entre :
  - **mode automatique** (comportement actuel, tout activé),
  - **mode déplacement** (pas de création de liens, seulement bouger les boîtes),
  - **mode liens** (pas de déplacement de boîtes, seulement les liens),
  - et d'autres options à imaginer d'ici là.
- Ligne directrice UX : le **clic droit** est le foyer des options de personnalisation.

### v1.0 — Finitions 🚀
- Export PNG (et PDF ?) de la carte.
- Association du `.carte` à l'appli (double-clic dans l'Explorateur).
- Installeur (comme les `setup-stargazer-*.exe`) + icône.
- Récupération après plantage (sauvegarde auto périodique).

## 4. Risques identifiés

1. **`RichTextBox` dans un canevas zoomable** : l'édition en place avec zoom demande du soin (basculer affichage figé ↔ éditeur actif au double-clic). Solution éprouvée, mais c'est le morceau le plus délicat.
2. **Performance à grande échelle** (500+ boîtes) : WPF tient bien la charge ; si besoin, virtualisation des éléments hors écran en v1.0+.
3. **Annuler/Rétablir** : à concevoir dès le début (chaque action = une commande réversible), sinon c'est très coûteux à rattraper. Prévu dans l'architecture dès la v0.1 même si le raccourci n'arrive qu'en v0.7.

## 5. Décisions actées (31/07/2026)

| # | Question | Décision |
|---|---|---|
| 1 | Plateforme .NET | **Framework 4.8** (zéro install ; on pourra migrer plus tard au besoin) |
| 2 | Style de code | **csc code-only** (comme Stargazer) |
| 3 | Geste de connexion | **Poignées draggables** — plus de flexibilité (v0.2) |
| 4 | Extension de fichier | **`.tea`** (actée le 03/08/2026 — « très à propos avec la menthe ! » ; l'ancien `.zeubi` reste lisible) |
| 5 | Nom du logiciel | **Mental-o** (provisoire) |

## 6. Brainstorm avant la v1.0 (31/07/2026)

Vivier d'idées à trier ensemble — rien n'est engagé, tout est discutable.

### Confort d'édition (le cœur de la v0.7)
- **Copier/coller/dupliquer des boîtes** (avec styles, et liens internes quand on copie plusieurs boîtes).
- **Magnétisme et guides d'alignement** : aimant léger sur les bords/centres des voisines, guides visuels pendant le glisser.
- **Alignement et distribution** : aligner gauche/centre/droite, distribuer horizontalement/verticalement (sélection multiple).
- **Recherche** (Ctrl+F) : trouver une boîte par son texte, naviguer entre les occurrences, zoom automatique.
- **Navigation clavier** : flèches pour passer de boîte en boîte, F2 pour éditer.
- **Styles favoris** : une petite palette de styles de boîte enregistrés, réutilisables d'un clic.

### Liens
- **Étiquettes sur les liens** (texte posé au milieu du trait).
- **Liens courbes** (Bézier) avec poignée de courbure ; liens en équerre.
- **Lier des groupes** (un lien qui part du cadre).

### Apparence de la carte
- **Thèmes** : fond de canevas (couleur, pointillé, quadrillé), styles par défaut assortis, mode sombre.
- **Ombres et transparences réglables** ; bordure différente par côté.

### Organisation des grandes cartes
- **Plier/déplier un groupe** (le réduire à une pastille-titre).
- **Mini-carte** (aperçu global cliquable dans un coin).
- **Zoom vers la sélection** ; « zoom pour tout voir ».
- **Étiquettes/tags** sur les boîtes + filtre d'affichage.
- **Calques** (arrière-plan/premier plan verrouillables).

### Contenu
- **Hyperliens cliquables** dans le texte ; **notes cachées** par boîte (infobulle) ; pièces jointes.

### Import / export
- **Export PNG / SVG / PDF** de la carte (ou de la sélection).
- **Export texte structuré** (Markdown, OPML — compatible autres logiciels de mind-map).
- **Import d'un texte à puces** → arbre de boîtes liées.
- **Impression**.

### Système (v1.0)
- **Sauvegarde automatique + récupération après plantage** ; fichiers récents.
- **Installeur** (comme Stargazer) ; **nom définitif de l'extension** (adieu `.zeubi` ? 😄) ; onglets ou multi-fenêtres.

### Modes futurs (le sélecteur est prêt à en accueillir)
- **Mode présentation** : plein écran, lecture seule, navigation de boîte en boîte.
- **Mode chaîne** : chaque nouvelle boîte se lie automatiquement à la précédente (prise de notes rapide).
- **Mode verrouillé** : tout figé, juste la consultation et le pan/zoom.

## 7. Comparatif avec Scapple (03/08/2026)

> Sources : site et fiche App Store de Literature & Latte (liste officielle des fonctions,
> historique des versions) + revues détaillées. Versions de référence : Scapple 1.5.4 (macOS)
> / 1.4.2 (Windows) — un logiciel volontairement minimaliste, quasi figé depuis des années.

### Cœur du canevas

| Fonction | Scapple | Mental-o | Verdict |
|---|---|---|---|
| Double-clic sur le vide → note | ✔ | ✔ | Égalité |
| Placement 100 % libre, canevas « infini » | ✔ | ✔ | Égalité |
| Création de lien | glisser une note **sur** une autre | poignées sur les bords (choix v0.2) | Gestes différents, même service |
| Flèches simples / doubles | ✔ | ✔ | Égalité |
| Trait plein / tirets / pointillé | ✔ (pointillé par défaut) | ✔ | Égalité |
| **Étiquettes sur les liens** | ✔ | ✔ (v0.12) | Égalité |
| Liens **courbes** et en **équerre** | ✘ (droites uniquement, regret connu des utilisateurs) | ✔ + poignée de courbure | **Mental-o devant** |
| Insérer une note **au milieu d'un lien existant** | ✔ | ✔ (v0.13, clic droit sur le lien) | Égalité |
| Piles de notes (« stacks » : colonnes auto-alignées) | ✔ | ✔ « Empiler la sélection » (v0.13) | Égalité |
| Formes d'arrière-plan / notes aimantées | ✔ (formes libres, les notes posées dessus suivent) | ✔ via les groupes (cadre stylable, déplacement en bloc, titres) | Égalité, philosophies différentes |
| Liens depuis un groupe/cadre | ✘ | ✔ (v0.10) | **Mental-o devant** |

### Notes et style

| Fonction | Scapple | Mental-o | Verdict |
|---|---|---|---|
| Bordure, fond, couleur, coins | ✔ | ✔ (+ trait tirets/pointillé) | Égalité+ |
| **Texte riche multi-styles dans une même note** | partiel (style par note surtout) | ✔ (segments : police/taille/couleur/gras… par sélection, alignement par ligne) | **Mental-o devant** |
| Listes à puces dans une note | ✘ | ✔ (v0.9.5) | **Mental-o devant** |
| Ombre et opacité des notes | ✔ (fade/apparence) | ✔ (v0.12) | Égalité |
| Palette de styles réutilisables | ✔ (Inspector + **Cmd-Opt-1 à 9**) | ✔ styles favoris (clic droit) | Égalité — les raccourcis clavier manquent |
| Styles par défaut du document | ✔ | ✔ (v0.12, par fichier : boîtes, liens **et** groupes) | **Mental-o devant** |
| Images dans les notes | ✔ | ✔ (+ texte avant/après l'image) | Égalité+ |
| Équations MathType | ✔ (Mac) | ✘ | Hors périmètre |
| Correcteur orthographique (système) | ✔ | ✘ | À envisager (v1.0+) |

### Vue et confort

| Fonction | Scapple | Mental-o | Verdict |
|---|---|---|---|
| Zoom, pan, molette | ✔ (+ « Z » maintenu = vue globale) | ✔ | Égalité — « zoom pour tout voir » à piquer (déjà au §6) |
| Plein écran | ✔ | ✔ F11 + **mode présentation** F5 (v0.13) | **Mental-o devant** |
| Recherche | ✔ | ✔ (Ctrl+F, navigation, centrage) | Égalité |
| Alignement / distribution | ✔ | ✔ + magnétisme et guides au glisser | **Mental-o devant** |
| Mini-carte, inspecteur, historique flottants | ✘ | ✔ | **Mental-o devant** |
| Modes d'interaction (auto/déplacement/liens/lecture) | ✘ | ✔ | **Mental-o devant** |
| Fond du canevas personnalisable | ✔ (+ **image en mosaïque**) | ✔ couleur + motifs (pas d'image) | Presque égalité |
| Mode sombre | ✔ | ✔ (thème complet re-templété) | Égalité |
| Raccourcis personnalisables + profils | ✘ | ✔ | **Mental-o devant** |
| Sauvegarde auto | ✔ (auto-save système Mac) | ✔ (3 min, option du fichier) | Égalité — la **récupération après plantage** reste à faire |
| iCloud / Quick Look | ✔ (Mac) | ✘ | Hors périmètre Windows |

### Import / export

| Fonction | Scapple | Mental-o | Verdict |
|---|---|---|---|
| Export PNG | ✔ | ✔ (échelle 2×) | Égalité |
| Export PDF | ✔ | ✔ (v0.13, via Microsoft Print to PDF) | Égalité |
| Export SVG | ✘ | ✔ | **Mental-o devant** |
| Export texte / liste à puces | ✔ (txt, liste indentée) | ✔ Markdown (arbre par liens) | Égalité+ |
| Export RTF / RTFD | ✔ | ✘ | Faible priorité |
| **Export OPML** (échange mind-map) | ✔ | ✔ (v0.13) | Égalité |
| Export dossier d'images | ✔ | ✘ | Faible priorité |
| **Impression** | ✔ | ✔ (v0.13, Ctrl+P, ajustée à la page) | Égalité |
| Import texte / fichiers par glisser | ✔ (txt, RTF, PDF, images) | images ✔ ; texte à puces ✔ (dialogue v0.12) — pas de dépôt direct de .txt | Presque égalité — dépôt de .txt à envisager |
| Import OPML | ✘ (demande récurrente jamais exaucée) | ✔ (v0.13) | **Mental-o devant** 😏 |
| Intégration Scrivener | ✔ (raison d'être) | ✘ | Hors périmètre |

### Bilan pour la roadmap

Mental-o couvre déjà l'essentiel de Scapple et le dépasse sur le texte riche, les liens
(courbes, équerre, groupes), les modes, les panneaux et la personnalisation. Les manques
réels face à Scapple, par ordre de valeur — **points 1 à 5 livrés en v0.13 (03/08/2026)** :

1. ~~**Insérer une note au milieu d'un lien**~~ ✔ v0.13 — clic droit sur le lien.
2. ~~**Export PDF + impression**~~ ✔ v0.13 — Fichier > Exporter > PDF, Imprimer (Ctrl+P).
3. ~~**Export OPML**~~ ✔ v0.13 — export **et import**, ce que Scapple refuse de faire
   depuis des années.
4. ~~**Piles / « empiler la sélection »**~~ ✔ v0.13 — menu Édition et clic droit.
5. ~~**Plein écran / mode présentation**~~ ✔ v0.13 — F11, et F5 pour la présentation.
6. **Divers v1.0+** (laissés pour plus tard) : correcteur orthographique, image de fond
   en mosaïque, dépôt direct de fichiers .txt, raccourcis 1-9 pour les styles favoris.

Ces points rejoignent le vivier du §6 ; le reste du §6 (mini-carte évoluée, tags/filtres,
calques, hyperliens/notes cachées, plier les groupes…) est constitué de fonctions que
Scapple n'a même pas — c'est là que Mental-o creuse son propre sillon. 🍵

## 8. Tour d'horizon des logiciels du genre (07/09/2026)

> Où en est Mental-o (v0.14) face au vivier du §6, et ce que font les autres — outils de
> canevas libre (Scapple 1.4.2 Win / 1.5.4 Mac, Obsidian Canvas + format ouvert JSON
> Canvas, Heptabase, Milanote, Excalidraw) et arbres classiques (Freeplane, SimpleMind,
> MindNode, XMind) — d'après leurs pages officielles, notes de version et revues 2026.
> Objectif : repérer ce qui manque pour en faire quelque chose de plus solide, pas copier.

### Pointage du vivier §6 à la v0.14

| Rubrique | Livré | Reste |
|---|---|---|
| Confort d'édition | copier/coller/dupliquer, magnétisme + guides, aligner/distribuer, recherche, navigation clavier, styles favoris, empiler | raccourcis 1-9 des favoris |
| Liens | étiquettes, courbes + équerre, liens de groupes, insertion au milieu | — |
| Apparence | fond couleur + motifs, mode sombre, ombres, opacité, défauts par fichier | bordure différente par côté, image de fond |
| Grandes cartes | mini-carte, inspecteur, historique | **plier/déplier un groupe, zoom sélection / tout voir, tags + filtre, calques** |
| Contenu | images, **fichiers attachés intégrables au .tea (v0.14)** | **hyperliens web dans une boîte, notes cachées** |
| Import / export | PNG, SVG, PDF, Markdown, OPML (2 sens), texte à puces, impression | dépôt direct de .txt/.md |
| Système | sauvegarde auto 3 min, récents, `.tea`, association, mode présentation, mode lecture (= « verrouillé ») | **récupération après plantage**, installeur, onglets/multi-fenêtres, correcteur |
| Modes | automatique, déplacement, liens, lecture, présentation | mode chaîne (prise de notes au clavier) |

Le §6 est livré aux deux tiers ; ce qui reste est concentré sur l'**organisation des grandes
cartes** et la **robustesse système** — exactement ce que le marché confirme ci-dessous.

### Ce que font les autres et que Mental-o n'a pas

| Fonction | Qui l'a | Intérêt pour Mental-o |
|---|---|---|
| **Zoom pour tout voir / sur la sélection** | Obsidian (Maj+1 / Maj+2), Scapple (touche Z), tous | Évident, trivial, attendu partout |
| **Plier/déplier** (branche, groupe, section) | Freeplane, SimpleMind, MindNode, XMind, Heptabase (sections) | Seule vraie arme contre les grandes cartes ; nos groupes titrés s'y prêtent (réduire à la pastille-titre) |
| **Notes cachées** par boîte (infobulle, icône discrète) | SimpleMind, MindNode, XMind, Freeplane (notes + détails) | Garder la carte aérée sans perdre le détail ; complète les fichiers attachés |
| **Hyperliens web** cliquables (carte-lien avec aperçu chez Obsidian, « web links » XMind) | Obsidian, XMind, Freeplane, Milanote | Le rail « fichiers » de la v0.14 accueille naturellement une variante URL |
| **Lien vers une autre boîte** (ancre interne, « topic link ») | XMind, MindNode (cross-document), Obsidian | Navigation dans une grosse carte ; Ctrl+clic → recentrer sur la cible |
| **Tags / étiquettes visuelles + filtre** | MindNode (visual tags), Freeplane (filtres sauvegardés), XMind (labels), Heptabase | Classer sans hiérarchie ; le filtre estompe le reste (« focus mode » MindNode) |
| **Cases à cocher / tâches + progression** | SimpleMind (cases + barres), MindNode (tâches), XMind (to-do, Gantt) | Une puce « ☐ » cliquable dans nos listes, progression par groupe |
| **Icônes / marqueurs / stickers** sur les boîtes | tous les arbres classiques, MindNode (250 stickers), Freeplane | Repère visuel rapide ; nos SVG Phosphor embarqués font déjà le socle |
| **Vue plan (outline)** synchronisée avec la carte | Freeplane, SimpleMind, MindNode, XMind | Notre export Markdown calcule déjà l'arbre par liens : l'afficher en panneau et le rendre cliquable |
| **Ranger en arbre** (auto-disposition d'une sélection : radial, horizontal, vertical) | SimpleMind (7 modes), MindNode, XMind, Whimsical | Complément d'« Empiler » : ranger un sous-arbre d'un clic, sans renoncer au placement libre |
| **Création au clavier** (Entrée = sœur, Tab = enfant lié) | Whimsical, XMind, Freeplane, Scapple (raccourcis étendus) | C'est notre « mode chaîne » du §6 ; le geste clavier vaut mieux qu'un mode |
| **Récupération après plantage** (cartes non enregistrées restaurées) | Scapple 1.4 (« restoration of previously-open unsaved boards »), Obsidian, Heptabase | Condition de la confiance ; on a l'autosave, pas le filet |
| **Correcteur orthographique** | Scapple (Hunspell), Freeplane, XMind | Dernier manque face à Scapple ; Marabook embarque déjà Hunspell + dictionnaires |
| **Historique de versions** | Coggle (illimité), Heptabase, Milanote | Snapshots légers dans le .tea (modèle Marabook), « revenir à hier » |
| **Format d'échange ouvert JSON Canvas** (`.canvas`) | Obsidian, Kinopio, Kosmik… | Nœuds text/file/link/group + arêtes avec côtés, flèches, couleur, étiquette : correspondance quasi directe avec boîtes/fichiers/groupes/liens — import **et** export à portée |
| **Modèles de cartes + thèmes assortis** | XMind (templates, thèmes), SimpleMind (15 feuilles de style), Milanote, Whimsical | Un .tea vierge « modèle » + thèmes fond/boîte/lien/groupe cohérents |
| **Présentation par diapositives** (cadres → slides, pointeur laser) | Excalidraw (frames + laser), XMind (Pitch), MindMeister | Nos groupes = diapositives, F5 enchaîne ; le pointeur laser est un gadget de présentation apprécié |
| **Dépôt direct de fichiers texte** (txt/md/odt → boîtes) | Scapple (txt, RTF, ODT, PDF) | Petit ; le dépôt .txt était déjà noté au §7 |
| **Gestes de sélection** : Ctrl+A, Alt+rectangle pour désélectionner, Alt+glisser = dupliquer, Maj = contraindre l'axe, Ctrl+flèches = déplacer | Scapple 1.4, Obsidian | Finitions bon marché |
| Formes d'arrière-plan « aimantées » | Scapple | Couvert par nos groupes (voir §7) |
| Équations LaTeX, notes audio, IA, collaboration temps réel, cloud/mobile, clipper web | XMind, MindNode, Heptabase, Miro… | **Hors périmètre** : Mental-o est local, léger, sans compte |

### Ce que Mental-o fait déjà mieux (à préserver)

Texte riche par sélection et alignement par ligne (personne d'autre dans les canevas
libres), liens courbes/équerre avec poignée, liens depuis un cadre, modes d'interaction,
raccourcis re-liables avec profils, défauts par fichier pour boîtes/liens/groupes, export
SVG et import OPML (que Scapple n'a toujours pas), fichiers attachés **intégrables** au
document (Obsidian et Heptabase ne font que référencer), mini-carte/inspecteur/historique,
et un exe de quelques centaines de Ko sans dépendance.

### Recommandation — dix chantiers, par valeur décroissante

1. **Zoom pour tout voir / sur la sélection** (+ Ctrl+A, Alt+rectangle) — une soirée, attendu par tous.
2. **Plier/déplier un groupe** en pastille-titre (liens redirigés vers la pastille, état enregistré).
3. **Notes cachées + hyperliens web** dans une boîte, sur le rail des fichiers attachés de la v0.14 (URL = PieceJointe sans octets ; note = champ texte, icône coin, infobulle, dialogue d'édition).
4. **Tags de couleur + filtre estompant** (panneau Fenêtres, recherche existante réutilisée) ; « focus » sur la sélection.
5. **Cases à cocher** cliquables dans les listes (puce « ☐/☑ » de PucesTexte) + progression affichée dans le titre de groupe.
6. **Récupération après plantage** : copie de secours horodatée dans %APPDATA% à chaque autosave, proposée au démarrage suivant ; puis versions légères dans le .tea.
7. **Correcteur orthographique** (Hunspell + dictionnaire fr repris de Marabook, soulignement dans l'éditeur, clic droit = suggestions).
8. **Import/export JSON Canvas** (Obsidian & co) + dépôt direct de .txt/.md (titres/puces → arbre) ; FreeMind .mm si l'OPML ne suffit pas.
9. **Création au clavier** (Entrée = boîte sœur, Tab = boîte enfant liée, dans l'axe libre) + **Ranger en arbre** (disposition automatique d'une sélection : horizontal / vertical / radial).
10. **Présentation par groupes** (chaque groupe = une diapositive, ordre réglable, pointeur laser) + **modèles de cartes** et **thèmes assortis**.

Système v1.0 (inchangé) : installeur auto-extracteur (celui de Gallerizz est réutilisable),
onglets ou multi-fenêtres, mise à jour signalée. Les points 1 à 5 forment une v0.15
« grandes cartes » cohérente ; 6 et 7 une v0.16 « confiance » ; 8 à 10 la v0.17 avant la v1.0.

**Bilan du 07/09/2026 : les dix chantiers sont livrés** (v0.15, v0.16, v0.17, même journée).
Écarts assumés par rapport au plan : le correcteur s'appuie sur celui de Windows via WPF
(aucun dictionnaire livré, exe inchangé) plutôt que sur Hunspell ; FreeMind `.mm` n'a pas été
ajouté (l'OPML et JSON Canvas couvrent l'échange) ; la vue plan (outline) reste dans le vivier.

> Sources : [Scapple 1.4 for Windows](https://www.literatureandlatte.com/latest/scapple-1-4-for-windows), [notes de version Scapple](https://www.literatureandlatte.com/scapple/release-notes?os=Windows), [Scapple 1.5.4 macOS](https://forum.literatureandlatte.com/t/scapple-1-5-4-for-macos-released/152204), [Obsidian Canvas](https://obsidian.md/help/plugins/canvas), [JSON Canvas 1.0](https://jsoncanvas.org/spec/1.0/), [Freeplane](https://docs.freeplane.org/), [SimpleMind](https://simplemind.eu/features/), [MindNode](https://www.mindnode.com/features), [Xmind](https://xmind.com/features/), [Milanote](https://milanote.com/product), [Heptabase](https://heptabase.com/), [Excalidraw+](https://plus.excalidraw.com/), [Storyflow — mind mapping tools 2026](https://storyflow.so/blog/best-mind-mapping-tools-2025), [Zapier — best mind mapping software 2026](https://zapier.com/blog/best-mind-mapping-software/), [Heptabase alternatives 2026](https://storyflow.so/blog/best-heptabase-alternatives-2026).

## 9. Journal

- **12/09/2026** — **v0.18 « système » livrée** (le premier volet de la v1.0 ; les onglets/multi-fenêtres sont écartés pour rester mono-fichier). **Mises à jour** : menu **Aide** (remplace « ? ») > **Vérifier les mises à jour…** — dernière release GitHub du dépôt (`Persistance/MiseAJour.cs`, standard de la famille Stargazer), comparaison avec `VERSION`, notes de la release et **Installer et redémarrer** (`Vue/DialogueMiseAJour.cs` : archive portable déballée en temp, script cmd qui attend la fermeture, xcopy par-dessus, relance ; réglages en %APPDATA% donc rien à préserver) ; vérification silencieuse au lancement → « Aide ● » en or + mention cliquable à gauche de la barre d'état ; À propos lit `VERSION`. **Installeur** `outils/setup-stub.cs` (recette Typonanny, habillage nuit/or) : dossier au choix (%LOCALAPPDATA%\Programs\Mental-o), raccourcis menu Démarrer/Bureau, **association .tea/.zeubi** (case cochée par défaut, retirée à la désinstallation seulement si elle pointe encore sur cet exe), entrée « Applications installées » avec désinstallation (`--silent`, `--uninstall`). **`outils/release.ps1`** fabrique `dist/mental-o-windows-portable.zip` (exe, VERSION, manifeste, logo, icônes) + `dist/Mental-o-Setup-Windows.exe` — noms stables sans version pour `releases/latest/download/…` et le hub. `VERSION` passe à `0.18.0` (majeure.mineure.correctif, exigé par le script). Testé : installation/désinstallation silencieuses, mise à jour de bout en bout contre une fausse release locale. **`outils/extraire-textes-ui.ps1`** → `TEXTES-UI.md` (gitignoré) : tous les textes de l'interface avec fichier et ligne, pour la passe de ton. Reste : publier la première release (`gh release create v0.18.0 …`), les boutons du README (hands-off), puis vue plan / ancres internes / finitions de sélection.
- **07/09/2026** — **v0.17 « interop et présentation » livrée** (chantiers 8 à 10 du §8) : **JSON Canvas** export/import (Obsidian ; cartes texte/fichier/lien/groupe, arêtes fléchées, dossier `<nom>_fichiers`, appartenance aux groupes par géométrie) ; **dépôt de .txt/.md** → boîtes (titres Markdown `#` compris, Alt = lier) ; **création au clavier** (Entrée = sœur, Tab = enfant fléché, création annulée si vide) ; **Ranger en arbre** horizontal/vertical/radial (racine en place, une annulation) ; **présentation par diapositives** (chaque groupe = diapo, ordre au clic droit, →/←/Début, compteur, **pointeur laser** L) ; **modèles de cartes** (%APPDATA%\Mental-o\modeles) ; **6 thèmes de carte** assortis (Papier, Ardoise, Encre, Menthe, Pastel, Craie) appliqués aux défauts et, au choix, aux éléments existants. Format `.tea` v16. Sonde verte. **Les dix chantiers du §8 sont livrés** ; reste la v1.0 système (installeur, onglets).
- **07/09/2026** — **v0.16 « confiance » livrée** (chantiers 6 et 7 du §8) : **copies de secours** chaque minute dans %APPDATA%\Mental-o\secours (cartes jamais enregistrées comprises), effacées à l'enregistrement ou à la fermeture normale, **récupération proposée au démarrage** (la plus récente rouverte, les autres rangées dans Documents) ; **versions antérieures dans le .tea** (`versions/aaaa-mm-jj_hh-mm.json`, 10 au plus, fusion sous dix minutes, Fichier > Versions antérieures… restaure à l'écran) ; **correcteur orthographique** de Windows via WPF (Paramètres > Édition : activé, langue), clic droit = corrections / ignorer / dictionnaire personnel `dictionnaire.lex`. Sonde verte.
- **07/09/2026** — **v0.15 « grandes cartes » livrée** (chantiers 1 à 5 du §8) : Tout voir (Ctrl+Maj+0) / zoom sur la sélection (Ctrl+Maj+1) / Ctrl+A / Alt+rectangle ; **plier un groupe** en pastille « ▸ Titre · n » (liens extérieurs rabattus, intérieurs masqués, double-clic déplie, recherche et flèches déplient) ; **notes cachées** (icône coin + infobulle, Ctrl+Maj+N) ; **étiquettes** par fichier (puces colorées, sous-menu clic droit, dialogue de gestion, panneau Fenêtres > Filtre qui estompe à 15 %, mise en avant de la sélection Ctrl+Maj+M) ; **cases à cocher** (bouton ☐, clic pour cocher, avancement « 3/5 » dans le titre du groupe) ; **liens web** (Ctrl+Maj+L, dépôt d'adresse ou de texte). Format `.tea` v15. ToileVue éclatée en partiels Vue/Pliage/Annotations. Sonde hors écran verte.
- **07/09/2026** — **v0.14 livrée** : DialogueMessage (dialogues au thème, visuels Marabook) remplace les MessageBox ; **fichiers attachés** aux boîtes (menu Insertion, dépôt, clic droit ; lien à la ligne avec icône ; ouvrir/renommer/remplacer/**intégrer au .tea**/copie/détacher/supprimer ; format v14) ; nouvelle icône, en-tête du README, manifeste Stargazer. **§8 ajouté** : tour d'horizon du marché et dix chantiers priorisés vers la v1.0.
- **31/07/2026** — Plan validé, décisions actées. Démarrage de la v0.1.
- **31/07/2026** — **v0.1 livrée** : canevas (pan/zoom), création de boîtes au double-clic, édition du texte, déplacement, suppression, sélection, enregistrement/ouverture `.zeubi`, annuler/rétablir (bonus anticipé de la v0.7). Compilé avec csc, exe de 31 Ko. Prochaine étape : v0.2 (liens par poignées draggables).
- **31/07/2026** — **v0.2 livrée** : poignée bleue au survol d'une boîte → tirer vers une autre boîte pour créer un lien (surbrillance verte de la cible, doublons refusés) ; clic droit sur un lien → style (8 couleurs, 4 épaisseurs, plein/tirets/pointillé, flèches simple/double) ; sélection et suppression des liens ; suppression d'une boîte = suppression de ses liens (restaurés par Ctrl+Z) ; format `.zeubi` v2 (rétrocompatible v1). Bonus : icônes générées (`outils\generer-icones.bat`), icône embarquée dans l'exe, association `.zeubi` (`outils\associer-zeubi.bat` / `dissocier-zeubi.bat`). Prochaine étape : v0.3 (texte riche).
- **31/07/2026** — **v0.2.1** (retour utilisateur) : les poignées ne disparaissent plus quand on sort de la boîte pour les attraper (zone de grâce de 28 px autour de la boîte) ; 4 poignées, une au centre de chaque bordure, pour choisir le point de départ du lien ; surbrillance au survol (poignées épaissies quand la boîte est survolée, davantage encore sous le curseur).
- **31/07/2026** — **v0.3 livrée** : texte riche. Modèle en « segments » stylés (police, taille, couleur, gras/italique/souligné/barré, surlignage) + alignement par boîte ; affichage par `TextBlock` à inlines, édition par `RichTextBox` ; barre de formatage type traitement de texte en haut de la fenêtre (synchronisée avec la sélection) ; menu contextuel de formatage au clic droit sur une boîte et dans l'éditeur ; Ctrl+B/I/U ; format `.zeubi` v3 (rétrocompatible v1/v2). L'édition ne se ferme pas quand le focus part vers la barre ou un menu.
- **31/07/2026** — **v0.3.1** (retour utilisateur) : bordures unifiées à tous les zooms (désactivation de l'arrondi de mise en page et de l'accrochage aux pixels sur le canevas, qui les rendaient irrégulières) ; les boîtes neuves passent à la ligne vers ~30 caractères au lieu de s'étirer ; taille du texte saisissable librement (combo éditable + « Autre… » dans le menu, bornée 6–120) ; sélecteur de polices branché sur les polices système (combo éditable avec autocomplétion, sous-menu rempli à l'ouverture) ; couleurs personnalisées via un sélecteur (« Personnalisée… » dans les combos et menus), mémorisées **par fichier** dans le `.zeubi` (format v4) et affichées dans les listes.
- **31/07/2026** — **v0.4 livrée** : styles de boîtes, tout au clic droit. Fond (blanc, transparent, pastels, couleurs vives, personnalisées), bordure (couleur, épaisseur 1 à 5, trait plein/tirets/pointillé/aucune — demandes utilisateur), coins (carrés/arrondis/très arrondis). Copier/coller le style d'une boîte, « utiliser ce style pour les nouvelles boîtes ». Boîtes restructurées en fond+cadre `Rectangle` (le `Border` WPF ne sait pas dessiner de pointillés). Format `.zeubi` v5, rétrocompatible.
- **31/07/2026** — **v0.5 livrée** : images. Import par menu Insertion (Ctrl+Maj+I), glisser-déposer depuis l'Explorateur (plusieurs fichiers à la fois) ou Ctrl+V (fichiers copiés ou capture en presse-papiers, ré-encodée en PNG). Limite 5 Mo par image, messages d'erreur clairs. L'image vit dans une boîte : styles de boîte (fond, bordure, coins) et liens fonctionnent. Redimensionnement par poignée en bas à droite, proportions conservées (Maj pour libérer), annulable. Octets stockés dans `images/` du zip `.zeubi` (format v6). png/jpg/jpeg/gif/bmp.
- **31/07/2026** — **v0.5.1** (retour utilisateur) : correction du lien qui ne se créait pas malgré la surbrillance (le hit-test simple de WPF ignorait `IsHitTestVisible` : le trait provisoire se faisait viser à la place de la boîte — remplacé par un hit-test filtré qui traverse tout ce qui n'est pas une boîte) ; redimensionnement des boîtes texte à la poignée (règle la largeur de retour à la ligne, hauteur automatique, « Largeur automatique » au clic droit pour revenir) ; images dans les boîtes texte — « Insérer une image dans la boîte… » au clic droit ou dépôt d'un fichier sur la boîte, l'image s'affiche sous le texte et suit l'alignement (gauche/centre/droite), « Retirer l'image », texte ajoutable sur une boîte-image au double-clic. Format `.zeubi` v7 (largeur fixe).
- **31/07/2026** — **v0.5.2** (retour utilisateur) : la ligne vide née d'un double-clic sans saisie sur une boîte-image est purgée à la validation ; le texte s'écrit **avant et après** l'image — dans l'éditeur, l'image est un bloc que le curseur traverse aux flèches, et le texte « après » vit dans le modèle (`SegmentsApres`), aligné comme le reste ; inversion demandée des raccourcis : **Entrée = retour à la ligne, Maj+Entrée = valider**.
- **31/07/2026** — **v0.6 livrée** : groupes. Sélection multiple (Ctrl+clic et rectangle de sélection au glisser sur le fond, Ctrl pour ajouter) ; déplacement en bloc de la sélection ; Ctrl+G groupe (un nœud n'appartient qu'à un groupe, le nouveau absorbe), Ctrl+Maj+G dégroupe ; cadre en tirets arrondi teinté autour du groupe, cliquable (sélectionne et déplace le bloc) et stylable au clic droit (couleur) ; cliquer une boîte groupée sélectionne tout le groupe ; suppression multiple en une étape d'annulation (liens partagés capturés une seule fois) ; l'appartenance aux groupes survit à suppression/annulation. Format `.zeubi` v8 (texte après image + groupes).
- **31/07/2026** — **v0.6.1** (retour utilisateur) : second clic sur un groupe sélectionné → la boîte cliquée s'isole (déplaçable et redimensionnable seule, la poignée revient) ; le cadre du groupe se redimensionne à la poignée (il peut excéder son contenu, jamais être plus petit, taille enregistrée) ; « Personnalisée… » et couleurs du fichier dans la couleur de cadre ; commande « Réajuster au contenu » sur les boîtes (largeur auto + image à l'échelle d'import) et les groupes (cadre au contenu). Format v9.
- **31/07/2026** — **v0.8 livrée** (avancée avant la v0.7, comme convenu) : sélecteur de modes en haut de la fenêtre + Ctrl+1/2/3 — **Automatique** (tout), **Déplacement** (poignées de lien désactivées), **Liens** (boîtes et groupes immobiles). Architecture extensible pour les modes futurs.
- **31/07/2026** — **v0.9 livrée : les menus.** Barres de personnalisation en accordéon sous la barre des modes : barre **texte** (n'apparaît que quand une boîte est ciblée ou en édition), barre **boîtes** (fond, bordure, coins, réajuster — sans passer par le clic droit), barre **groupes** (couleur du cadre, **titre de groupe** posé sur le trait qu'il coupe visuellement, position haut/bas × gauche/droite, réajuster, dégrouper). Icônes Segoe MDL2 sur les alignements, les modes et « réajuster » (infobulles). **Fichier > Préférences de fichier** (onglets Général/Personnalisation/Texte — padding des boîtes et texte par défaut, enregistrés dans le `.zeubi` v10). **Fichier > Paramètres** : onglet Raccourcis, chaque action re-liable (tableau par catégorie), favoris ★ ; stockage `%APPDATA%\Mental-o\parametres.json`. **Menu Fenêtres > Tust des raccourcis** : mémo flottant en haut à droite du tableau (raccourcis de base + favoris), visibilité mémorisée.
- **31/07/2026** — **v0.9.5** (retours + chantiers 1-4). Corrections : cliquer un cadre de groupe sélectionne **le groupe seul** (surbrillance du cadre, personnalisation directe, glisser = déplacement du bloc, Suppr = dégrouper) ; Tust en une ligne par raccourci, sans Entrée. **Abandon de l'accordéon** : barre de personnalisation unique et permanente, pilotée par des **onglets Texte/Boîte/Groupe/Lien** dans la barre des modes (bascule automatique selon la sélection, contenu grisé sans cible) — dont le nouveau **panneau Lien** (couleur, épaisseur, trait, flèches). **Listes à puces** (•, –, 1., un niveau, boîte entière, préfixes portés par le texte donc stylables et portables) ; l'auto-liste « * »/« - »+espace est **désactivée par défaut** et activable dans Paramètres > Édition. Chantiers : **Personnalisation** (fond du tableau couleur + motif uni/pointillé/quadrillé, gestion des couleurs personnalisées du fichier) ; **Général** (nom de carte affiché dans le titre, auteur, statistiques, **sauvegarde automatique** 3 min) ; **panneaux flottants** mini-carte cliquable, inspecteur de sélection, historique ; **raccourcis 2.0** : détection de conflits, export/import de profils json, neutralisation des gestes système Fichier (Annuler/Coller conservent les leurs — les éditeurs en dépendent). Format `.zeubi` v11.
- **31/07/2026** — **v0.10** (retours + suite du chantier menus, hors panneaux déplaçables/historique nommé/verrouillage fichier, exclus à la demande). **Liens de groupes** : les cadres portent les poignées au survol et se lient à toute boîte ou autre cadre (surbrillance verte du cadre cible, géométrie suivie, liens capturés au dégroupage pour l'annulation). **Styles sur sélection** : pendant l'édition, puces et alignement ne touchent que les lignes/paragraphes sélectionnés — l'alignement devient **par ligne** dans le modèle (`Segment.AlignementLigne`, affichage par lignes empilées) ; hors édition, toute la boîte. **Groupes** : trait (tirets/plein/pointillé) et épaisseur du cadre, titre stylable (couleur, fond — qui « coupe » le trait —, taille). **Mode Lecture** (Ctrl+4) : navigation et consultation uniquement, tout est verrouillé. **Général** : dates de création/modification (horodatées à l'enregistrement) + miniature de la carte. **Onglet épinglable** (📌) résistant à la bascule automatique. **Paramètres > Apparence** : taille des poignées, vitesse du zoom. Format `.zeubi` v12.
- **31/07/2026** — **Icônes définitives** fournies par l'utilisateur (`ressources\apercu-mentalo-2.png` et `apercu-fichier-zeubi-2.png`, 128×128) : `generer-icones.bat` les convertit désormais en `.ico` multi-tailles (16→256, interpolation haute qualité) et ne revient au dessin généré que si ces PNG disparaissent. Exe recompilé avec la nouvelle icône, cache d'icônes rafraîchi.
- **31/07/2026** — **Icônes de menus personnalisées** : 15 SVG (style Phosphor) déposés par l'utilisateur dans `ressources\icons`, nommés par usage. `outils\generer-icones-menus.ps1` en extrait les tracés et génère `src\Vue\Icones.cs` (vecteurs `Path` embarqués, recolorables, nets à tous les DPI — aucun fichier livré avec l'exe). Branchés sur : les 4 modes, les 4 onglets de la barre de personnalisation (icône + libellé), les alignements et gras/italique/souligné/barré. Les glyphes Segoe MDL2 ne restent que là où aucune icône n'a été fournie (réajuster, épingle).
- **31/07/2026** — **Interface modernisée** (`src\Vue\Theme.cs`) : dictionnaire de styles WPF appliqué à toute l'application au démarrage — boutons et bascules arrondis avec états survol/actif à l'accent bleu, listes déroulantes re-templétées (popup arrondi ombré, éditables comprises), menus et menus contextuels modernes (surbrillance arrondie, coches et flèches dessinées, ombres portées), zones de texte à focus accentué, cases à cocher pleines, onglets soulignés, barres de défilement fines, infobulles arrondies. Aucun changement architectural : purement des gabarits. Le sélecteur de couleurs Windows (non stylable) est remplacé par un **sélecteur maison** (`SelecteurCouleur.cs`) : carré saturation/luminosité, barre de teinte, champ hexadécimal, aperçu — plus aucune dépendance visuelle au style système. Chrome de fenêtre (barre de titre) volontairement conservé.
- **31/07/2026** — **Étiquettes, « Barre d'outils : » et mode sombre.** Chaque liste déroulante des barres porte désormais une petite étiquette au-dessus d'elle (Police, Taille, Couleur, Fond, Bordure, Cadre, Titre, Flèches…) ; la mention « Barre d'outils : » précède les onglets Texte/Boîte/Groupe/Lien. **Thème sombre** : bouton lune/soleil au bout de la barre des modes, mémorisé dans les paramètres — le dictionnaire de styles est re-parsé avec la palette sombre (`Theme.Basculer`) et le « chrome » (barres, panneaux, libellés, icônes) bascule instantanément grâce à des pinceaux partagés mutables (`Chrome.cs`) ; si le fond du tableau est resté sur la couleur par défaut (le motif ne compte pas), il permute vers un bleu très foncé (`CouleurFondEffective`), motifs et titres de groupes adaptés (trait éclairci sur fond sombre).
- **31/07/2026** — Ajustements : bouton de thème ancré tout à droite de la barre des modes ; **vignette** (très légère ombre dégradée sur les quatre bords du tableau, calque non cliquable), désactivable dans Paramètres > Apparence, activée par défaut ; les raccourcis retirés du pied de l'application (le Tust les porte), la barre d'état ne garde que le zoom.
- **03/08/2026** — **v0.13 : les cinq priorités du comparatif Scapple, livrées.** **Insérer une boîte au milieu d'un lien** (clic droit sur le lien) : l'original devient deux tronçons de même style — l'étiquette reste sur le premier, une flèche simple file vers la cible —, la boîte neuve s'édite aussitôt, Échap annule toute l'insertion (lien restauré). **Empiler la sélection** (menu Édition + clic droit multi-sélection, raccourci re-liable sans défaut) : colonne à centres alignés, espacement 14 px, une annulation. **Plein écran** (F11) et **mode présentation** (F5) : présentation = plein écran + barres masquées + mode lecture, Échap restaure tout (mode d'avant compris). **Impression** (Ctrl+P, PrintDialog WPF, carte ajustée-centrée à la page) et **export PDF** sans dépendance : rendu image 2× envoyé à « Microsoft Print to PDF » avec fichier cible imposé (PrintToFile), paysage automatique, repli sur Imprimer si l'imprimante manque. **OPML** : export 2.0 (arborescence des liens, étiquettes entre parenthèses, ownerName) **et import** (outline → boîtes + flèches, via le constructeur d'arbre commun avec le texte à puces — refactorisé en `ConstruireArbreDepuisNiveaux`, forêt Markdown/OPML partagée dans `Export.ConstruireForet`) — testé en aller-retour. `RendreCarte()` factorisé (PNG/PDF/impression). Réf. System.Xml ajoutée au build. §7 mis à jour : plus un seul « à piquer » face à Scapple.
- **03/08/2026** — **§7 : comparatif Scapple ↔ Mental-o** ajouté au plan (sources : Literature & Latte + revues). Verdict : parité atteinte et dépassée sur le cœur ; à piquer en priorité — insérer une note au milieu d'un lien, export PDF/impression, export (et import !) OPML, « empiler la sélection », plein écran/présentation.
- **03/08/2026** — **v0.12.1 : styles par défaut par fichier.** Fichier > Préférences de fichier gagne trois onglets — **Boîtes** (fond, bordure couleur/épaisseur/trait, coins, ombre, opacité), **Liens** (couleur, épaisseur, trait, flèches, tracé) et **Groupes** (couleur/trait/épaisseur du cadre) — qui définissent le style des **nouveaux** éléments de la carte (les existants ne bougent pas). Enregistrés dans le `.tea` (v13, champs compacts `boiteDefaut`/`lienDefaut`/`groupeDefaut`), appliqués à la création (boîtes, images, liens — un tracé courbe par défaut naît bombé —, groupes, import de texte à puces qui garde ses flèches). Le clic droit « Utiliser ce style pour les nouvelles boîtes » écrit désormais dans ce même défaut de fichier (persisté, visible dans les préférences) au lieu d'un réglage de session. Bouton « Autre… » sur chaque couleur (sélecteur maison, la couleur rejoint le répertoire du fichier).
- **03/08/2026** — **v0.12 : liens évolués, apparence, `.tea` et import/export.** **Liens** : tracé au choix — droit, **courbe** (Bézier quadratique, poignée rose de courbure au milieu du trait, annulable) ou **équerre** (coudes dans l'axe dominant) ; **étiquettes** posées au milieu du trait (fond du tableau derrière, via la barre Lien, le clic droit ou « Retirer l'étiquette »). **Apparence des boîtes** : **ombre** réglable (aucune/légère/marquée) et **opacité** (100 à 30 %, bornée à 15 % au rendu pour rester trouvable) — onglet Boîte, clic droit, copier/coller de style et styles favoris (sérialisation passée de 5 à 7 champs, anciens favoris relus). **`.zeubi` → `.tea`** : nouvelle extension par défaut (ouverture des deux, enregistrement en `.tea`), icône `fichier-tea.ico`, `outils\associer-tea.bat`/`dissocier-tea.bat` (remplacent les scripts zeubi et couvrent les deux extensions), et **Fichier > Associer les fichiers .tea à Mental-o** — la même association HKCU écrite depuis l'appli. **Fichier > Ouvrir les fichiers récents** : les 5 derniers `.tea` ouverts/enregistrés (parametres.json), entrée morte retirée avec message, « Vider la liste ». **Import/export** : Fichier > Exporter — **PNG** (échelle 2×, fond compris, artefacts d'interface masqués, rendu via VisualBrush à transformée neutralisée), **SVG** (dessin vectoriel approché : boîtes, texte par lignes/segments, images en base64, liens aux trois tracés avec flèches et étiquettes, cadres et titres de groupes, opacité), **Markdown** (arborescence suivant les liens — les flèches simples orientent la descente, racines = boîtes sans flèche entrante, étiquettes en italique, section Groupes) ; **Importer un texte à puces** (Ctrl+Entrée pour valider) → arbre de boîtes liées par flèches, indentation → niveaux, une seule annulation. Format `.tea` **v13** (tracé/courbure/étiquette des liens, ombre/opacité des boîtes), testé en aller-retour.
- **31/07/2026** — **v0.11 : la v0.7 « Confort d'édition » est livrée.** **Copier/coller/dupliquer** (Ctrl+C/V/D) : les boîtes se copient avec styles, images et liens internes ; presse-papier interne validé par un marqueur système (copier autre chose ailleurs rend la main au collage d'images), collages décalés en cascade, nouvelle sélection sur les copies, une seule annulation. **Magnétisme + guides** : pendant le glisser, les bords et centres s'aimantent sur ceux des voisines (seuil 7 px), guides roses pointillés à l'écran, Alt pour ignorer, désactivable dans Paramètres > Édition. **Aligner et distribuer** (menu Édition + clic droit multi-sélection) : 6 alignements + distribution H/V à espaces égaux, une seule annulation. **Recherche Ctrl+F** : barre flottante en haut du tableau, résultats en direct, compteur n/N, Entrée/Maj+Entrée pour naviguer, centrage automatique, Échap ferme. **Navigation clavier** : flèches de boîte en boîte (choix directionnel au score), recentrage si hors vue, F2 édite. **Styles favoris** (clic droit > Styles favoris) : enregistrer le style d'une boîte, l'appliquer à toute la sélection, retirer — conservés dans les paramètres de l'application.
