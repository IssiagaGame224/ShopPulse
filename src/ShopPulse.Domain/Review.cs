namespace ShopPulse.Domain;

/// <summary>Avis client sur un produit. Un seul avis par (produit, utilisateur) :
/// une nouvelle soumission met à jour l'avis existant. AuthorName est le nom
/// d'affichage capturé à la soumission (preferred_username), jamais fourni par le client.</summary>
public record Review(
       int Id,
       int ProductId,
       string AuthorName,
       int Rating,          // 1..5
       string Comment,
       DateTimeOffset CreatedAt);

/// <summary>Agrégat d'avis d'un produit : moyenne (0 s'il n'y en a pas) et nombre.</summary>
public record ProductRating(double Average, int Count);

/// <summary>Réponse de lecture publique : l'agrégat et les avis récents du produit.</summary>
public record ProductReviews(ProductRating Rating, IReadOnlyList<Review> Reviews);

/// <summary>Vue de modération (back-office) : l'avis avec le nom du produit concerné.</summary>
public record AdminReview(
       int Id,
       int ProductId,
       string ProductName,
       string AuthorName,
       int Rating,
       string Comment,
       DateTimeOffset CreatedAt);
