using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

public class DependencyGraph : Dictionary<string, HashSet<string>>
{
}
