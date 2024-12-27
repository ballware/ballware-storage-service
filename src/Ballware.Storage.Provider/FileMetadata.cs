using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Ballware.Storage.Provider;

public sealed class FileMetadata
{
    [JsonProperty("Name")]
    [JsonPropertyName("Name")]
    public required string Filename { get; set; }
}