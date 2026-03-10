#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Extensions;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

/// <inheritdoc cref="ITypeResolver" />
public sealed class TypeRegistrar : ITypeRegistrar, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypeRegistrar"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public TypeRegistrar(IServiceCollection services)
    {
        this.Services = services;
        this.BuiltProviders = [];
    }

    private IServiceCollection Services { get; }

    private IList<IDisposable> BuiltProviders { get; }

    private TypeResolver TypeResolver { get; set; }

    /// <inheritdoc />
    public ITypeResolver Build()
    {
        if (this.TypeResolver is not null)
        {
            this.TypeResolver.ServiceProvider = this.Services.BuildServiceProvider();
            return this.TypeResolver;
        }

        var buildServiceProvider = this.Services.BuildServiceProvider();
        this.BuiltProviders.Add(buildServiceProvider);
        return this.TypeResolver = new TypeResolver(buildServiceProvider);
    }

    /// <inheritdoc />
    public void Register(Type service, Type implementation) => this.Services.AddSingleton(service, implementation);

    /// <inheritdoc />
    public void RegisterInstance(Type service, object implementation) => this.Services.AddSingleton(service, implementation);

    /// <inheritdoc />
    public void RegisterLazy(Type service, Func<object> factory) => this.Services.AddSingleton(service, _ => factory());

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var provider in this.BuiltProviders)
        {
            provider.Dispose();
        }
    }
}
