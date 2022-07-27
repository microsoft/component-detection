using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CycloneDX.Json;
using CycloneDX.Models;
using CycloneDX.Utils;
using Microsoft.ComponentDetection.Contracts.BcdeModels;
using Microsoft.ComponentDetection.Contracts.TypedComponent;

namespace Microsoft.ComponentDetection.Contracts.Mappers
{
    public static class CycloneDx
    {
        public static string ToCycloneDxString(this ScanResult scanResult) => Serializer.Serialize(scanResult.ToCycloneDx());

        public static Bom ToCycloneDx(this ScanResult scanResult) => new Bom
        {
            SerialNumber = CycloneDXUtils.GenerateSerialNumber(),
            Metadata = new Metadata
            {
                Timestamp = DateTime.UtcNow,
                Tools = new List<Tool>
                {
                    new Tool
                    {
                        Vendor = "Microsoft",
                        Name = "Component Detection",
                        Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                        ExternalReferences = new List<ExternalReference>
                        {
                            new ExternalReference
                            {
                                Type = ExternalReference.ExternalReferenceType.Vcs,
                                Url = "https://github.com/microsoft/component-detection",
                            },
                        },
                    },
                },
            },
            Components = scanResult.ComponentsFound.ToComponents(),
        };

        private static List<Component> ToComponents(this IEnumerable<ScannedComponent> scannedComponents) =>
            scannedComponents.Select(sc => sc.ToComponent()).ToList();

        private static Component ToComponent(this ScannedComponent scannedComponent)
        {
            var component = new Component
            {
                Type = Component.Classification.Library,
                Name = scannedComponent.Component.PackageUrl.Name,
                Version = scannedComponent.Component.PackageUrl.Version,
                Purl = scannedComponent.Component.PackageUrl.ToString(),
                Properties = scannedComponent.GenerateProperties(),
            };

            switch (scannedComponent.Component.Type)
            {
                case ComponentType.Cargo:
                    var cargoComponent = (CargoComponent)scannedComponent.Component;
                    break;
                case ComponentType.Conda:
                    var condaComponent = (CondaComponent)scannedComponent.Component;
                    component.ExternalReferences = new List<ExternalReference>
                    {
                        new ExternalReference
                        {
                            Type = ExternalReference.ExternalReferenceType.Other,
                            Url = condaComponent.Url,
                        },
                    };
                    break;
                case ComponentType.DockerImage:
                    var dockerImageComponent = (DockerImageComponent)scannedComponent.Component;
                    break;
                case ComponentType.Git:
                    var gitComponent = (GitComponent)scannedComponent.Component;
                    component.ExternalReferences = new List<ExternalReference>
                    {
                        new ExternalReference
                        {
                            Type = ExternalReference.ExternalReferenceType.Vcs,
                            Url = gitComponent.RepositoryUrl.ToString(),
                        },
                    };
                    break;
                case ComponentType.Go:
                    var goComponent = (GoComponent)scannedComponent.Component;
                    component.Hashes = new List<Hash>
                    {
                        new Hash
                        {
                            Alg = Hash.HashAlgorithm.SHA_256,
                            Content = goComponent.Hash,
                        },
                    };
                    break;
                case ComponentType.Linux:
                    var linuxComponent = (LinuxComponent)scannedComponent.Component;
                    component.Properties.AddRange(
                        new List<Property>
                        {
                            new Property
                            {
                                Name = "distribution", Value = linuxComponent.Distribution,
                            },
                            new Property
                            {
                                Name = "release", Value = linuxComponent.Release,
                            },
                        });
                    break;
                case ComponentType.Maven:
                    var mavenComponent = (MavenComponent)scannedComponent.Component;
                    break;
                case ComponentType.Npm:
                    var npmComponent = (NpmComponent)scannedComponent.Component;
                    if (npmComponent.Author?.Name != null || npmComponent.Author?.Email != null)
                    {
                        component.Author = $"{npmComponent.Author?.Name} <{npmComponent.Author?.Email}>";
                    }

                    if (npmComponent.Hash != null)
                    {
                        component.Hashes = new List<Hash>
                        {
                            new Hash
                            {
                                Alg = Hash.HashAlgorithm.Null, // algorithm is included in hash
                                Content = npmComponent.Hash,
                            },
                        };
                    }

                    break;
                case ComponentType.NuGet:
                    component.Author = string.Join(",", ((NuGetComponent)scannedComponent.Component).Authors);
                    break;
                case ComponentType.Other:
                    var otherComponent = (OtherComponent)scannedComponent.Component;
                    component.Hashes = new List<Hash>
                    {
                        new Hash
                        {
                            Alg = Hash.HashAlgorithm.Null,
                            Content = otherComponent.Hash,
                        },
                    };
                    component.ExternalReferences = new List<ExternalReference>
                    {
                        new ExternalReference
                        {
                            Type = ExternalReference.ExternalReferenceType.Distribution,
                            Url = otherComponent.DownloadUrl.ToString(),
                        },
                    };
                    break;
                case ComponentType.Pip:
                    var pipComponent = (PipComponent)scannedComponent.Component;
                    break;
                case ComponentType.Pod:
                    var podComponent = (PodComponent)scannedComponent.Component;
                    component.ExternalReferences = new List<ExternalReference>
                    {
                        new ExternalReference
                        {
                            Type = ExternalReference.ExternalReferenceType.Vcs,
                            Url = podComponent.SpecRepo,
                        },
                    };
                    break;
                case ComponentType.RubyGems:
                    var rubyGemsComponent = (RubyGemsComponent)scannedComponent.Component;
                    component.ExternalReferences = new List<ExternalReference>
                    {
                        new ExternalReference
                        {
                            Type = ExternalReference.ExternalReferenceType.Vcs,
                            Url = rubyGemsComponent.Source,
                        },
                    };
                    break;
                case ComponentType.Spdx:
                    var spdxComponent = (SpdxComponent)scannedComponent.Component;
                    break;
                case ComponentType.Vcpkg:
                    var vcpkgComponent = (VcpkgComponent)scannedComponent.Component;
                    component.Description = vcpkgComponent.Description;
                    component.ExternalReferences = new List<ExternalReference>
                    {
                        new ExternalReference
                        {
                            Type = ExternalReference.ExternalReferenceType.Distribution,
                            Url = vcpkgComponent.DownloadLocation,
                        },
                    };
                    break;
            }

            return component;
        }

        private static List<Property> GenerateProperties(this ScannedComponent scannedComponent)
        {
            var properties = new List<Property>();
            properties.AddRange(scannedComponent.LocationsFoundAt.Select((locationFoundAt, i) => new Property
            {
                Name = $"component-detection:location:{i}",
                Value = locationFoundAt,
            }));
            return properties;
        }
    }
}
