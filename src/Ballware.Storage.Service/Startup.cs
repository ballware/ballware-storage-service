using Ballware.Shared.Authorization;
using Ballware.Storage.Api.Endpoints;
using Ballware.Storage.Data.Ef;
using Ballware.Storage.Data.Ef.Configuration;
using Ballware.Storage.Data.Ef.SqlServer;
using Ballware.Storage.Jobs;
using Ballware.Storage.Jobs.Configuration;
using Ballware.Storage.Provider.Azure;
using Ballware.Storage.Provider.Azure.Configuration;
using Ballware.Storage.Service.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using Quartz;

namespace Ballware.Storage.Service;

public class Startup(IWebHostEnvironment environment, ConfigurationManager configuration, IServiceCollection services)
{
    private IWebHostEnvironment Environment { get; } = environment;
    private ConfigurationManager Configuration { get; } = configuration;
    private IServiceCollection Services { get; } = services;

    public void InitializeServices()
    {
        CorsOptions? corsOptions = Configuration.GetSection("Cors").Get<CorsOptions>();
        AuthorizationOptions? authorizationOptions = Configuration.GetSection("Authorization").Get<AuthorizationOptions>();
        MetaStorageOptions? metaStorageOptions = Configuration.GetSection("Meta").Get<MetaStorageOptions>();
        AzureStorageOptions? azureStorageOptions = Configuration.GetSection("AzureStorage").Get<AzureStorageOptions>();
        TriggerOptions? triggerOptions = Configuration.GetSection("Trigger").Get<TriggerOptions>();
        SwaggerOptions? swaggerOptions = Configuration.GetSection("Swagger").Get<SwaggerOptions>();

        var metaStorageConnectionString = Configuration.GetConnectionString("MetaStorageConnection");
        
        Services.AddOptionsWithValidateOnStart<AuthorizationOptions>()
            .Bind(Configuration.GetSection("Authorization"))
            .ValidateDataAnnotations();

        Services.AddOptionsWithValidateOnStart<MetaStorageOptions>()
            .Bind(Configuration.GetSection("Meta"))
            .ValidateDataAnnotations();

        Services.AddOptionsWithValidateOnStart<TriggerOptions>()
            .Bind(Configuration.GetSection("Trigger"))
            .ValidateDataAnnotations();
        
        Services.AddOptionsWithValidateOnStart<SwaggerOptions>()
            .Bind(Configuration.GetSection("Swagger"))
            .ValidateDataAnnotations();

        if (azureStorageOptions != null)
        {
            Services.AddOptionsWithValidateOnStart<AzureStorageOptions>()
                .Bind(Configuration.GetSection("AzureStorage"))
                .ValidateDataAnnotations();
        }
        
        if (authorizationOptions == null || metaStorageOptions == null)
        {
            throw new ConfigurationException("Required configuration for authorization and storage is missing");
        }

        if (triggerOptions == null)
        {
            triggerOptions = new TriggerOptions();
        }

        Services.AddAuthentication(options =>
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

        Services.AddAuthorizationBuilder()
            .AddPolicy("storageApi",
            policy => policy.RequireAssertion(context =>
                context.User
                    .Claims
                    .Where(c => "scope" == c.Type)
                    .SelectMany(c => c.Value.Split(' '))
                    .Any(s => authorizationOptions.RequiredScopes
                        .Split(" ").Contains(s, StringComparer.Ordinal))));

        if (corsOptions != null)
        {
            Services.AddCors(options =>
            {
                options.AddDefaultPolicy(c =>
                {
                    c.WithOrigins(corsOptions.AllowedOrigins)
                        .WithMethods(corsOptions.AllowedMethods)
                        .WithHeaders(corsOptions.AllowedHeaders);
                });
            });
        }

        Services.AddHttpContextAccessor();

        Services.AddMvcCore()
            .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null)
            .AddNewtonsoftJson(opts => opts.SerializerSettings.ContractResolver = new DefaultContractResolver())
            .AddApiExplorer();
        
        Services.Configure<QuartzOptions>(Configuration.GetSection("Quartz"));

        Services.AddAutoMapper(config =>
        {
            config.AddBallwareMetaStorageMappings();
        });

        Services.AddBallwareSharedAuthorizationUtils(authorizationOptions.TenantClaim, authorizationOptions.UserIdClaim, authorizationOptions.RightClaim);

        if (!string.IsNullOrEmpty(metaStorageConnectionString))
        {
            Services.AddBallwareMetaStorageForSqlServer(metaStorageOptions, metaStorageConnectionString);    
        }

        if (azureStorageOptions != null)
        {
            Services.AddBallwareAzureBlobStorage(azureStorageOptions);    
        }

        Services.AddBallwareStorageBackgroundJobs(triggerOptions);
        
        Services.AddEndpointsApiExplorer();

        if (swaggerOptions != null)
        {
            Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("storage", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "ballware Storage User API",
                    Version = "v1"
                });
                
                c.SwaggerDoc("service", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "ballware Storage Service API",
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

            Services.AddSwaggerGenNewtonsoftSupport();
        }
    }

    public void InitializeApp(WebApplication app)
    {
        if (Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            IdentityModelEventSource.ShowPII = true;
        }

        app.UseCors();
        app.UseRouting();

        app.UseAuthorization();
        
        app.MapAttachmentUserApi("attachment");
        app.MapAttachmentServiceApi("attachment");
        app.MapTemporaryUserApi("temporary");
        app.MapTemporaryServiceApi("temporary");

        var authorizationOptions = app.Services.GetService<IOptions<AuthorizationOptions>>()?.Value;
        var swaggerOptions = app.Services.GetService<IOptions<SwaggerOptions>>()?.Value;

        if (swaggerOptions != null && authorizationOptions != null)
        {
            app.MapSwagger();

            app.UseSwagger();

            if (swaggerOptions.EnableClient)
            {
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("storage/swagger.json", "ballware Storage User API");
                    c.SwaggerEndpoint("service/swagger.json", "ballware Storage Service API");
                    c.OAuthClientId(swaggerOptions.ClientId);
                    c.OAuthClientSecret(swaggerOptions.ClientSecret);
                    c.OAuthScopes(authorizationOptions.RequiredScopes?.Split(" "));
                    c.OAuthUsePkce();
                });
            }
        }
    }
}