using ShopPulse.Domain;

namespace ShopPulse.Application;

/// <summary>Port des avis clients côté Web : lecture publique, soumission (le jeton
/// de l'utilisateur voyage via le client HTTP « api »), modération back-office.</summary>
public interface IReviewService
{
       /// <summary>Agrégat + avis récents ; null si le produit n'existe pas.</summary>
       Task<ProductReviews?> GetForProductAsync(int productId, CancellationToken ct = default);

       Task<ReviewSubmitResult> SubmitAsync(int productId, int rating, string comment, CancellationToken ct = default);

       Task<IReadOnlyList<AdminReview>> GetForModerationAsync(CancellationToken ct = default);

       Task<bool> DeleteAsync(int reviewId, CancellationToken ct = default);
}

/// <summary>Résultat d'une soumission d'avis — l'appelant distingue sans interpréter
/// de codes HTTP (même esprit que OrderPlacement).</summary>
public abstract record ReviewSubmitResult
{
       public sealed record Ok(Review Review) : ReviewSubmitResult;
       public sealed record Unauthorized : ReviewSubmitResult;
       public sealed record Invalid(string Message) : ReviewSubmitResult;
       public sealed record Failed(string Message) : ReviewSubmitResult;
}
