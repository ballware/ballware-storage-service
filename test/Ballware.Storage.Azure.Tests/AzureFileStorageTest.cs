using System.Collections.ObjectModel;
using System.Text;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Ballware.Storage.Azure.Internal;
using Ballware.Storage.Provider;
using Moq;

namespace Ballware.Storage.Azure.Tests;

[TestFixture]
[Category("UnitTest")]
public class AzureFileStorageTest
{
    private Mock<ShareDirectoryClient> DirectoryClientMock { get; } = new();
    private Mock<ShareClient> ShareClientMock { get; } = new();
    private Mock<IShareClientFactory> ShareClientFactoryMock { get; } = new();

    [SetUp]
    protected void SetupMocks()
    {
        ShareClientMock.Setup(c => c.GetDirectoryClient("fake_owner")).Returns(DirectoryClientMock.Object);
        ShareClientFactoryMock.Setup(f => f.GetFileShare("fake_connection_string", "fake_share")).Returns(ShareClientMock.Object);
    }

    [Test]
    public async Task Enumerate_existing_files_succeeds()
    {
        var ownerFiles = new List<FileMetadata>() { new FileMetadata() { Filename = "File1" }, new FileMetadata() { Filename = "File2" } };

        var fileItemsList = new ReadOnlyCollection<ShareFileItem>(new List<ShareFileItem>(ownerFiles.Select(f =>
            FilesModelFactory.StorageFileItem(false, f.Filename, 0))));

        var mockedFilesResponse = AsyncPageable<ShareFileItem>.FromPages(new[]
            { Page<ShareFileItem>.FromValues(fileItemsList, null, Mock.Of<Response>()) });

        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(true, Mock.Of<Response>()));
        DirectoryClientMock.Setup(c => c.GetFilesAndDirectoriesAsync(It.IsAny<ShareDirectoryGetFilesAndDirectoriesOptions>(), It.IsAny<CancellationToken>()))
            .Returns(() => mockedFilesResponse);

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        var files = await subject.EnumerateFilesAsync("fake_owner");

