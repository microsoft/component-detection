#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests.Utilities;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

// https://stackoverflow.com/questions/35128996/groupby-on-complex-object-e-g-listt
public class EnumerableStringComparer : IEqualityComparer<IEnumerable<string>>
{
    public bool Equals([AllowNull] IEnumerable<string> x, [AllowNull] IEnumerable<string> y)
    {
        return x.SequenceEqual(y);
    }

    public int GetHashCode([DisallowNull] IEnumerable<string> obj)
    {
        return obj.Aggregate(0, (a, y) => a ^ y.GetHashCode());
    }
}
