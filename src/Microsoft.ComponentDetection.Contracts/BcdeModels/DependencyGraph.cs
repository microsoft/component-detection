#nullable disable
namespace Microsoft.ComponentDetection.Contracts.BcdeModels;

using System.Collections.Generic;

public class DependencyGraph : Dictionary<string, HashSet<string>>
{
}
