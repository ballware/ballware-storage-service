using Ballware.Storage.Azure;
using Ballware.Storage.Service.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Environment;

builder.Services.Configure<KestrelServerOptions>(builder.Configuration.GetSection("Kestrel"));

builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("appsettings.json", true, true);
builder.Configuration.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", true, true);
builder.Configuration.AddJsonFile($"appsettings.local.json", true, true);
builder.Configuration.AddEnvironmentVariables();

CorsOptions corsOptions = new();
AuthorizationOptions authorizationOptions = new();
StorageOptions storageOptions = new();
SwaggerOptions swaggerOptions = new();

builder.Configuration.GetSection("Cors").Bind(corsOptions);
builder.Configuration.GetSection("Authorization").Bind(authorizationOptions);
builder.Configuration.GetSection("Storage").Bind(storageOptions);
builder.Configuration.GetSection("Swagger").Bind(swaggerOptions);

builder.Services.AddOptionsWithValidateOnStart<AuthorizationOptions>()
    .Bind(builder.Configuration.GetSection("Authorization"))
    .ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<StorageOptions>()
    .Bind(builder.Configuration.GetSection("Storage"))
    .ValidateDataAnnotations();

builder.Services.AddOptionsWithValidateOnStart<SwaggerOptions>()
    .Bind(builder.Configuration.GetSection("Swagger"))
    .ValidateDataAnnotations();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.Authority = authorizationOptions.Authority;
    options.Audience = authorizationOptions.Audience;
    options.RequireHttpsMetadata = authorizationOptions.RequireHttpsMetadata;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
    {
        ValidIssuer = authorizationOptions.Authority
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("storageApi", policy => policy.RequireClaim("scope", authorizationOptions.RequiredScopes.Split(" ")));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(c =>
        {
            c.WithOrigins(corsOptions.AllowedOrigins)
                .WithMethods(corsOptions.AllowedMethods)
                .WithHeaders(corsOptions.AllowedHeaders);
        });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddMvcCore()
    .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null)
    .AddNewtonsoftJson(opts => opts.SerializerSettings.ContractResolver = new DefaultContractResolver())
    .AddApiExplorer();
builder.Services.AddControllers()
    .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null)
    .AddNewtonsoftJson(opts => opts.SerializerSettings.ContractResolver = new DefaultContractResolver());

builder.Services.AddBallwareAzureFileStorageShare(
    storageOptions.ConnectionString, 
    storageOptions.Share);

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("storage", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ballware Storage API",
        Version = "v1"
    });

    c.EnableAnnotations();

    c.AddSecurityDefinition("oidc", new OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.OpenIdConnect,
        OpenIdConnectUrl = new Uri(authorizationOptions.Authority + "/.well-known/openid-configuration")
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oidc" }
            },
            authorizationOptions.RequiredScopes.Split(" ")
        }
    });
});

builder.Services.AddSwaggerGenNewtonsoftSupport();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    IdentityModelEventSource.ShowPII = true;
}

app.UseCors();
app.UseRouting();

app.UseAuthorization();

app.UseStaticFiles();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapSwagger();
});

app.UseSwagger();

if (swaggerOptions.EnableClient)
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("storage/swagger.json", "ballware Storage API");
        c.OAuthClientId(swaggerOptions.ClientId);
        c.OAuthClientSecret(swaggerOptions.ClientSecret);
        c.OAuthScopes(authorizationOptions.RequiredScopes?.Split(" "));
        c.OAuthUsePkce();
    });
}

await app.RunAsync();