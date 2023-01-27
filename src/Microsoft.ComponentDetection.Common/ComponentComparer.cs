namespace Microsoft.ComponentDetection.Common;
using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

public class ComponentComparer : EqualityComparer<TypedComponent>
{
    public override bool Equals(TypedComponent t0, TypedComponent t1) => t0.Id.Equals(t1.Id);

    public override int GetHashCode(TypedComponent typedComponent) => typedComponent.Id.GetHashCode();
}
