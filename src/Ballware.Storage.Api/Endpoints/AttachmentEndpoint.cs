using System.Collections.Immutable;
using System.Security.Claims;
using Ballware.Shared.Authorization;
using Ballware.Storage.Data.Public;
using Ballware.Storage.Data.Repository;
using Ballware.Storage.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Storage.Api.Endpoints;

public static class AttachmentEndpoint
{
    private const string AttachmentPrimaryQuery = "primary";
    private const string ApiTag = "Attachment";
    private const string ApiOperationPrefix = "Attachment";
    
    public static IEndpointRouteBuilder MapAttachmentUserApi(this IEndpointRouteBuilder app, 
        string basePath,
        string apiTag = ApiTag,
        string apiOperationPrefix = ApiOperationPrefix,
        string authorizationScope = "storageApi",
        string apiGroup = "storage")
    {
        app.MapGet(basePath + "/allforentityandowner/{entity}/{ownerId}", HandleAllForEntityAndOwnerAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<Attachment>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "AllForEntityAndOwner")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query attachments for entity and owner");
        
        app.MapGet(basePath + "/downloadforentityandownerbyid/{entity}/{ownerId}/{id}", HandleDownloadForEntityAndOwnerByIdAsync)
            .Produces<FileStream>(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "DownloadForEntityAndOwnerById")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Download attachment for entity and owner by ID");
        
        app.MapPost(basePath + "/uploadforentityandowner/{entity}/{ownerId}", HandleUploadForEntityAndOwnerAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "UploadForEntityAndOwner")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Upload attachment for entity and owner");
        
        app.MapDelete(basePath + "/dropforentityandownerbyid/{entity}/{ownerId}/{id}", HandleDropForEntityAndOwnerByIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropForEntityAndOwnerById")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop attachment for entity and owner by ID");
        
        app.MapDelete(basePath + "/dropallforentityandowner/{entity}/{ownerId}", HandleDropAllForEntityAndOwnerAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropAllForEntityAndOwner")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop all attachments for entity and owner");
        
        return app;
    }

    public static IEndpointRouteBuilder MapAttachmentServiceApi(this IEndpointRouteBuilder app,
        string basePath,
        string apiTag = ApiTag,
        string apiOperationPrefix = ApiOperationPrefix,
        string authorizationScope = "serviceApi",
        string apiGroup = "service")
    {   
        app.MapGet(basePath + "/allfortenantentityandowner/{tenantId}/{entity}/{ownerId}", HandleAllForTenantEntityAndOwnerAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<Attachment>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "AllForTenantEntityAndOwner")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query attachments for entity and owner");
        
        app.MapGet(basePath + "/downloadfortenantentityandownerbyid/{tenantId}/{entity}/{ownerId}/{id}", HandleDownloadForTenantEntityAndOwnerByIdAsync)
            .Produces<FileStream>(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "DownloadForTenantEntityAndOwnerById")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Download attachment for entity and owner by ID");
        
        app.MapGet(basePath + "/downloadfortenantentityandownerbyfilename/{tenantId}/{entity}/{ownerId}/{filename}", HandleDownloadForTenantEntityAndOwnerByFilenameAsync)
            .Produces<FileStream>(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "DownloadForTenantEntityAndOwnerByFilename")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Download attachment for entity and owner by filename");
        
        app.MapPost(basePath + "/uploadfortenantentityandownerbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}", HandleUploadForTenantEntityAndOwnerBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "UploadForTenantEntityAndOwnerBehalfOfUser")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Upload attachment for tenant, entity and owner");
        
        app.MapDelete(basePath + "/dropfortenantentityandownerbyidbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}/{id}", HandleDropForTenantEntityAndOwnerByIdBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropForTenantEntityAndOwnerByIdBehalfOfUser")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop attachment for tenant, entity and owner by ID");
        
        app.MapDelete(basePath + "/dropfortenantentityandownerbyfilenamebehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}/{filename}", HandleDropForTenantEntityAndOwnerByFilenameBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropForTenantEntityAndOwnerByFilenameBehalfOfUser")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop attachment for tenant, entity and owner by filename");
        
        app.MapDelete(basePath + "/dropallfortenantentityandownerbehalfofuser/{tenantId}/{userId}/{entity}/{ownerId}", HandleDropAllForTenantEntityAndOwnerBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropAllForTenantEntityAndOwnerBehalfOfUser")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop all attachments for tenant, entity and owner");
        
        app.MapDelete(basePath + "/dropallfortenantandentitybehalfofuser/{tenantId}/{userId}/{entity}", HandleDropAllForTenantAndEntityBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropAllForTenantAndEntityBehalfOfUser")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop all attachments for tenant, entity and owner");
        
        app.MapDelete(basePath + "/dropallfortenantbehalfofuser/{tenantId}/{userId}", HandleDropAllForTenantBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces( StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropAllForTenantBehalfOfUser")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop all attachments for tenant");
        
        return app;
    }
    
    private static async Task<IResult> HandleAllForEntityAndOwnerAsync(IPrincipalUtils principalUtils, IAttachmentRepository repository, ClaimsPrincipal user, string entity, Guid ownerId)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        
        return Results.Ok(await repository.AllByEntityAndOwnerIdAsync(tenantId, entity, ownerId));
    }
    
