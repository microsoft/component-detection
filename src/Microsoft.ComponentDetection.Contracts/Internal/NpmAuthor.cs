﻿using System;

namespace Microsoft.ComponentDetection.Contracts.Internal
{
    public class NpmAuthor
    {
        public NpmAuthor(string name, string email = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Email = string.IsNullOrEmpty(email) ? null : email;
        }

        public string Name { get; set; }

        public string Email { get; set; }
    }
}
