#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Detectors.Rust;
using Microsoft.ComponentDetection.Detectors.Rust.Sbom.Contracts;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class RustSbomDetectorTests : BaseDetectorTest<RustSbomDetector>
{
    private readonly string testSbom = /*lang=json,strict*/ @"
{
  ""version"": 1,
  ""root"": 0,
  ""crates"": [
    {
      ""id"": ""path+file:///temp/test-crate#0.1.0"",
      ""features"": [],
      ""dependencies"": [
        {
          ""index"": 1,
          ""kind"": ""normal""
        },
        {
          ""index"": 1,
          ""kind"": ""build""
        },
        {
          ""index"": 2,
          ""kind"": ""normal""
        }
      ]
    },
    {
      ""id"": ""registry+https://github.com/rust-lang/crates.io-index#my_dependency@1.0.0"",
      ""features"": [],
      ""unexpected_new_thing_from_the_future"": ""foo"",
      ""dependencies"": []
    },
    {
      ""id"": ""registry+https://github.com/rust-lang/crates.io-index#other_dependency@0.4.0"",
      ""features"": [],
      ""dependencies"": [
            {
              ""index"": 3,
              ""kind"": ""normal""
            }
        ]
    },
    {
      ""id"": ""registry+https://github.com/rust-lang/crates.io-index#other_dependency_dependency@0.1.12-alpha.6"",
      ""features"": [],
      ""dependencies"": []
    }
  ],
  ""rustc"": {
    ""version"": ""1.84.1"",
    ""wrapper"": null,
    ""workspace_wrapper"": null,
    ""commit_hash"": ""2b00e2aae6389eb20dbb690bce5a28cc50affa53"",
    ""host"": ""x86_64-pc-windows-msvc"",
    ""verbose_version"": ""rustc 1.84.1""
  },
  ""target"": ""x86_64-pc-windows-msvc""
}
";

    private readonly string testSbomWithGitDeps = /*lang=json,strict*/ @"{
    ""version"": 1,
    ""root"": 2,
    ""crates"": [
        {
            ""id"": ""registry+https://github.com/rust-lang/crates.io-index#aho-corasick@1.1.3"",
            ""features"": [
                ""perf-literal"",
                ""std""
            ],
            ""dependencies"": [
                {
                    ""index"": 3,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""path+file:///D:/temp/hello#0.1.0"",
            ""features"": [],
            ""dependencies"": [
                {
                    ""index"": 4,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""path+file:///D:/temp/hello#0.1.0"",
            ""features"": [],
            ""dependencies"": [
                {
                    ""index"": 1,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 4,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""bin""
            ]
        },
        {
            ""id"": ""registry+https://github.com/rust-lang/crates.io-index#memchr@2.7.4"",
            ""features"": [
                ""alloc"",
                ""std""
            ],
            ""dependencies"": [],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""git+https://github.com/rust-lang/regex.git#regex@1.11.1"",
            ""features"": [
            ],
            ""dependencies"": [
                {
                    ""index"": 0,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 3,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 5,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 6,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""git+https://github.com/rust-lang/regex.git#regex-automata@0.4.9"",
            ""features"": [
            ],
            ""dependencies"": [
                {
                    ""index"": 0,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 3,
                    ""kind"": ""normal""
                },
                {
                    ""index"": 6,
                    ""kind"": ""normal""
                }
            ],
            ""kind"": [
                ""lib""
            ]
        },
        {
            ""id"": ""git+https://github.com/rust-lang/regex.git#regex-syntax@0.8.5"",
            ""features"": [
            ],
            ""dependencies"": [],
            ""kind"": [
                ""lib""
            ]
        }
    ],
    ""rustc"": {
        ""version"": ""1.88.0-nightly"",
        ""wrapper"": null,
        ""workspace_wrapper"": null,
        ""commit_hash"": ""6bc57c6bf7d0024ad9ea5a2c112f3fc9c383c8a4"",
        ""host"": ""x86_64-pc-windows-msvc"",
        ""verbose_version"": ""rustc 1.88.0-nightly (6bc57c6bf 2025-04-22)\nbinary: rustc\ncommit-hash: 6bc57c6bf7d0024ad9ea5a2c112f3fc9c383c8a4\ncommit-date: 2025-04-22\nhost: x86_64-pc-windows-msvc\nrelease: 1.88.0-nightly\nLLVM version: 20.1.2\n""
    },
    ""target"": ""x86_64-pc-windows-msvc""
}";

    [TestMethod]
    public async Task TestGraphIsCorrectAsync()
    {
        var sbom = CargoSbom.FromJson(this.testSbom);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("main.exe.cargo-sbom.json", this.testSbom)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(3);

        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

        // Verify explicitly referenced roots
        var rootComponents = new List<string>
        {
            "my_dependency 1.0.0 - Cargo",
            "other_dependency 0.4.0 - Cargo",
        };

        rootComponents.ForEach(rootComponentId => graph.IsComponentExplicitlyReferenced(rootComponentId).Should().BeTrue());

        // Verify dependencies for my_dependency
        graph.GetDependenciesForComponent("my_dependency 1.0.0 - Cargo").Should().BeEmpty();

        // Verify dependencies for other_dependency
        var other_dependencyDependencies = new List<string>
        {
            "other_dependency_dependency 0.1.12-alpha.6 - Cargo",
        };

        graph.GetDependenciesForComponent("other_dependency 0.4.0 - Cargo").Should().BeEquivalentTo(other_dependencyDependencies);
    }

    [TestMethod]
    public async Task TestGraphIsCorrectWithGitDeps()
    {
        var sbom = CargoSbom.FromJson(this.testSbomWithGitDeps);

        var (result, componentRecorder) = await this.DetectorTestUtility
            .WithFile("main.exe.cargo-sbom.json", this.testSbomWithGitDeps)
            .ExecuteDetectorAsync();

        result.ResultCode.Should().Be(ProcessingResultCode.Success);
        componentRecorder.GetDetectedComponents().Should().HaveCount(2);

        var graph = componentRecorder.GetDependencyGraphsByLocation().Values.First(); // There should only be 1

        // Verify dependencies
        graph.GetDependenciesForComponent("aho-corasick 1.1.3 - Cargo").Should().BeEquivalentTo("memchr 2.7.4 - Cargo");
    }
}
