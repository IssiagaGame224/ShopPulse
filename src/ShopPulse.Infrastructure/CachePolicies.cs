namespace ShopPulse.Infrastructure;

/// <summary>
/// Une clé de cache doit identifier exactement ce qui détermine la réponse :
/// variantes (langue, devise, catégorie, page) + version de schéma (v2) pour invalider
/// tout en cas de changement de format. Jamais d'identité utilisateur dans un cache partagé
/// sans que ce soit voulu.
/// </summary>
public static class CacheKeys
{
       public static string Products(string? category, int page, string culture)
              => $"products:v2:{category ?? "all"}:p{page}:{culture}";

       public static string Product(int id, string culture) => $"product:v2:{id}:{culture}";
}