        Assert.That(files?.Select(f => f.Filename).ToArray(), Is.EqualTo(ownerFiles.Select(f => f.Filename).ToArray()));
    }

    [Test]
    public async Task Enumerate_owner_notexisting_succeeds()
    {
        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(false, Mock.Of<Response>()));

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        var files = await subject.EnumerateFilesAsync("fake_owner");

        Assert.That(files, Is.Empty);
    }

    [Test]
    public async Task Open_existing_file_succeeds()
    {
        var expectedFileContent = "Fake content";
        var fileClientMock = new Mock<ShareFileClient>();

        fileClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Response.FromValue(true, Mock.Of<Response>()));
        fileClientMock.Setup(c => c.OpenReadAsync(It.IsAny<long>(), It.IsAny<int?>(), It.IsAny<ShareFileRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(expectedFileContent)));

        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(true, Mock.Of<Response>()));
        DirectoryClientMock.Setup(c => c.GetFileClient("requested_file"))
            .Returns(() => fileClientMock.Object);

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        var actualFileContentStream = await subject.OpenFileAsync("fake_owner", "requested_file");

        Assert.That(actualFileContentStream, Is.Not.Null);

        using var actualContentReader = new StreamReader(actualFileContentStream);

        var actualFileContent = await actualContentReader.ReadToEndAsync();

        Assert.That(actualFileContent, Is.EqualTo(expectedFileContent));
    }

    [Test]
    public async Task Open_file_unknown_owner_succeeds()
    {
        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(false, Mock.Of<Response>()));

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        var actualFileContentStream = await subject.OpenFileAsync("fake_owner", "requested_file");

        Assert.That(actualFileContentStream, Is.Null);
    }

    [Test]
    public async Task Open_file_unknown_file_succeeds()
    {
        var fileClientMock = new Mock<ShareFileClient>();

        fileClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(false, Mock.Of<Response>()));

        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(true, Mock.Of<Response>()));
        DirectoryClientMock.Setup(c => c.GetFileClient("requested_file"))
            .Returns(() => fileClientMock.Object);

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        var actualFileContentStream = await subject.OpenFileAsync("fake_owner", "requested_file");

        Assert.That(actualFileContentStream, Is.Null);
    }

    [Test]
    public async Task Upload_file_unknown_owner_succeeds()
    {
        var expectedFileContent = "Fake content";
        var fileClientMock = new Mock<ShareFileClient>();

        fileClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Response.FromValue(false, Mock.Of<Response>()));
        fileClientMock.Setup(c => c.UploadAsync(It.IsAny<Stream>(), It.IsAny<ShareFileUploadOptions>(), It.IsAny<CancellationToken>()));

        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(false, Mock.Of<Response>()));
        DirectoryClientMock.Setup(c => c.GetFileClient("requested_file"))
            .Returns(() => fileClientMock.Object);

        var expectedFileContentStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedFileContent));

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        await subject.UploadFileAsync("fake_owner", "requested_file", expectedFileContentStream);

        fileClientMock.Verify(c => c.UploadAsync(expectedFileContentStream, It.IsAny<ShareFileUploadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Upload_file_known_owner_succeeds()
    {
        var expectedFileContent = "Fake content";
        var fileClientMock = new Mock<ShareFileClient>();

        fileClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Response.FromValue(false, Mock.Of<Response>()));
        fileClientMock.Setup(c => c.UploadAsync(It.IsAny<Stream>(), It.IsAny<ShareFileUploadOptions>(), It.IsAny<CancellationToken>()));

        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(true, Mock.Of<Response>()));
        DirectoryClientMock.Setup(c => c.GetFileClient("requested_file"))
            .Returns(() => fileClientMock.Object);

        var expectedFileContentStream = new MemoryStream(Encoding.UTF8.GetBytes(expectedFileContent));

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        await subject.UploadFileAsync("fake_owner", "requested_file", expectedFileContentStream);

        fileClientMock.Verify(c => c.UploadAsync(expectedFileContentStream, It.IsAny<ShareFileUploadOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Drop_file_unknown_owner_succeeds()
    {
        var fileClientMock = new Mock<ShareFileClient>();

        fileClientMock.Setup(c => c.DeleteIfExistsAsync(It.IsAny<ShareFileRequestConditions>(), It.IsAny<CancellationToken>()));

        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(false, Mock.Of<Response>()));
        DirectoryClientMock.Setup(c => c.GetFileClient("requested_file"))
            .Returns(() => fileClientMock.Object);

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        await subject.DropFileAsync("fake_owner", "requested_file");

        fileClientMock.Verify(c => c.DeleteIfExistsAsync(It.IsAny<ShareFileRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Drop_file_known_owner_succeeds()
    {
        var fileClientMock = new Mock<ShareFileClient>();

        fileClientMock.Setup(c => c.DeleteIfExistsAsync(It.IsAny<ShareFileRequestConditions>(), It.IsAny<CancellationToken>()));

        DirectoryClientMock.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(true, Mock.Of<Response>()));
        DirectoryClientMock.Setup(c => c.GetFileClient("requested_file"))
            .Returns(() => fileClientMock.Object);

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        await subject.DropFileAsync("fake_owner", "requested_file");

        fileClientMock.Verify(c => c.DeleteIfExistsAsync(It.IsAny<ShareFileRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Drop_all_succeeds()
    {
        DirectoryClientMock.Setup(c => c.DeleteIfExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Response.FromValue(true, Mock.Of<Response>()));

        var subject = new AzureFileStorage(ShareClientFactoryMock.Object, "fake_connection_string", "fake_share");

        await subject.DropAllAsync("fake_owner");

        DirectoryClientMock.Verify(c => c.DeleteIfExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}