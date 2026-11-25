using ShopPulse.Domain;

namespace ShopPulse.Infrastructure.Persistence;

/// <summary>Source unique du catalogue de départ : partagée par l'implémentation
/// en mémoire (dev/tests) et par le seeding EF Core. Les 10 modèles réels
/// (une photo, un nom, une description commerciale par image wwwroot/img/p*.jpg)
/// sont déclinés en 200 produits pour alimenter la pagination et la recherche.</summary>
public static class CatalogSeed
{
       /// <summary>Catégories du catalogue, ids stables (1..n) : exigés par HasData
       /// et par le backfill de la migration AddCategories.</summary>
       public static readonly (int Id, string Name)[] Catégories =
       [
              (1, "Audio"),
              (2, "Clavier"),
              (3, "Écran"),
       ];

       public static int CategoryIdOf(string name) =>
              Catégories.First(c => c.Name == name).Id;

       public static readonly (string Catégorie, string Nom, string Description, string Image)[] Modèles =
       [
              ("Clavier", "Clavier Mécanique Sans Fil Pro",
                     "Clavier mécanique sans fil au châssis robuste en aluminium brossé et finition noir mat. Équipé de commutateurs tactiles silencieux, d'une connectivité Bluetooth multi-appareils et d'une autonomie longue durée pour une frappe fluide au bureau comme en déplacement.",
                     "/img/p0.jpg"),
              ("Clavier", "Clavier Rétroéclairé Ultra-Fin",
                     "Clavier bureautique ergonomique ultra-plat doté d'un rétroéclairage LED blanc réglable. Ses touches chiclet à faible course offrent un silence absolu et un confort de frappe optimal pour vos sessions de travail prolongées.",
                     "/img/p1.jpg"),
              ("Écran", "Écran Incurvé Ultrawide 34\"",
                     "Moniteur panoramique incurvé (courbure 1500R) au format 21:9 avec résolution WQHD. Conçu pour le multitâche intensif et l'immersion totale, il intègre des technologies anti-scintillement et de réduction de lumière bleue.",
                     "/img/p2.jpg"),
              ("Audio", "Casque Audio Sans Fil Studio Hi-Fi",
                     "Casque circum-aural premium combinant réduction active du bruit (ANC) et transducteurs haute résolution. Profitez de coussinets à mémoire de forme ultra-confortables, d'un son riche et équilibré ainsi que de plus de 30 heures d'écoute continue.",
                     "/img/p3.jpg"),
              ("Clavier", "Clavier Mécanique Compact TKL",
                     "Clavier mécanique compact au format Tenkeyless (sans pavé numérique) pour libérer de l'espace sur votre bureau. Commutateurs linéaires ultra-réactifs et câble USB-C tressé amovible, idéal pour les setups épurés et nomades.",
                     "/img/p4.jpg"),
              ("Écran", "Écran Gaming & Multimédia 27\" 4K",
                     "Écran 27 pouces IPS 4K UHD avec bordures ultra-fines sur 3 côtés et taux de rafraîchissement élevé. Il offre un temps de réponse instantané et une compatibilité HDR éclatante pour le jeu et les médias haute fidélité.",
                     "/img/p5.jpg"),
              ("Audio", "Enceintes Stéréo de Bureau Hi-Res",
                     "Paire d'enceintes acoustiques compactes délivrant des basses profondes et des aigus cristallins. Connectivité polyvalente (Bluetooth 5.0, entrée optique et AUX) pour sublimer le son de votre poste de travail.",
                     "/img/p6.jpg"),
              ("Clavier", "Clavier Ergonomique Confort Avancé",
                     "Clavier ergonomique pensé pour soulager la tension des poignets et des avant-bras grâce à son repose-poignet rembourré intégré et sa disposition incurvée naturelle des touches.",
                     "/img/p7.jpg"),
              ("Écran", "Moniteur Professionnel 32\" Graphique & Création",
                     "Moniteur haute précision 32 pouces calibré en usine avec une couverture de 99 % de l'espace sRGB / DCI-P3. Pied ergonomique entièrement ajustable (hauteur, inclinaison, pivot) et hub USB-C avec alimentation intégrée.",
                     "/img/p8.jpg"),
              ("Audio", "Écouteurs Sans Fil True Wireless",
                     "Écouteurs intra-auriculaires compacts avec isolation phonique passive et micro haute clarté pour vos réunions et appels. Boîtier de recharge compact compatible charge sans fil pour une journée complète de musique.",
                     "/img/p9.jpg"),
       ];

       /// <summary>Génération déterministe : le seed EF (HasData) exige des valeurs stables
       /// d'une migration à l'autre.</summary>
       public static IReadOnlyList<Product> BuildProducts() =>
              Enumerable.Range(1, 200)
                     .Select(i =>
                     {
                            var m = Modèles[i % 10];
                            var price = 10 + i * 1.5m;
                            return new Product(
                                   i, $"{m.Nom} — édition {i}", m.Catégorie,
                                   price, i % 7 == 0 ? 0 : 50 - i % 40,
                                   m.Image, m.Description,
                                   Sku: $"SP-{i:D4}",
                                   // Un produit sur cinq est en promotion (prix barré).
                                   OldPrice: i % 5 == 0 ? Math.Round(price * 1.25m, 2) : null,
                                   Features: FeaturesFor(m.Catégorie));
                     })
                     .ToList();

       /// <summary>Caractéristiques techniques par catégorie (style liste à puces e-commerce).</summary>
       private static string[] FeaturesFor(string category) => category switch
       {
              "Clavier" =>
              [
                     "Connectivité Bluetooth 5.0 multi-appareils et USB-C",
                     "Autonomie longue durée (jusqu'à 6 mois)",
                     "Touches silencieuses à faible course",
                     "Garantie 2 ans",
              ],
              "Écran" =>
              [
                     "Dalle IPS, angles de vision 178°/178°",
                     "Luminosité 250 cd/m², contraste 3000:1",
                     "Technologies anti-scintillement et réduction de lumière bleue",
                     "Garantie 2 ans",
              ],
              _ =>   // Audio
              [
                     "Connectivité Bluetooth 5.0, entrée AUX",
                     "Autonomie jusqu'à 30 heures d'écoute",
                     "Coussinets à mémoire de forme ultra-confortables",
                     "Garantie 2 ans",
              ],
       };
}
