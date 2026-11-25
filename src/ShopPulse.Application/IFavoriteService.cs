namespace ShopPulse.Application;

public interface IFavoriteService
{
       Task SetAsync(int productId, bool isFavorite, CancellationToken ct = default);
}
