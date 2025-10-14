#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Extensions;

using System;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

/// <inheritdoc cref="ITypeResolver" />
public sealed class TypeResolver : ITypeResolver, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypeResolver"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    internal TypeResolver(ServiceProvider serviceProvider) => this.ServiceProvider = serviceProvider;

    internal ServiceProvider ServiceProvider { get; set; }

    /// <inheritdoc />
    public void Dispose() => this.ServiceProvider.Dispose();

    /// <inheritdoc />
    public object Resolve(Type type) => this.ServiceProvider.GetService(type) ?? Activator.CreateInstance(type);
}
