# Chapitre 8 — Module 8 : Accessibilité et internationalisation

> **Question du module :** comment faire en sorte que tout le monde puisse utiliser ShopPulse, quelles que soient ses capacités, sa langue et sa culture ?
> **Objectif :** WCAG 2.2 niveau AA, en français, anglais et arabe (écriture de droite à gauche).
> **Fil rouge :** structure sémantique, skip link, région live, `<dialog>` natif, axe en CI, `/fr` `/en` `/ar` avec `dir="rtl"`, pluriels CLDR à six formes.

---

# Partie A — Accessibilité (concepts 1 à 14)

## 1. Semantic HTML

Le HTML sémantique donne gratuitement : rôles, navigation clavier, comportements. Utilisez l'élément qui correspond au **sens**, pas à l'apparence : `<button>` et non `<div class="btn" @onclick>` (qui n'a ni clavier ni rôle). Structure du fil rouge (MainLayout) : `SkipLink`, `header` > `nav aria-label="Principal"`, **`main id="main" tabindex="-1"`**, `footer`. Règles : un seul `<h1>` par page, titres hiérarchiques sans saut, listes pour les listes, `<a>` pour naviguer et `<button>` pour agir, `<PageTitle>` unique par page.

## 2. ARIA Roles

**Première règle : si un élément natif existe, utilisez-le. Une mauvaise ARIA est pire que pas d'ARIA.** ARIA complète quand il n'existe pas d'élément adapté :

| Besoin | Attribut |
|---|---|
| Nom sans texte visible | `aria-label`, `aria-labelledby` |
| Description complémentaire | `aria-describedby` |
| Déplié/replié | `aria-expanded` |
| Élément courant | `aria-current` |
| En chargement | `aria-busy="true"` |
| Champ invalide | `aria-invalid` |
| Urgence / état | `role="alert"` / `role="status"` |

> **Piège (convention du cours)** : jamais d'attribut booléen ARIA nu — `aria-expanded="@_open"` génère un attribut sans valeur ; écrire `aria-expanded="@(_open ? "true" : "false")"`. Un `role` ne change que ce qui est annoncé, pas le comportement : `role="button"` sur un `div` ne donne ni Entrée ni Espace.

## 3. Keyboard Navigation

Tout doit être utilisable sans souris (WCAG 2.1.1). L'ordre de tabulation suit l'ordre visuel (`tabindex > 0` interdit). Le focus doit toujours être visible (WCAG 2.4.7, et 2.4.11 « Focus non masqué » nouveau en 2.2) — le fil rouge pose un `:focus-visible` de 3 px sur le primaire, et ne supprime jamais `outline` sans remplacement. Pour les composants composites (onglets, menus), suivre les motifs de l'**APG** : un seul onglet dans l'ordre de tabulation, flèches pour naviguer, `tabindex="-1"` sur les onglets inactifs. Pas de piège clavier (WCAG 2.1.2).

## 4. Focus Management

Le focus suit le contenu : navigation entre pages → `FocusOnNavigate Selector="h1"` (déjà dans Routes.razor) ; ouverture de dialogue → focus dedans ; fermeture → retour au déclencheur ; suppression d'élément → focus sur le suivant ; erreur de formulaire → focus sur le résumé ou le premier champ (`ElementReference.FocusAsync()`, en interactif ; un conteneur non focalisable reçoit le focus programmatique avec `tabindex="-1"`).

## 5. Screen Readers

Les lecteurs d'écran (NVDA, JAWS, VoiceOver) lisent l'**arbre d'accessibilité**, pas l'écran : nom, rôle, état, valeur. Pour les satisfaire : un nom pour chaque contrôle, des repères (header/nav/main/footer), et du texte visuellement caché mais lu (`.visually-hidden` du fil rouge). `display:none`, `hidden` et `visibility:hidden` retirent l'élément de l'arbre. **Aucun outil automatique ne remplace une écoute réelle** : NVDA + Firefox/Chrome sur Windows.

## 6. Color Contrast

WCAG 2.2 AA : texte normal **4,5:1**, texte large 3:1, composants d'interface et indicateur de focus 3:1 (par rapport à l'état non focalisé). Le fil rouge inclut `Contrast.Ratio` (SharedKernel) et un test : `Primary_on_white_meets_AA` (11,95,255 sur blanc ≥ 4,5). Ne transmettez jamais une informations par la couleur seule (WCAG 1.4.1) ; testez les deux thèmes.

## 7. Alt Text

| Image | alt |
|---|---|
| Informative (produit) | décrit ce qui compte |
| Décorative | `alt=""` (vide, pas absent) |
| Fonctionnelle (lien/bouton) | décrit l'**action** |
| Complexe (graphique) | court alt + description à côté |

