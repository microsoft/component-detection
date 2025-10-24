#nullable disable
namespace Microsoft.ComponentDetection.Orchestrator.Tests.Extensions;

using System;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.ComponentDetection.Orchestrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class TypeRegistrarTests
{
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Unit tests.")]
    [SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Unit tests.")]
    public interface IService
    {
    }

    [TestMethod]
    public void Build_ReturnsTypeResolver()
    {
        var services = new ServiceCollection();
        var typeRegistrar = new TypeRegistrar(services);

        var result = typeRegistrar.Build();
        var result2 = typeRegistrar.Build();

        result.Should().NotBeNull();
        result.Should().BeOfType<TypeResolver>();
        result2.Should().BeSameAs(result);
    }

    [TestMethod]
    public void Register_AddsServiceAndImplementation()
    {
        var services = new ServiceCollection();
        var typeRegistrar = new TypeRegistrar(services);

        typeRegistrar.Register(typeof(IService), typeof(Implementation));

        var serviceProvider = services.BuildServiceProvider();
        var resolvedService = serviceProvider.GetRequiredService<IService>();
        resolvedService.Should().NotBeNull();
        resolvedService.Should().BeOfType<Implementation>();
    }

    [TestMethod]
    public void RegisterInstance_AddsServiceAndInstance()
    {
        var services = new ServiceCollection();
        var typeRegistrar = new TypeRegistrar(services);
        var instance = new Implementation();

        typeRegistrar.RegisterInstance(typeof(IService), instance);

        var serviceProvider = services.BuildServiceProvider();
        var resolvedService = serviceProvider.GetRequiredService<IService>();
        resolvedService.Should().NotBeNull();
        resolvedService.Should().BeSameAs(instance);
    }

    [TestMethod]
    public void RegisterLazy_AddsServiceAndFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var typeRegistrar = new TypeRegistrar(services);
        var factoryMock = new Mock<Func<IService>>();
        factoryMock.Setup(factory => factory()).Returns(new Implementation());

        typeRegistrar.RegisterLazy(typeof(IService), factoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();
        var resolvedService = serviceProvider.GetRequiredService<IService>();

        factoryMock.Verify(factory => factory(), Times.Once);
        resolvedService.Should().NotBeNull();
    }

    private class Implementation : IService
    {
    }
}
