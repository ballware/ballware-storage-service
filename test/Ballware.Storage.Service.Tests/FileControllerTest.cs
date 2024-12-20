using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Ballware.Storage.Provider;
using Ballware.Storage.Service.Tests.Helper;
using Moq;

namespace Ballware.Storage.Service.Tests;

[TestFixture]
[Category("IntegrationTest")]
public class FileControllerTest : MockableBaseTest
{
    [Test]
    [Category("PreCommit")]
    public async Task Query_existing_files_for_unauthorized_fail()
    {
        var owner = Guid.NewGuid();
        var ownerFiles = new List<FileMetadata>() { new FileMetadata() { Filename = "File1" }, new FileMetadata() { Filename = "File2" } };

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.EnumerateFilesAsync(owner.ToString())).ReturnsAsync(ownerFiles);

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        var response = await GetClient().GetAsync($"/api/file/all/{owner}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Category("PreCommit")]
    public async Task Query_existing_files_for_owner_succeeds()
    {
        var owner = Guid.NewGuid();
        var ownerFiles = new List<FileMetadata>() { new FileMetadata() { Filename = "File1" }, new FileMetadata() { Filename = "File2" } };

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.EnumerateFilesAsync(owner.ToString())).ReturnsAsync(ownerFiles);

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        var response = await GetAuthenticatedClient().GetAsync($"/api/file/all/{owner}");
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var responseContent = await response.Content.ReadFromJsonAsync<IEnumerable<FileMetadata>>();

        Assert.That(responseContent?.Select(f => f.Filename).ToArray(), Is.EqualTo(ownerFiles.Select(f => f.Filename).ToArray()));
    }

    [Test]
    [Category("PreCommit")]
    public async Task Download_byname_for_unknown_return_not_found()
    {
        var owner = Guid.NewGuid();

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.OpenFileAsync(owner.ToString(), "File1")).ReturnsAsync(null as Stream);

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        var response = await GetClient().GetAsync($"/api/file/byname/{owner}?file=File1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [Category("PreCommit")]
    public async Task Download_byname_for_existing_succeeds()
    {
        var owner = Guid.NewGuid();

        var mockedFileStorage = new Mock<IFileStorage>();

        var expectedStream = new MemoryStream();
        await expectedStream.WriteAsync(Encoding.UTF8.GetBytes("some fake content"));
        expectedStream.Position = 0;

        var expectedLength = expectedStream.Length;

        mockedFileStorage.Setup(m => m.OpenFileAsync(owner.ToString(), "File1")).ReturnsAsync(expectedStream);

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        var response = await GetClient().GetAsync($"/api/file/byname/{owner}?file=File1");

        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(response.Content, Is.TypeOf<StreamContent>());
        var actualStream = await response.Content.ReadAsStreamAsync();

        Assert.That(actualStream.Length, Is.EqualTo(expectedLength));
    }

    [Test]
    [Category("PreCommit")]
    public async Task Upload_byname_unauthorized_fail()
    {
        var owner = Guid.NewGuid();

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.UploadFileAsync(owner.ToString(), "File1", It.IsAny<Stream>())).Returns(Task.CompletedTask);

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("some fake content"));

        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(fileContent, "files[]", "File1");

        var response = await GetClient().PostAsync($"/api/file/upload/{owner}", form);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Category("PreCommit")]
    public async Task Upload_byname_without_file_fail()
    {
        var owner = Guid.NewGuid();

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.UploadFileAsync(owner.ToString(), "File1", It.IsAny<Stream>())).Returns(Task.CompletedTask);

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        using var form = new MultipartFormDataContent();

        var response = await GetAuthenticatedClient().PostAsync($"/api/file/upload/{owner}", form);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [Category("PreCommit")]
    public async Task Upload_byname_succeeds()
    {
        var owner = Guid.NewGuid();

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.UploadFileAsync(owner.ToString(), "File1", It.IsAny<Stream>())).Returns(Task.CompletedTask);

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("some fake content")));

        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
        form.Add(fileContent, "files[]", "File1");

        var response = await GetAuthenticatedClient().PostAsync($"/api/file/upload/{owner}", form);

        Assert.That(response.IsSuccessStatusCode, Is.True);
        mockedFileStorage.Verify(m => m.UploadFileAsync(owner.ToString(), "File1", It.IsAny<Stream>()), Times.Once);
    }

    [Test]
    [Category("PreCommit")]
    public async Task Delete_byname_unauthorized_fail()
    {
        var owner = Guid.NewGuid();

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.DropFileAsync(owner.ToString(), "File1"));

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        var response = await GetClient().DeleteAsync($"/api/file/byname/{owner}?file=File1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [Category("PreCommit")]
    public async Task Delete_byname_succeeds()
    {
        var owner = Guid.NewGuid();

        var mockedFileStorage = new Mock<IFileStorage>();

        mockedFileStorage.Setup(m => m.DropFileAsync(owner.ToString(), "File1"));

        MockedServices?.AddMock<IFileStorage>(mockedFileStorage.Object);

        var response = await GetAuthenticatedClient().DeleteAsync($"/api/file/byname/{owner}?file=File1");

        Assert.That(response.IsSuccessStatusCode, Is.True);
        mockedFileStorage.Verify(m => m.DropFileAsync(owner.ToString(), "File1"), Times.Once);
    }
}