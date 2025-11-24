#nullable disable
namespace Microsoft.ComponentDetection.Contracts.Internal;

using System;
using System.Text.Json.Serialization;

public class NpmAuthor
{
    public NpmAuthor(string name, string email = null)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.Email = string.IsNullOrEmpty(email) ? null : email;
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }
}