    private static async Task<IResult> HandleUploadForEntityAndOwnerAsync(IPrincipalUtils principalUtils, IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, ClaimsPrincipal user, string entity, Guid ownerId, IFormFileCollection files)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var userId = principalUtils.GetUserId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        foreach (var file in files)
        {
            var storagePath = await storageProvider.UploadForEntityAndOwnerAsync(tenantId, entity, ownerId, file.FileName, file.ContentType, file.OpenReadStream());
            
            var attachment = await repository.SingleByEntityOwnerAndFileNameAsync(tenantId, entity, ownerId, file.FileName);

            if (attachment == null)
            {
                attachment = await repository.NewAsync(tenantId, AttachmentPrimaryQuery, claims);
            }
            
            attachment.Entity = entity;
            attachment.OwnerId = ownerId;
            attachment.FileName = file.FileName;
            attachment.ContentType = file.ContentType;
            attachment.FileSize = file.Length;
            attachment.StoragePath = storagePath;

            await repository.SaveAsync(tenantId, userId, AttachmentPrimaryQuery, claims, attachment);
        }

        return Results.Created();
    }
    
    private static async Task<IResult> HandleDownloadForEntityAndOwnerByIdAsync(IPrincipalUtils principalUtils, IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, ClaimsPrincipal user, string entity, Guid ownerId, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        var attachment = await repository.ByIdAsync(tenantId, AttachmentPrimaryQuery, claims, id);
        
        if (attachment == null || String.IsNullOrEmpty(attachment.StoragePath))
        {
            return Results.NotFound($"Attachment with ID {id} not found for entity '{entity}' and owner '{ownerId}'.");
        }
        
        var fileContent = await storageProvider.DownloadForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
        
        if (fileContent == null)
        {
            return Results.NotFound($"File not found for attachment with ID {id} for entity '{entity}' and owner '{ownerId}'.");
        }
        
        return Results.File(fileContent, attachment.ContentType, attachment.FileName);
    }
    
    private static async Task<IResult> HandleDropForEntityAndOwnerByIdAsync(IPrincipalUtils principalUtils, IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, ClaimsPrincipal user, string entity, Guid ownerId, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var userId = principalUtils.GetUserId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        var attachment = await repository.ByIdAsync(tenantId, AttachmentPrimaryQuery, claims, id);
        
        if (attachment == null || String.IsNullOrEmpty(attachment.StoragePath))
        {
            return Results.NotFound($"Attachment with ID {id} not found for entity '{entity}' and owner '{ownerId}'.");
        }
        
        await storageProvider.DropForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
        
        await repository.RemoveAsync(tenantId, userId, claims, new Dictionary<string, object>()
        {
            { "Id", id }
        });
        
        return Results.Ok();
    }
    
    private static async Task<IResult> HandleDropAllForEntityAndOwnerAsync(IPrincipalUtils principalUtils, IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, ClaimsPrincipal user, string entity, Guid ownerId)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var userId = principalUtils.GetUserId(user);
        var claims = principalUtils.GetUserClaims(user);

        var attachments = await repository.AllByEntityAndOwnerIdAsync(tenantId, entity, ownerId);

        foreach (var attachment in attachments)
        {
            if (!string.IsNullOrEmpty(attachment.StoragePath))
            {
                await storageProvider.DropForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
            }    
            
            await repository.RemoveAsync(tenantId, userId, claims, new Dictionary<string, object>()
            {
                { "Id", attachment.Id }
            });
        }
        
        return Results.Ok();
    }
    
    private static async Task<IResult> HandleAllForTenantEntityAndOwnerAsync(IAttachmentRepository repository, Guid tenantId, string entity, Guid ownerId)
    {
        return Results.Ok(await repository.AllByEntityAndOwnerIdAsync(tenantId, entity, ownerId));
    }
    
    private static async Task<IResult> HandleUploadForTenantEntityAndOwnerBehalfOfUserAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, Guid userId, string entity, Guid ownerId, IFormFileCollection files)
    {
        foreach (var file in files)
        {
            var storagePath = await storageProvider.UploadForEntityAndOwnerAsync(tenantId, entity, ownerId, file.FileName, file.ContentType, file.OpenReadStream());
            
            var attachment = await repository.SingleByEntityOwnerAndFileNameAsync(tenantId, entity, ownerId, file.FileName);

            if (attachment == null)
            {
                attachment = await repository.NewAsync(tenantId, AttachmentPrimaryQuery, ImmutableDictionary<string, object>.Empty);
            }
            
            attachment.Entity = entity;
            attachment.OwnerId = ownerId;
            attachment.FileName = file.FileName;
            attachment.ContentType = file.ContentType;
            attachment.FileSize = file.Length;
            attachment.StoragePath = storagePath;

            await repository.SaveAsync(tenantId, userId, AttachmentPrimaryQuery, ImmutableDictionary<string, object>.Empty, attachment);
        }

        return Results.Created();
    }
    
    private static async Task<IResult> HandleDownloadForTenantEntityAndOwnerByIdAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, string entity, Guid ownerId, Guid id)
    {
        var attachment = await repository.ByIdAsync(tenantId, AttachmentPrimaryQuery, ImmutableDictionary<string, object>.Empty, id);
        
        if (attachment == null || String.IsNullOrEmpty(attachment.StoragePath))
        {
            return Results.NotFound($"Attachment with ID {id} not found for entity '{entity}' and owner '{ownerId}'.");
        }
        
        var fileContent = await storageProvider.DownloadForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
        
        if (fileContent == null)
        {
            return Results.NotFound($"File not found for attachment with ID {id} for entity '{entity}' and owner '{ownerId}'.");
        }
        
        return Results.File(fileContent, attachment.ContentType, attachment.FileName);
    }
    
    private static async Task<IResult> HandleDownloadForTenantEntityAndOwnerByFilenameAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, string entity, Guid ownerId, string filename)
    {
        var attachment = await repository.SingleByEntityOwnerAndFileNameAsync(tenantId, entity, ownerId, filename);
        
        if (attachment == null || String.IsNullOrEmpty(attachment.StoragePath))
        {
            return Results.NotFound($"Attachment {filename} not found for entity '{entity}' and owner '{ownerId}'.");
        }
        
        var fileContent = await storageProvider.DownloadForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
        
        if (fileContent == null)
        {
            return Results.NotFound($"File not found for attachment {filename} for entity '{entity}' and owner '{ownerId}'.");
        }
        
        return Results.File(fileContent, attachment.ContentType, attachment.FileName);
    }
    
    private static async Task<IResult> HandleDropForTenantEntityAndOwnerByIdBehalfOfUserAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, Guid userId, string entity, Guid ownerId, Guid id)
    {
        var attachment = await repository.ByIdAsync(tenantId, AttachmentPrimaryQuery, ImmutableDictionary<string, object>.Empty, id);
        
        if (attachment == null || String.IsNullOrEmpty(attachment.StoragePath))
        {
            return Results.NotFound($"Attachment with ID {id} not found for entity '{entity}' and owner '{ownerId}'.");
        }
        
        await storageProvider.DropForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
        
        await repository.RemoveAsync(tenantId, userId, ImmutableDictionary<string, object>.Empty, new Dictionary<string, object>()
        {
            { "Id", id }
        });
        
        return Results.Ok();
    }
    
    private static async Task<IResult> HandleDropForTenantEntityAndOwnerByFilenameBehalfOfUserAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, Guid userId, string entity, Guid ownerId, string filename)
    {
        var attachment = await repository.SingleByEntityOwnerAndFileNameAsync(tenantId, entity,ownerId, filename);
        
        if (attachment == null || String.IsNullOrEmpty(attachment.StoragePath))
        {
            return Results.NotFound($"Attachment {filename} not found for entity '{entity}' and owner '{ownerId}'.");
        }
        
        await storageProvider.DropForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
        
        await repository.RemoveAsync(tenantId, userId, ImmutableDictionary<string, object>.Empty, new Dictionary<string, object>()
        {
            { "Id", attachment.Id }
        });
        
        return Results.Ok();
    }
    
    private static async Task<IResult> HandleDropAllForTenantEntityAndOwnerBehalfOfUserAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, Guid userId, string entity, Guid ownerId)
    {
        var attachments = await repository.AllByEntityAndOwnerIdAsync(tenantId, entity, ownerId);

        foreach (var attachment in attachments)
        {
            if (!string.IsNullOrEmpty(attachment.StoragePath))
            {
                await storageProvider.DropForEntityAndOwnerByPathAsync(tenantId, entity, ownerId, attachment.StoragePath);
            }    
            
            await repository.RemoveAsync(tenantId, userId, ImmutableDictionary<string, object>.Empty, new Dictionary<string, object>()
            {
                { "Id", attachment.Id }
            });
        }
        
        return Results.Ok();
    }
    
    private static async Task<IResult> HandleDropAllForTenantAndEntityBehalfOfUserAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, Guid userId, string entity)
    {
        var attachments = await repository.AllByEntityAsync(tenantId, entity);

        foreach (var attachment in attachments)
        {
            if (!string.IsNullOrEmpty(attachment.StoragePath))
            {
                await storageProvider.DropForEntityAndOwnerByPathAsync(tenantId, entity, attachment.OwnerId, attachment.StoragePath);
            }    
            
            await repository.RemoveAsync(tenantId, userId, ImmutableDictionary<string, object>.Empty, new Dictionary<string, object>()
            {
                { "Id", attachment.Id }
            });
        }
        
        return Results.Ok();
    }
    
    private static async Task<IResult> HandleDropAllForTenantBehalfOfUserAsync(IAttachmentStorageProvider storageProvider, IAttachmentRepository repository, Guid tenantId, Guid userId)
    {
        var attachments = await repository.AllAsync(tenantId);

        foreach (var attachment in attachments)
        {
            if (!string.IsNullOrEmpty(attachment.StoragePath))
            {
                await storageProvider.DropForEntityAndOwnerByPathAsync(tenantId, attachment.Entity, attachment.OwnerId, attachment.StoragePath);
            }    
            
            await repository.RemoveAsync(tenantId, userId, ImmutableDictionary<string, object>.Empty, new Dictionary<string, object>()
            {
                { "Id", attachment.Id }
            });
        }
        
        return Results.Ok();
    }
}