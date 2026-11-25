namespace ShopPulse.Domain;

/// <summary>Produit du catalogue. Sku = référence commerciale unique (ex. "SP-0001") ;
/// OldPrice = prix barré quand le produit est en promotion (null sinon) ;
/// Features = caractéristiques techniques affichées en liste (fiche produit) ;
/// AverageRating/ReviewCount = agrégat des avis clients (0 tant qu'il n'y en a pas).</summary>
public record Product(
       int Id,
       string Name,
       string Category,
       decimal Price,
       int Stock,
       string ImageUrl,
       string Description,
       string Sku,
       decimal? OldPrice,
       IReadOnlyList<string> Features,
       double AverageRating = 0,
       int ReviewCount = 0)
{
       /// <summary>Le produit est en promotion si un prix barré supérieur au prix existe.</summary>
       public bool IsOnSale => OldPrice is { } old && old > Price;
}
