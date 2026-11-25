using System.ComponentModel.DataAnnotations;

namespace ShopPulse.Domain;

public class ShippingForm
{
       [Required(ErrorMessage = "Le nom est obligatoire.")]
       [StringLength(80)]
       public string FullName { get; set; } = "";

       [Required, EmailAddress]
       public string Email { get; set; } = "";

       [Required, StringLength(200, MinimumLength = 5)]
       public string Address { get; set; } = "";

       [Required, RegularExpression(@"^\d{5}$", ErrorMessage = "Code postal à 5 chiffres.")]
       public string PostalCode { get; set; } = "";
}
