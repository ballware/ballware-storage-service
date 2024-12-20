using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Ballware.Storage.Service.Tests.Helper;

public class MockableWebApplicationFactory : WebApplicationFactory<Startup>
{
    private static RandomNumberGenerator RandomNumberGenerator { get; } = RandomNumberGenerator.Create();

    private MockedServicesRepository MockedServices { get; }

    private SecurityKey SecurityKey { get; set; }
    private SigningCredentials SigningCredentials { get; set; }
    private JwtSecurityTokenHandler TokenHandler { get; set; }
    private byte[] Key { get; set; } = new byte[32];

    public string Issuer { get; set; } = "MockedTokenIssuer";
    public string Audience { get; set; } = "MockedTokenAudience";

    public string GenerateToken(IEnumerable<Claim> claims)
    {
        return TokenHandler.WriteToken(new JwtSecurityToken(Issuer, Audience, claims, null, DateTime.UtcNow.AddMinutes(20),
            SigningCredentials));
    }

    public MockableWebApplicationFactory(MockedServicesRepository mockedServicesRepository)
    {
        RandomNumberGenerator.GetBytes(Key);
        SecurityKey = new SymmetricSecurityKey(Key)
        {
            KeyId = Guid.NewGuid().ToString(),
        };
        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256);
        TokenHandler = new JwtSecurityTokenHandler();

        MockedServices = mockedServicesRepository;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");
        base.ConfigureWebHost(builder);

        builder
            .ConfigureServices(services =>
            {
                services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var config = new OpenIdConnectConfiguration()
                    {
                        Issuer = Issuer
                    };

                    config.SigningKeys.Add(SecurityKey);
                    options.Configuration = config;
                    options.Audience = Audience;
                });

                foreach (var (interfaceType, serviceMock) in MockedServices.GetMocks())
                {
                    var registeredService = services.SingleOrDefault(d => d.ServiceType == interfaceType);

                    if (registeredService != null)
                    {
                        services.Remove(registeredService);
                    }

                    services.AddSingleton(interfaceType, serviceMock);
                }
            });
    }
}