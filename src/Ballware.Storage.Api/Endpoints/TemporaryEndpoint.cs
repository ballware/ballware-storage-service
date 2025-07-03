using System.Collections.Immutable;
using System.Security.Claims;
using Ballware.Storage.Authorization;
using Ballware.Storage.Data.Repository;
using Ballware.Storage.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Storage.Api.Endpoints;

public static class TemporaryEndpoint
{
    private const string TemporaryPrimaryQuery = "primary";
    private const string ApiTag = "Temporary";
    private const string ApiOperationPrefix = "Temporary";
    
    public static IEndpointRouteBuilder MapTemporaryUserApi(this IEndpointRouteBuilder app, 
        string basePath,
        string apiTag = ApiTag,
        string apiOperationPrefix = ApiOperationPrefix,
        string authorizationScope = "storageApi",
        string apiGroup = "storage")
    {   
        app.MapGet(basePath + "/downloadbyid/{id}", HandleDownloadByIdAsync)
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "DownloadById")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Download temporary file by ID");
        
        return app;
    }

    public static IEndpointRouteBuilder MapTemporaryServiceApi(this IEndpointRouteBuilder app,
        string basePath,
        string apiTag = ApiTag,
        string apiOperationPrefix = ApiOperationPrefix,
        string authorizationScope = "serviceApi",
        string apiGroup = "storage")
    {   
        app.MapGet(basePath + "/downloadfortenantbyid/{tenantId}/{id}", HandleDownloadForTenantByIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "DownloadForTenantById")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Download temporary for tenant by ID");
        
        app.MapPost(basePath + "/uploadfortenantandidbehalfofuser/{tenantId}/{userId}/{id}", HandleUploadForTenantAndIdBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "UploadForTenantAndIdBehalfOfUser")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Upload temporary for tenant and ID behalf of user");
        
        app.MapDelete(basePath + "/dropfortenantandidbehalfofuser/{tenantId}/{userId}/{id}", HandleDropForTenantAndIdBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropForTenantAndId")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop temporary for tenant and ID");
        
        return app;
    }
    
    private static async Task<IResult> HandleDownloadByIdAsync(IPrincipalUtils principalUtils, ITemporaryStorageProvider storageProvider, ITemporaryRepository repository, ClaimsPrincipal user, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        var temporary = await repository.ByIdAsync(tenantId, TemporaryPrimaryQuery, claims, id);
        
        if (temporary == null || String.IsNullOrEmpty(temporary.StoragePath))
        {
            return Results.NotFound($"Temporary with ID ${id} not existing in tenant {tenantId}");
        }
        
        var fileContent = await storageProvider.DownloadByPathAsync(tenantId, temporary.StoragePath);
        
        if (fileContent == null)
        {
            return Results.NotFound($"File not found for path {temporary.StoragePath}");
        }
        
        return Results.File(fileContent, temporary.ContentType, temporary.FileName);
    }
    
    private static async Task<IResult> HandleDownloadForTenantByIdAsync(ITemporaryStorageProvider storageProvider, ITemporaryRepository repository, Guid tenantId, Guid id)
    {
        var temporary = await repository.ByIdAsync(tenantId, TemporaryPrimaryQuery, ImmutableDictionary<string, object>.Empty, id);
        
        if (temporary == null || String.IsNullOrEmpty(temporary.StoragePath))
        {
            return Results.NotFound($"Temporary with ID ${id} not existing in tenant {tenantId}");
        }
        
        var fileContent = await storageProvider.DownloadByPathAsync(tenantId, temporary.StoragePath);
        
        if (fileContent == null)
        {
            return Results.NotFound($"File not found for path {temporary.StoragePath}");
        }
        
        return Results.File(fileContent, temporary.ContentType, temporary.FileName);
    }
    
    private static async Task<IResult> HandleUploadForTenantAndIdBehalfOfUserAsync(ITemporaryStorageProvider storageProvider, ITemporaryRepository repository, Guid tenantId, Guid userId, Guid id, IFormFileCollection files)
    {
        foreach (var file in files)
        {
            var storagePath = await storageProvider.UploadForIdAsync(tenantId, id, file.FileName, file.ContentType, file.OpenReadStream());
            
            var temporary = await repository.ByIdAsync(tenantId, TemporaryPrimaryQuery, ImmutableDictionary<string,object>.Empty, id);

            if (temporary == null)
            {
                temporary = await repository.NewAsync(tenantId, TemporaryPrimaryQuery, ImmutableDictionary<string,object>.Empty);
            }
            
            temporary.FileName = file.FileName;
            temporary.ContentType = file.ContentType;
            temporary.FileSize = file.Length;
            temporary.StoragePath = storagePath;

            await repository.SaveAsync(tenantId, userId, TemporaryPrimaryQuery, ImmutableDictionary<string,object>.Empty, temporary);
        }

        return Results.Created();
    }
    
    private static async Task<IResult> HandleDropForTenantAndIdBehalfOfUserAsync(ITemporaryStorageProvider storageProvider, ITemporaryRepository repository, Guid tenantId, Guid userId, Guid id)
    {
        var temporary = await repository.ByIdAsync(tenantId, TemporaryPrimaryQuery, ImmutableDictionary<string, object>.Empty, id);
        
        if (temporary == null || string.IsNullOrEmpty(temporary.StoragePath))
        {
            return Results.NotFound($"Temporary with ID {id} not found.");
        }
        
        await storageProvider.DropByPathAsync(tenantId, temporary.StoragePath);
        
        await repository.RemoveAsync(tenantId, userId, ImmutableDictionary<string, object>.Empty, new Dictionary<string, object>()
        {
            { "Id", id }
        });
        
        return Results.Ok();
    }
}