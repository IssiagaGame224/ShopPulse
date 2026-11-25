# Plan de manuscrit — « ShopPulse : le Frontend Engineering vu de l'intérieur »

> Document de travail pour le livre. Chaque chapitre s'appuie sur la documentation technique
> correspondante (`docs/00` à `docs/09`) mais la réécrit en récit : un problème, une fausse piste,
> une résolution. Le filon narratif transversal : **les 5 règles d'or du cours, vues de l'intérieur** —
> chaque chapitre en illustre une ou deux par un incident réel, tiré du journal de bord du projet.
> Ton visé : technique mais lisible, à la manière d'un « projet fil rouge commenté ».

## Arguments du livre

1. **On apprend par les incidents.** Chaque chapitre contient au moins un « autopsie d'un bug »
   réel (la cascade d'erreurs Razor, le service worker qui tue les circuits, SignalR 11.0.0,
   le DNS split-horizon de Keycloak…) — matière première que ne possède pas un manuel classique.
2. **Blazor Server comme révélateur.** Beaucoup de concepts du frontend (CSR/SSR, hydration,
   état, cache) deviennent *plus* visibles dans un modèle où le C# reste serveur : les erreurs
   coûtent cher et s'entendent.
3. **La mesure avant l'opinion.** Chaque affirmation du livre a été vérifiée par une requête, un
   test ou un en-tête HTTP — la méthode est elle-même enseignée.

## Public et ton (calibrage pour débutants)

**Public visé : le développeur débutant.** Quelqu'un qui écrit du C# depuis quelques mois,
qui connaît HTML et le concept de « site web », mais qui n'a jamais construit d'application
web complète et n'a jamais entendu parler de circuit, de SSR ou d'injection de dépendances.
**Aucun prérequis en frontend moderne** : chaque terme technique est défini à sa première
apparition, dans une phrase, avec une analogie du quotidien.

Conséquences sur le calibrage (ajusté par rapport au plan initial) :

- **Rythme plus lent, chapitres plus courts** : ~25-30 pages par chapitre, un seul concept
  majeur à la fois ; les 20 concepts d'un module du cours deviennent 8-12 sections, certaines
  regroupées (« Les trois métriques qui comptent » plutôt que LCP, INP, CLS séparément).
- **Moins de densité, plus de répétition utile** : chaque règle d'or est rappelée dans les
  termes du chapitre courant, pas seulement dans son chapitre d'origine.
- **Code commenté ligne par ligne** les dix premières pages, puis de moins en plus
  (on retire les échelles à mesure que le lecteur marche).
- **Exercices avec solutions** : chaque chapitre se termine par 2-3 exercices corrigés en
  annexe (le débutant a besoin de vérifier, pas de chercher trois heures).
- **Le vocabulaire d'abord** : glossaire dès l'introduction (20 termes), enrichi à chaque
  chapitre ; pas de phrase contenant plus d'un terme nouveau.
- **Ton** : deuxième personne du singulier (« tu »), présent de l'indicatif, phrases courtes.
  Autorisé : l'humour, l'auto-dérision (« nous avons planté ce site trois fois pour toi »).
  Interdit : le jargon non défini, l'acronyme sans développé, le « il va de soi ».
- **Les « autopsies » restent**, mais racontées plus lentement : l'erreur d'abord, la panique
  ensuite, la méthode enfin — le débutant doit apprendre à déboguer avant d'apprendre à coder.