Pas de « image de… » (le lecteur l'annonce déjà) ; un alt absent fait lire le nom du fichier. Le fil rouge : `alt="@Product.Name"` partout, `aria-hidden="true"` sur les icônes.

## 8. Forms and Labels

Chaque champ a une étiquette visible associée programmatiquement (`<label for>`) et ses erreurs sont liées (`aria-describedby`, `aria-invalid`, `role="alert"`). `placeholder` n'est pas une étiquette. `autocomplete` sur les champs d'identité (WCAG 1.3.5) — le `ShippingFormView` du module 3 le fait déjà. Champs obligatoires indiqués en texte ; aide à la correction précise (WCAG 3.3.3) ; ne pas redemander une donnée saisie (WCAG 3.3.7, nouveau en 2.2) ; pas de test cognitif bloquant à la connexion (WCAG 3.3.8) ; résumé des erreurs en haut avec liens vers les champs.

## 9. Skip Links

Le lien d'évitement permet de sauter la navigation (WCAG 2.4.1) : premier élément focalisable, visible au focus, cible `main#main`. Le fil rouge (`SkipLink.razor`) :

> **Piège Blazor vérifiable** : avec `<base href>` incluant le préfixe de langue, un lien vers `#main` résout vers la page d'accueil. Deux précautions : `data-enhance-nav="false"` (pas de navigation améliorée) et un href pointant vers la page courante + `#main`.

## 10. Reduced Motion

Respecter `prefers-reduced-motion: reduce` — le fil rouge ajoute le bloc média qui écrase animations et transitions (le shimmer du squelette devient statique). Écrire d'abord sobre, ajouter le mouvement sous `no-preference`. Rien ne doit clignoter plus de 3 fois par seconde (WCAG 2.3.1) ; carrousels automatiques à éviter.

## 11. Live Regions

Les régions live font annoncer des changements sans prise de focus : `aria-live="polite"`/`role="status"` (attend la fin de la phrase) vs `aria-live="assertive"`/`role="alert"` (interrompt). **Règle capitale : la région doit exister dans le DOM, vide, avant que son contenu ne change** — d'où `LiveRegion.razor` posée une fois dans le layout, alimentée par le `LiveAnnouncer` (Scoped, port `ILiveAnnouncer` dans Application pour que l'UI reste indépendante). Le `AddToCartButton` annonce « Produit ajouté au panier. ». N'annoncez ni chaque frappe ni chaque mise à jour de stock : le bruit est une nuisance.

## 12. Accessible Modals

L'élément natif **`<dialog>` avec `showModal()`** gère le piège de focus, Échap, l'inertie de l'arrière-plan et le retour du focus. Le fil rouge : `Modal.razor` (UI) + `a11y.js` (ouverture/fermeture/événement `close`), `aria-labelledby` vers son titre, `OnAfterRenderAsync` pour l'interop. Pour une confirmation destructive, focaliser « Annuler ». Évitez les modales quand un panneau en ligne suffit.

## 13. Testing with axe

axe-core détecte 30-40 % des problèmes (contraste, noms manquants, rôles invalides). Le fil rouge ajoute `tests/ShopPulse.A11y` (xunit + `Microsoft.Playwright` 1.65 + `Deque.AxeCore.Playwright` 4.14) : la théorie `Page_has_no_wcag_violations` parcourt 5 pages dont `/ar/catalog` (le RTL est testé aussi), tags `wcag2a` → `wcag22aa`. Nécessite les serveurs de dev et les navigateurs Playwright (`playwright.ps1 install chromium`). Stratégie complète : Roslyn/éditeur à l'écriture, axe + Lighthouse en CI, parcours clavier manuel, NVDA, tests avec des personnes concernées.

## 14. WCAG Levels

4 principes (POUR : Perceptible, Utilisable, Compréhensible, Robuste), 3 niveaux : A (minimum), **AA (cible légale usuelle)**, AAA (rarement atteignable en entier). Nouveautés 2.2 : 2.4.11 focus non masqué, 2.5.7 alternative au glisser-déposer, 2.5.8 cibles ≥ 24×24 px, 3.2.6 aide cohérente, 3.3.7 saisie redondante, 3.3.8 authentification accessible. Cadres légaux (EAA juin 2025, RGAA, ADA, EN 301 549) s'appuient sur AA ; documentez une déclaration d'accessibilité publique.

---

# Partie B — Internationalisation (concepts 15 à 20)

## 15. Internationalization

L'i18n prépare le code pour plusieurs langues ; la l10n fournit chaque langue. En Blazor Server : `IStringLocalizer<T>` + fichiers `.resx`. Le fil rouge : `AddLocalization()` ; `SharedResource` + `SharedResource.{fr,en,ar}.resx` dans le projet **UI** (référencé par Web et les features) ; clés sémantiques (`AddToCart`, `Cart.Items.one`…) ; jamais de concaténation de textes traduits — des paramètres (`L["Welcome", name]`) ; une clé manquante renvoie la clé elle-même (`ResourceNotFound` pour la repérer). **Mode interactif : la culture est lue au démarrage du circuit** — d'où la culture dans l'URL (concept 19).

## 16. RTL Support

