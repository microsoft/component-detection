#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

/// <summary>
/// Compares two <see cref="TypedComponent"/>s by their <see cref="TypedComponent.Id"/>.
/// </summary>
public class ComponentComparer : EqualityComparer<TypedComponent>
{
    /// <summary>
    /// Determines whether the specified objects are equal.
    /// </summary>
    /// <param name="x">The first object of type <see cref="TypedComponent"/> to compare.</param>
    /// <param name="y">The second object of type <see cref="TypedComponent"/> to compare.</param>
    /// <returns>true if the specified objects are equal; otherwise, false.</returns>
    public override bool Equals(TypedComponent x, TypedComponent y)
    {
        return x.Id.Equals(y.Id);
    }

    /// <summary>
    /// Returns a hash code for the specified object.
    /// </summary>
    /// <param name="obj">The <see cref="TypedComponent"/> for which a hash code is to be returned.</param>
    /// <returns>A hash code for the specified object.</returns>
    public override int GetHashCode(TypedComponent obj)
    {
        return obj.Id.GetHashCode();
    }
}
