namespace ShopPulse.SharedKernel;

/// <summary>
/// Port partagé entre features : le catalogue déclenche l'ajout au panier sans référencer
/// la feature Cart. Implémenté par CartStore, enregistré dans l'hôte.
/// </summary>
public interface ICartGateway
{
       void Add(int productId, int quantity = 1);

       /// <summary>Lignes courantes (lecture pour le tunnel de commande).</summary>
       IReadOnlyList<CartLine> Lines { get; }

       /// <summary>Vide le panier (après une commande confirmée).</summary>
       void Clear();
}