L'arabe s'écrit de droite à gauche : il faut inverser la **mise en page**, pas seulement le texte. `App.razor` : `<html lang="@lang" dir="@(IsRightToLeft ? "rtl" : "ltr")">`. CSS : **propriétés logiques** (`margin-inline-start`, `padding-inline-end`, `text-align: start`…) — Flexbox et Grid suivent déjà `dir`. Les icônes directionnelles s'inversent (`[dir="rtl"] .icon-chevron { transform: scaleX(-1); }`), pas les logos ni les chiffres. Isoler les fragments latins en arabe avec `<bdi>`/dir="auto". **Vérifié : `/ar/cart` sert `lang="ar" dir="rtl"`.**

## 17. Date and Number Formats

Ne formatez jamais à la main : utilisez la culture (`price.ToString("C", culture)`). **Piège** : la devise n'est pas la culture — conservez la devise du produit, formatez avec la culture de l'utilisateur. Dates stockées en UTC, affichées dans le fuseau de l'utilisateur (via interop JS `Intl`). Mode invariant pour tout ce qui est sérialisé. Sous Linux/conteneurs : installer ICU sinon `InvariantGlobalization` coupe les cultures.

## 18. Pluralization

Les règles de pluriel diffèrent : anglais 2 formes, français 2 (0 et 1 au singulier), **arabe 6** (0, 1, 2, 3-10, 11-99, 100+). Le fil rouge : `PluralRules.For(lang, n)` (UI/Resources) — testée sur les 6 catégories ar (0, 1, 2, 5, 11, 100) — et `L.Plural("Cart.Items", n)` qui choisit `Cart.Items.{cat}`. Alternative robuste : MessageFormat ICU (base CLDR) avec MessageFormat.NET.

## 19. Locale Routing

La langue dans l'URL est partageable, indexable, indépendante de la session. Le fil rouge (`LocalizationSetup.cs`) : préfixe via **PathBase** — le middleware retire `/fr|en|ar` du path, pose la culture, et la racine redirige selon `Accept-Language` ; les routes des pages n'ont pas à changer.

> **Deux pièges vécus** : (1) dans l'hôte minimal, le routage est inséré **avant** le middleware personnalisé — il faut un `app.UseRouting()` explicite après la réécriture ; (2) `UseRequestLocalization` **réinitialise** la culture depuis ses providers (cookie/query/Accept-Language) : le middleware pose `CurrentCulture` directement. Ne jamais rediriger `/api` ni `/_blazor`. `<base href="@Nav.BaseUri">` conserve le préfixe pour les liens relatifs. hreflang pour le SEO ; sélecteur accessible : chaque langue écrite dans sa propre langue avec `lang` et `hreflang`, `aria-current` sur la langue active, **pas de drapeaux** (WCAG 3.1.2). La politique OutputCache varie déjà par langue (`VaryByHeader Accept-Language`, module 5).

## 20. Translation Workflow

Traduire est un processus : une langue source (fr), clés sémantiques, contexte pour les traducteurs (commentaires, captures, limites), **vérification en CI** — le fil rouge implémente `Traductions_parfaites_entre_langues` : clés non plurielles identiques partout + **chaque langue possède les formes de pluriel que ses règles produisent** (adaptation du test du cours : l'arabe a légitimement six formes, le français deux — comparer des ensembles bruts serait faux). Plus : pseudo-localisation (textes allongés), relecteur natif (surtout pour l'arabe : ton, forme, direction), plateformes (Crowdin, Lokalise, Weblate), ne jamais traduire identifiants/URL/marques.

---

## Récapitulatif du fil rouge après le module 8

| Domaine | Mise en œuvre |
|---|---|
| Structure | header, nav aria-label, main#main tabindex=-1, footer, un h1 par page |
| Clavier | :focus-visible 3px, FocusOnNavigate, skip link data-enhance-nav="false" |
| Lecteur d'écran | noms, repères, LiveAnnouncer + LiveRegion dans le layout |
| Visuel | contraste testé (≥ 4,5:1), pas de couleur seule, prefers-reduced-motion |
| Modale | `<dialog>` natif + a11y.js (exercice d'usage) |
| Tests | axe (Playwright+Deque) en CI, pluriels + parité resx + contraste en xUnit |
| Langues | fr/en/ar via IStringLocalizer + .resx dans UI |
| RTL | dir dynamique, propriétés logiques, icônes à inverser |
| URL | /fr /en /ar par PathBase, racine selon Accept-Language, hreflang |
| Traduction | parité + complétude des pluriels vérifiées en CI (14/14 tests) |

## Exercices du module

1. Parcours complet au clavier : pièges et focus invisibles (le focus visible est en place).
2. ✅ prêt : `dotnet test tests/ShopPulse.A11y` (serveurs de dev requis) — corriger les violations relevées.
3. NVDA sur la page produit : noter ce qui est annoncé.
4. ✅ fait en pair programming : l'arabe est servi, `dir="rtl"`, pluriels testés (0, 1, 2, 5, 11, 100).
5. ✅ fait : le test de parité/complétude des traductions est écrit et vert.
6. ✅ fait : contraste du primaire mesuré ≥ 4,5:1 (test `Primary_on_white_meets_AA`).
7. `prefers-reduced-motion` dans les DevTools : vérifier que le shimmer disparaît.
