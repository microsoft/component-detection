# Creating a new service

We will be using [this PR](https://github.com/microsoft/component-detection/pull/12) following `EnvironmentVariableService` as a model for adding a new service.
The following steps are distilled and organized from that PR.

1. Create your new service interface in `src/Microsoft.ComponentDetection.Contracts/IMyNewService.cs`
2. Create your new service implementation in `src/Microsoft.ComponentDetection.Common/MyNewService.cs` implementing and exporting `IMyNewService`.

```c#
using System;
using System.Composition;
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common
{
    [Export(typeof(IMyNewService))]
    public class MyNewService : IMyNewService
    {
        ...
    }
}
```

3. Add your new service to `src/Microsoft.ComponentDetection.Contracts/IDetectorDependencies.cs`

```c#
namespace Microsoft.ComponentDetection.Contracts
{
    public interface IDetectorDependencies
    {
        ...
        IMyNewService MyNewService { get; set; }
    }
}
```

4. Add your new service to `src/Microsoft.ComponentDetection.Common/DetectorDependencies.cs`

```c#
namespace Microsoft.ComponentDetection.Common
{
    [Export(typeof(IDetectorDependencies))]
    public class DetectorDependencies : IDetectorDependencies
    {
        ...
        [Import]
        public IMyNewService MyNewService { get; set; }
    }
}
```

5. Add your new service to `src/Microsoft.ComponentDetection.Contracts/Internal/InjectionParameters.cs`

```c#
namespace Microsoft.ComponentDetection.Contracts.Internal
{
    internal class InjectionParameters
    {
        internal InjectionParameters(IDetectorDependencies detectorDependencies)
        {
            ...
            myNewServiceStatic = detectorDependencies.MyNewService;
        }
    }
    
    ...
    private static IMyNewService myNewServiceStatic;
    
    ...
    [Export(typeof(IMyNewService))]
    public IMyNewService MyNewService => myNewServiceStatic;
}
```
