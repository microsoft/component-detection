using System;

namespace Microsoft.ComponentDetection.Contracts.Internal;

public class NpmAuthor
{
    public NpmAuthor(string name, string email = null)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.Email = string.IsNullOrEmpty(email) ? null : email;
    }

    public string Name { get; set; }

    public string Email { get; set; }
}
