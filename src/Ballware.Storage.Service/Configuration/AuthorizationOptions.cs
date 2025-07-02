using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;

namespace Ballware.Storage.Service.Configuration;

public class AuthorizationOptions
{
    [Required]
    public required string Authority { get; set; }

    [Required]
    public required string Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public string RequiredScopes { get; set; } = "openid storageApi";
    
    [Required]
    public required string TenantClaim { get; set; } = "tenant";

    [Required]
    public required string UserIdClaim { get; set; } = JwtRegisteredClaimNames.Sub;

    [Required]
    public required string RightClaim { get; set; } = "right";
}