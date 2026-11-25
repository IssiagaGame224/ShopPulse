using System.Net;

namespace ShopPulse.Infrastructure;

/// <summary>Traduire les erreurs HTTP en messages utilisables — jamais de trace brute.</summary>
public static class ApiErrors
{
       public static string ToMessage(HttpRequestException ex) => ex.StatusCode switch
       {
              HttpStatusCode.NotFound => "Cet élément n'existe plus.",
              HttpStatusCode.Unauthorized => "Votre session a expiré, reconnectez-vous.",
              HttpStatusCode.TooManyRequests => "Trop de requêtes, patientez un instant.",
              >= HttpStatusCode.InternalServerError => "Le service est momentanément indisponible.",
              _ => "Une erreur est survenue."
       };
}
