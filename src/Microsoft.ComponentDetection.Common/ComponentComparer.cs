using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Common
{
    public class ComponentComparer : EqualityComparer<TypedComponent>
    {
        public override bool Equals(TypedComponent t0, TypedComponent t1)
        {
            return t0.Id.Equals(t1.Id);
        }

        public override int GetHashCode(TypedComponent typedComponent)
        {
            return typedComponent.Id.GetHashCode();
        }
    }
}
