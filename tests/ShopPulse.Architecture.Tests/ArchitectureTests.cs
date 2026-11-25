using NetArchTest.Rules;
using Xunit;

namespace ShopPulse.Architecture.Tests;

/// <summary>
/// Les frontières ne tiennent que si elles sont vérifiées automatiquement.
/// Exercice du cours : cassez volontairement une règle (ex. référence ASP.NET dans Domain)
/// pour voir le test rouge.
/// </summary>
public class ArchitectureTests
{
       [Fact]
       public void Domain_ne_depend_de_rien()
       {
              var result = Types.InAssembly(typeof(ShopPulse.Domain.Product).Assembly)
                     .ShouldNot().HaveDependencyOnAny(
                            "ShopPulse.Application", "ShopPulse.Infrastructure",
                            "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
                     .GetResult();

              Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
       }

       [Fact]
       public void Features_ne_se_connaissent_pas()
       {
              var result = Types.InAssembly(typeof(ShopPulse.Features.Cart.CartModule).Assembly)
                     .ShouldNot().HaveDependencyOn("ShopPulse.Features.Catalog")
                     .GetResult();

              Assert.True(result.IsSuccessful);
       }

       [Fact]
       public void Composants_UI_nutilisent_pas_dacces_aux_donnees()
       {
              var result = Types.InAssembly(typeof(ShopPulse.UI.Marker).Assembly)
                     .ShouldNot().HaveDependencyOn("ShopPulse.Infrastructure")
                     .GetResult();

              Assert.True(result.IsSuccessful);
       }
}
