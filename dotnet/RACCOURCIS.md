# Raccourcis : Mental-O seul et Mental-O dans Marabook

Tableau comparatif (26/10/2026) pour décider ce qui change quand la toile
vit dans Marabook. Les gestes de Mental-O sont **re-liables** dans Fichier >
Paramètres (seul) et dans Préférences > Mental-O (dlc) (Marabook) : ce
tableau donne les défauts.

Dans Marabook, les raccourcis de la toile ne sont actifs que **quand la
toile a le clavier** (une carte ouverte, cliquée). Les raccourcis globaux
de Marabook (Ctrl+S, F1/F2 des fiches, Ctrl+1…4 des vues…) restent ceux
de Marabook : la toile ne les intercepte que si le même geste lui est
lié, et elle a alors la priorité tant qu'elle a le focus.

Légende : ✔ identique · ✖ absent · ⚠ conflit possible avec Marabook (à
trancher) · — sans raccourci.

| Action | Mental-O seul | Dans Marabook (DLC) | Note |
|---|---|---|---|
| Nouveau / Ouvrir / Enregistrer / Enregistrer sous | Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+Maj+S | ✖ (à Marabook) | La carte est enregistrée avec le .plot (Ctrl+S de Marabook). |
| Imprimer | Ctrl+P | ✖ | Export PDF dans le menu Exporter de la barre. |
| Annuler / Rétablir | Ctrl+Z / Ctrl+Y | ✔ (toile au clavier) | ⚠ Marabook a son propre Ctrl+Z : la toile le prend quand elle a le focus. |
| Copier les boîtes / Coller / Dupliquer | Ctrl+C / Ctrl+V / Ctrl+D | ✔ | ⚠ Ctrl+D : vérifier qu'il n'est pas pris par Marabook sur la vue carte. |
| Rechercher dans la carte | Ctrl+F | ✔ | ⚠ Ctrl+F = recherche de Marabook ; la toile gagne tant qu'elle a le focus. |
| Modifier la boîte sélectionnée | F2 | ✔ | ⚠ F2 = Généalogie sur les fiches (1.1.0) ; pas de conflit sur une carte, mais deux sens pour la même touche. |
| Supprimer la sélection | Suppr | ✔ | |
| Grouper / Dégrouper | Ctrl+G / Ctrl+Maj+G | ✔ | |
| Empiler la sélection | — | — | Menu Aligner. |
| Insérer une image / un fichier / un lien web | Ctrl+Maj+I / Ctrl+Maj+F / Ctrl+Maj+L | ✔ | |
| Tout sélectionner | Ctrl+A | ✔ | |
| Note de la boîte | Ctrl+Maj+N | ✔ | |
| Plier / déplier le groupe | Ctrl+Maj+P | ✔ | |
| Tout voir / Zoom sur la sélection | Ctrl+Maj+0 / Ctrl+Maj+1 | ✔ | |
| Mettre en avant la sélection | Ctrl+Maj+M | ✔ | |
| Zoom avant / arrière / 100 % | Ctrl++ / Ctrl+− / Ctrl+0 (pavé ou rangée du haut) | ✔ | ⚠ Marabook zoome-t-il ses pages avec Ctrl+molette ou Ctrl++ ? À trancher. |
| Recentrer la carte | Ctrl+R | ✔ | |
| Plein écran | F11 | ✖ (à Marabook) | |
| Mode présentation | F5 | ✔ (masque la barre de la toile, pas Marabook) | Échap quitte. |
| Modes automatique / déplacement / liens / lecture | Ctrl+1 / Ctrl+2 / Ctrl+3 / Ctrl+4 | ✔ | ⚠ Ctrl+1…4 changent de vue dans Marabook (Pages/Brouillon/Calme…) : conflit direct — proposition : Alt+1…4 dans le DLC, ou laisser Marabook gagner et passer par les boutons de mode. |
| Gras / Italique / Souligné | Ctrl+B / Ctrl+I / Ctrl+U | ✔ | Mêmes gestes que l'éditeur de Marabook : cohérent. |
| Boîte sœur / boîte enfant | Entrée / Tab | ✔ | Sur la toile seulement, hors édition. |
| Navigation entre boîtes | ← → ↑ ↓ | ✔ | |
| Désélectionner, quitter le focus/la présentation | Échap | ✔ | |
| Naviguer (pan) | Espace + glisser, bouton du milieu | ✔ | |
| Zoom à la molette | Ctrl + molette | ✔ | |
| Créer une boîte | Double-clic sur le fond | ✔ | + bouton « Nouvelle boîte » de la barre. |
| Présentation : suivante / précédente / début / laser | → Espace / ← / Début / L | → Espace / ← / Début (pas de laser) | Le pointeur laser reste propre à Mental-O seul (fenêtre plein écran). |

## Ce qui n'existe que dans Mental-O seul

- Fichiers : nouveau depuis un modèle, fichiers récents, association .tea,
  copies de secours chaque minute, sauvegarde automatique (dans Marabook,
  c'est le .plot et son secours qui portent tout cela).
- Fenêtre : plein écran, pointeur laser, bascule du thème (Marabook impose
  le sien), menu principal.
- Mise à jour : Aide > Vérifier les mises à jour (dans Marabook, le DLC est
  vérifié en même temps que Marabook, toast « Une nouvelle version de
  Mental-O est disponible »).

## Ce qui n'est pas porté (par rapport à l'ancien Mental-o WPF)

- Le correcteur orthographique de Windows pendant la saisie (WPF seulement).
  Le dictionnaire personnel est conservé pour une suite (Grammalecte côté
  Marabook ?).
- L'impression directe (Avalonia n'a pas de dialogue d'impression) : Fichier
  > Imprimer produit un PDF temporaire ouvert dans le lecteur du système.
- Pendant l'édition d'une boîte, le texte s'affiche à plat (un TextBox,
  Avalonia n'a pas de RichTextBox) ; les styles (gras, couleurs, tailles par
  segment) sont conservés et s'appliquent à la sélection ou au mot sous le
  curseur, puis réapparaissent à la validation.