- **Fil rouge affectif** : ShopPulse n'est pas un exemple, c'est « ta boutique ». Le lecteur
  la construit morceau par morceau ; chaque chapitre ouvre sur ce qu'on va lui faire gagner
  (une page qui s'affiche vite, un panier qui survit, un site en trois langues…).

## Structure proposée (~350 pages, rythme débutant)

### Partie I — Tes premiers pas (≈ 60 pages, le double du plan initial : le débutant a besoin d'assises)
- **Introduction** — Ce que tu vas construire (des captures de ShopPulse, pas du blabla) ;
  le matériel nécessaire ; **le glossaire des 20 mots à connaître avant de commencer** ;
  les 5 règles d'or annoncées comme une promesse (« à la fin, tu les auras vécues »).
  *Matière : `docs/00` §1-2.*
- **Chapitre 1 — Ta boutique prend vie** — Installation, premier `dotnet run`, ce qui se
  passe quand tu cliques (le circuit raconté avec une analogie), et dès les premières pages
  la page catalogue qui s'affiche **avant** d'être complète (le streaming, comme preuve
  que le rendu est un choix, pas une fatalité). **Règle d'or n° 1 annoncée et vécue.**
  *Matière : `docs/00` + `docs/01`.* **Autopsie** : 26 erreurs de compilation pour une
  ligne oubliée — et la méthode pour ne jamais paniquer devant une cascade.
- **Chapitre 2 — Le squelette et les muscles** — Injection de dépendances expliquée comme
  une prise de courant ; les trois durées de vie avec l'histoire du panier partagé ; le
  pipeline HTTP comme un passage de sécurité. La latence simulée, ou l'art de se donner
  le temps de voir. **Règle d'or n° 5 annoncée** (une seule source de vérité).
  *Matière : `docs/00`.* 

### Partie II — Le rendu, la vitesse, l'état (≈ 110 pages)
- **Chapitre 3 — Où naît le HTML** — CSR/SSR avec des schémas à main levée ; les îlots
  interactifs (« un bouton électrique au milieu d'une page éteinte ») ; `@key` et la
  réconciliation, racontés avec des étiquettes de déménagement. *Matière : `docs/01`.*
- **Chapitre 4 — La vitesse se mesure** — Les trois métriques qui comptent (LCP, INP, CLS)
  avec la règle des 2,5 s / 200 ms / 0,1 ; le debounce comme un appareil photo qui attend
  que tu sois immobile ; la virtualisation ; le cache serveur. **Autopsie** : 1,7 s → 0,1 s.
  *Matière : `docs/02`.*
- **Chapitre 5 — Où vit la donnée** — Le champ local, le service, l'URL : trois étagères
  et une règle pour choisir ; l'immuabilité racontée avec des fiches qui ne se corrigent
  pas mais se remplacent ; la synchronisation entre onglets. **Autopsie** : le Singleton
  qui partageait les paniers de tout le monde. *Matière : `docs/03`.*

### Partie III — Faire grandir sans casser (≈ 90 pages)
- **Chapitre 6 — Des cloisons que le compilateur surveille** — Le monolithe modulaire,
  une RCL par pièce, le design system ; chaque frontière vérifiée par un test.
  *Matière : `docs/04`.* **Autopsie** : le paquet qui épinglait sa version.
- **Chapitre 7 — Ne pas refaire deux fois le même travail** — Les étages du cache, du
  navigateur à la base ; le service worker comme un majordome qui connaît tes habitudes.
  **Règle d'or n° 2.** **Autopsie** (chapitre fort) : *le majordome qui servait des pages
  périmées et tuait les boutons.*
- **Chapitre 8 — Parler au réseau sans en subir** — L'API séparée, le client HTTP typé,
  le retry avec jitter ; chaque appel a un délai, une reprise, une annulation.
  **Autopsie** (chapitre fort) : *l'application morte en silence* — SignalR 11 sur un
  runtime 10, diagnostiquée avec un navigateur automatisé et des logs en mode Debug.

### Partie IV — Se défendre, s'ouvrir (≈ 90 pages)
- **Chapitre 9 — Rien de ce qui vient du navigateur n'est fiable** — XSS, CSRF, CSP ;
  la connexion chez le fournisseur (OIDC raconté comme une badgeuse) ; la session qui
  expire. **Règle d'or n° 3.** *Matière : `docs/07`.* **Autopsies** : AccessDenied malgré
  la connexion ; la vulnérabilité attrapée par la compilation.
- **Chapitre 10 — Tout le monde, partout** — Le clavier d'abord, le lecteur d'écran
  ensuite ; axe comme correcteur orthographique de l'accessibilité ; trois langues dont
  une qui s'écrit à l'envers. **Règle d'or n° 4.** *Matière : `docs/08`.*

### Partie V — Le monde réel (≈ 40 pages)
- **Chapitre 11 — Déployer** (bonus) — Les trois services en conteneurs ; Release vs
  Development mesurée ; le DNS split-horizon raconté comme un immeuble avec deux entrées.
  *Matière : `docs/09`.* 
- **Conclusion — Les cinq règles, une page chacune** — ce que tu as vécu, preuve chiffrée
  à l'appui ; ce que ShopPulse ne couvre pas volontairement ; pour aller plus loin.

## Les fils narratifs transversaux

1. **« La règle d'or sous le microscope »** — encadré en fin de chaque chapitre : la règle
   annoncée au début, vérifiée à la fin, avec la preuve chiffrée du fil rouge (1,7 s → 0,1 s ;
   3/3 tests d'architecture ; 5/5 axe ; 304 mesuré…).
2. **« Le journal de bord »** — encadrés datés reproduisant le vrai diagnostic : message
   d'erreur brut, hypothèses, preuve, correctif. C'est la signature du livre.
3. **« Arbitrage »** — chaque fois que le cours laissait un choix ouvert (Zustand vs Redux,
   `ICartGateway`, composition dans l'hôte, test de parité adapté aux pluriels), un encadré
   « ce qu'on a choisi et pourquoi » — entraîne le jugement du lecteur.
4. **Le vocabulaire des 160 concepts** — glossaire final indexé, chaque concept renvoyant
   au chapitre où il a été *vécu* plutôt qu'à sa définition.

## Calibrage détaillé (par chapitre)

- **25-30 pages** par chapitre ; une « autopsie » fait 5-7 pages racontées pas à pas.
- La structure interne de chaque chapitre, en six temps constants (le débutant aime les
  repères) : **Ce que tu vas gagner** (1 p.) → **Les idées** (8-10 p., analogies d'abord,
  formules ensuite) → **On code** (8-10 p., extrait par extrait, chaque extrait dit d'où
  il vient et où il va) → **Autopsie** (5-7 p.) → **À toi de jouer** (2-3 exercices) →
  **Solutions** (2 p.) → **La règle d'or du chapitre, vérifiée** (1 p.).
- **Une figure dessinée à la main maximum par partie** (le circuit, les étages du cache,
  le DNS split-horizon) : mieux vaut un schéma laid et clair qu'un diagramme parfait
  et intimidant.
- Code du livre = le vrai dépôt, mais **allégé** : on n'imprime pas les usings et les
  imports après le chapitre 2 ; un encadré « où ça vit » indique le fichier exact.
- Chaque chapitre est **autosuffisant** pour la lecture, pas pour la pratique : un lecteur
  qui saute un chapitre comprend, mais son code ne compilera pas — on le dit franchement.
