# Creating a new service

Component Detection uses standard .NET dependency injection for service registration. This guide shows how to add a new service to the system.

## Steps to create a new service

1. **Create your service interface** in `src/Microsoft.ComponentDetection.Contracts/IMyNewService.cs`

```c#
namespace Microsoft.ComponentDetection.Contracts
{
    public interface IMyNewService
    {
        // Define your service methods
        string DoSomething();
    }
}
```

2. **Create your service implementation** in `src/Microsoft.ComponentDetection.Common/MyNewService.cs`

```c#
using Microsoft.ComponentDetection.Contracts;

namespace Microsoft.ComponentDetection.Common
{
    public class MyNewService : IMyNewService
    {
        // Inject any dependencies your service needs
        public MyNewService(ILogger<MyNewService> logger)
        {
            // Constructor injection
        }

        public string DoSomething()
        {
            // Implementation
        }
    }
}
```

3. **Register your service** in `src/Microsoft.ComponentDetection.Orchestrator/Extensions/ServiceCollectionExtensions.cs`

Add your service registration to the `AddComponentDetection` method:

```c#
public static IServiceCollection AddComponentDetection(this IServiceCollection services)
{
    // ... existing registrations ...

    // Your new service
    services.AddSingleton<IMyNewService, MyNewService>();

    // ... more registrations ...
    return services;
}
```

4. **Use your service** in detectors or other services via constructor injection:

```c#
public class MyDetector : FileComponentDetector
{
    private readonly IMyNewService myNewService;

    public MyDetector(
        IComponentStreamEnumerableFactory componentStreamEnumerableFactory,
        IObservableDirectoryWalkerFactory walkerFactory,
        ILogger<MyDetector> logger,
        IMyNewService myNewService)  // Inject your service
    {
        this.ComponentStreamEnumerableFactory = componentStreamEnumerableFactory;
        this.Scanner = walkerFactory;
        this.Logger = logger;
        this.myNewService = myNewService;
    }
}
```

## Service Lifetimes

- Use `AddSingleton` for stateless services or services that should be reused across the application lifetime
- Use `AddScoped` for services that should be created once per scan operation (rare in this codebase)
- Use `AddTransient` for lightweight, stateless services that should be created each time they're requested

Most services in Component Detection are registered as singletons.
