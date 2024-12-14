using System.Net;
using Ballware.Storage.Provider;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ballware.Storage.Service.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class FileController : ControllerBase
{
    private readonly IFileStorage _fileStorage;

    public FileController(IFileStorage storage)
    {
        _fileStorage = storage;
    }

    [HttpGet]
    [Route("all/{owner}")]
    [Authorize("storageApi")]
    [ApiExplorerSettings(GroupName = "storage")]
    [SwaggerOperation(
      Summary = "Query existing files for owner",
      Description = "",
      OperationId = "AllFilesForOwner"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Existing files for owner", typeof(IEnumerable<FileMetadata>), new[] { MimeMapping.KnownMimeTypes.Json })]
    public virtual async Task<IActionResult> All(
        [SwaggerParameter("Owner id")] string owner
    )
    {
        var files = await _fileStorage.EnumerateFilesAsync(owner);

        return Ok(files.Select(f => new { Name = f.Filename }));
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("byname/{owner}")]
    [ApiExplorerSettings(GroupName = "storage")]
    [SwaggerOperation(
      Summary = "Download file",
      Description = "",
      OperationId = "FileByNameForOwner"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound)]
    [SwaggerResponse((int)HttpStatusCode.OK, "File download", typeof(FileResult))]
    public virtual async Task<IActionResult> ByName(string owner, [FromQuery] string file)
    {
        var stream = await _fileStorage.OpenFileAsync(owner, file);

        if (stream != null)
        {
            return new FileStreamResult(stream, MimeMapping.MimeUtility.GetMimeMapping(Path.GetExtension(file)))
            {
                FileDownloadName = file
            };
        }
        else
        {
            return NotFound();
        }
    }

    [HttpPost]
    [Route("upload/{owner}")]
    [Authorize("storageApi")]
    [ApiExplorerSettings(GroupName = "storage")]
    [SwaggerOperation(
      Summary = "Upload file",
      Description = "",
      OperationId = "UploadFileForOwner"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.BadRequest, "No files uploaded")]
    [SwaggerResponse((int)HttpStatusCode.Created, "File created")]
    public virtual async Task<IActionResult> UploadFile(string owner, [FromForm(Name = "files[]")] List<IFormFile> files)
    {
        IFormFile? lastFile = null;

        foreach (var file in files)
        {
            lastFile = file;
            await _fileStorage.UploadFileAsync(owner, file.FileName, file.OpenReadStream());
        }

        if (lastFile != null)
        {
            return Created(Url.Action(nameof(ByName), new
            {
                owner = owner,
                file = lastFile.FileName
            }), null);    
        } 
        
        return BadRequest("No file uploaded");
    }

    [HttpDelete]
    [Route("byname/{owner}")]
    [Authorize("storageApi")]
    [ApiExplorerSettings(GroupName = "storage")]
    [SwaggerOperation(
      Summary = "Remove file",
      Description = "",
      OperationId = "RemoveFileForOwner"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.OK, "File removed")]
    public virtual async Task<IActionResult> DeleteFile(string owner, [FromQuery] string file)
    {
        await _fileStorage.DropFileAsync(owner, file);

        return Ok();
    }
}