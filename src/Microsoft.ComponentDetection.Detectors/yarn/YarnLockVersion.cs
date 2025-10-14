#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn;

public enum YarnLockVersion
{
    Invalid = 0,
    V1 = 1,

    // Berry is the public codename for the Yarn v2 rewrite.
    // The lockfile has remained the same syntactically (YAML) since this rewrite,
    // and all minor changes to the lockfile (up to Yarn v4/lockfile v8) have been irrelevant to CD
    Berry = 2,
}
