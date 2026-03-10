namespace Microsoft.ComponentDetection.Detectors.Rpm;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Represents package information extracted from an RPM header.
/// </summary>
internal sealed class RpmPackageInfo
{
    public string Name { get; set; }

    public string Version { get; set; }

    public string Release { get; set; }

    public int? Epoch { get; set; }

    public string Arch { get; set; }

    public string SourceRpm { get; set; }

    public string Vendor { get; set; }

    public List<string> Provides { get; set; } = [];

    public List<string> Requires { get; set; } = [];
}
