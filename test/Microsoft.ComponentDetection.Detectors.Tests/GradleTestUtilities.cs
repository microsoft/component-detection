#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Tests;

using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.Internal;
using Moq;

public static class GradleTestUtilities
{
    public static IComponentStreamEnumerableFactory GetMockComponentStreamEnumerableFactory(IEnumerable<IComponentStream> streams, IEnumerable<string> patterns = null)
    {
        var mock = new Mock<IComponentStreamEnumerableFactory>();
        mock.Setup(x => x.GetComponentStreams(It.IsAny<DirectoryInfo>(), patterns ?? It.IsAny<IEnumerable<string>>(), It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<bool>())).Returns(streams);

        return mock.Object;
    }

    public static IObservableDirectoryWalkerFactory GetDirectoryWalker(IEnumerable<ProcessRequest> processRequests, IEnumerable<string> patterns = null)
    {
        var mock = new Mock<IObservableDirectoryWalkerFactory>();
        mock.Setup(x => x.Initialize(It.IsAny<DirectoryInfo>(), It.IsAny<ExcludeDirectoryPredicate>(), It.IsAny<int>(), It.IsAny<IEnumerable<string>>()));
        mock.Setup(x => x.GetFilteredComponentStreamObservable(It.IsAny<DirectoryInfo>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IComponentRecorder>())).Returns(() => processRequests.ToObservable());

        return mock.Object;
    }
}
