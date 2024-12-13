using System.ComponentModel.DataAnnotations;

namespace Ballware.Storage.Service.Configuration;

public class AuthorizationOptions
{
    [Required]
    public string Authority { get; set; }
    
    [Required]
    public string Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public string RequiredScopes { get; set; } = "openid storageApi";
}