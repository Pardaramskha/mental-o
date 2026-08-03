# Mental-o

Logiciel de carte mentale pour Windows, simple et léger. Inspiré de Scapple, entièrement en français.

## Compiler

```
build.bat
```

Ne demande que le .NET Framework 4.8, inclus dans Windows 10/11 — aucune dépendance à installer. Produit `Mental-o.exe`.

## Utiliser

| Action | Geste |
|---|---|
| Créer une boîte | Double-clic sur le fond |
| Modifier le texte | Double-clic sur une boîte |
| Retour à la ligne / valider / abandonner | Entrée / Maj+Entrée / Échap |
| Sélection multiple | Ctrl+clic, ou rectangle au glisser sur le fond (Ctrl : ajouter) |
| Grouper / dégrouper | Ctrl+G / Ctrl+Maj+G — clic droit sur le cadre : couleur, réajuster, dégrouper |
| Boîte seule dans un groupe | Second clic sur une boîte du groupe sélectionné — elle se déplace et se redimensionne seule |
| Redimensionner un cadre de groupe | Poignée en bas à droite du cadre (jamais plus petit que le contenu) |
| Réajuster au contenu | Clic droit sur une boîte ou un cadre de groupe |
| Changer de mode | Ctrl+1 automatique / Ctrl+2 déplacement / Ctrl+3 liens / Ctrl+4 lecture |
| Lier un groupe | Survoler son cadre : les poignées bleues apparaissent comme sur une boîte |
| Puces / alignement sur sélection | En édition, seuls les lignes/paragraphes sélectionnés sont touchés |
| Barre de personnalisation | Onglets Texte / Boîte / Groupe / Lien dans la barre des modes (bascule automatique) |
| Listes | Boutons • – 1. de l'onglet Texte — toute la boîte, re-cliquer pour retirer |
| Sélectionner un groupe | Clic sur son cadre (surbrillance) — glisser déplace le bloc, Suppr dégroupe |
| Mini-carte / Inspecteur / Historique | Menu Fenêtres — panneaux flottants sur le tableau |
| Titre de groupe | Barre groupe : texte + position (haut/bas, gauche/droite) sur le cadre |
| Préférences du fichier | Fichier > Préférences de fichier — padding des boîtes, texte par défaut |
| Raccourcis personnalisés | Fichier > Paramètres > Raccourcis — re-liables, ★ = favoris du Tust |
| Tust des raccourcis | Fenêtres > Tust des raccourcis — mémo en haut à droite du tableau |
| Mettre en forme | Barre du haut ou clic droit — sur la boîte entière, ou sur la sélection pendant l'édition |
| Police / taille libres | Combos éditables : tapez un nom de police installée ou une taille (6–120) puis Entrée |
| Couleur personnalisée | « Personnalisée… » dans les listes de couleurs — mémorisée dans le fichier courant |
| Styler une boîte | Clic droit : fond, bordure (couleur, épaisseur, trait), coins |
| Réutiliser un style de boîte | Clic droit : copier/coller le style, ou « utiliser ce style pour les nouvelles boîtes » |
| Insérer une image (≤ 5 Mo) | Insertion > Image (Ctrl+Maj+I), glisser-déposer, ou Ctrl+V |
| Image dans une boîte texte | Clic droit > « Insérer une image dans la boîte… », ou déposer le fichier sur la boîte — suit l'alignement |
| Redimensionner une image | Poignée en bas à droite — proportions conservées, Maj pour libérer |
| Redimensionner une boîte texte | Poignée en bas à droite — règle la largeur ; « Largeur automatique » au clic droit pour revenir |
| Gras / Italique / Souligné | Ctrl+B / Ctrl+I / Ctrl+U |
| Déplacer une boîte | Glisser |
| Créer un lien | Survoler une boîte, tirer une des 4 poignées bleues (bords) vers une autre boîte |
| Styler un lien | Clic droit sur le lien : couleur, épaisseur, trait, flèches |
| Supprimer | Sélection (boîte ou lien) puis Suppr |
| Défiler | Molette (Maj+molette : horizontal) |
| Zoomer | Ctrl+molette, Ctrl++ / Ctrl+- / Ctrl+0 |
| Naviguer (pan) | Espace+glisser ou clic molette |
| Annuler / Rétablir | Ctrl+Z / Ctrl+Y |
| Copier / coller / dupliquer | Ctrl+C / Ctrl+V / Ctrl+D — styles, images et liens internes compris |
| Rechercher | Ctrl+F — Entrée/Maj+Entrée pour naviguer, Échap pour fermer |
| Naviguer au clavier | Flèches de boîte en boîte · F2 pour éditer |
| Magnétisme | Automatique au glisser (guides roses) — Alt pour ignorer, réglable dans Paramètres |
| Aligner / distribuer | Menu Édition ou clic droit (sélection multiple) |
| Styles favoris | Clic droit > Styles favoris — enregistrer, appliquer à la sélection, retirer |
| Recentrer la carte | Ctrl+R |

Les cartes sont enregistrées au format `.tea` (archive ZIP contenant un `document.json`).
L'ancienne extension `.zeubi` s'ouvre toujours. Fichier > Exporter propose PNG, PDF, SVG,
Markdown et OPML (l'impression est dans Fichier > Imprimer) ; l'import accepte un texte à
puces indenté ou un plan OPML, transformés en arbre de boîtes liées.

## Outils

- `outils\generer-icones.bat` — régénère `ressources\mentalo.ico` et `ressources\fichier-tea.ico`.
- `outils\associer-tea.bat` — associe `.tea` (et `.zeubi`) à Mental-o (icône + double-clic dans l'Explorateur ; registre utilisateur uniquement). Le menu Fichier > « Associer les fichiers .tea » fait la même chose depuis l'appli.
- `outils\dissocier-tea.bat` — annule ces associations.

Voir `PLAN.md` pour la feuille de route.
