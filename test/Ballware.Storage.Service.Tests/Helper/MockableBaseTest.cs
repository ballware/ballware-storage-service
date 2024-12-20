using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Ballware.Storage.Service.Tests.Helper;

public class MockableBaseTest
{
    private MockableWebApplicationFactory WebApplicationFactory { get; set; }
    protected MockedServicesRepository? MockedServices { get; set; }

    protected Guid UserSubject { get; set; } = Guid.NewGuid();

    protected Guid Tenant { get; set; } = Guid.NewGuid();

    protected string Scope { get; set; } = "storageApi";

    [SetUp]
    public virtual void SetUp()
    {
        MockedServices = new MockedServicesRepository();
        WebApplicationFactory = new MockableWebApplicationFactory(MockedServices);
    }

    [TearDown]
    public virtual void TearDown()
    {
        WebApplicationFactory.Dispose();
        MockedServices = null;
    }

    public HttpClient GetClient() => WebApplicationFactory.CreateClient();

    public HttpClient GetAuthenticatedClient(IEnumerable<Claim>? additionalClaims = null)
    {
        var client = GetClient();

        var claims = new List<Claim>();

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        if (null == claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, UserSubject.ToString()));
        }

        if (null == claims.FirstOrDefault(c => c.Type == "scope"))
        {
            claims.Add(new Claim("scope", Scope));
        }

        if (null == claims.FirstOrDefault(c => c.Type == "tenant"))
        {
            claims.Add(new Claim("tenant", Tenant.ToString()));
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", WebApplicationFactory.GenerateToken(claims));

        return client;
    }
}