namespace Microsoft.ComponentDetection.Common;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class ComponentComparer : EqualityComparer<TypedComponent>
{
    public override bool Equals(TypedComponent x, TypedComponent y)
    {
        return x.Id.Equals(y.Id);
    }

    public override int GetHashCode(TypedComponent obj)
    {
        return obj.Id.GetHashCode();
    }
}
