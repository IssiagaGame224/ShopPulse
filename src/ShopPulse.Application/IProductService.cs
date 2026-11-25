using ShopPulse.Domain;

namespace ShopPulse.Application;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);

public interface IProductService
{
       Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);
       Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
       Task<PagedResult<Product>> GetPageAsync(
              int skip, int take, string? search, string? category = null, CancellationToken ct = default);
       Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}
