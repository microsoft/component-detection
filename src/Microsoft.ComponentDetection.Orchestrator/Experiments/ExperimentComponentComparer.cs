namespace Microsoft.ComponentDetection.Orchestrator.Experiments;

using System.Collections.Generic;
using Microsoft.ComponentDetection.Orchestrator.Experiments.Models;

/// <summary>
/// Compares experiment components by their ID.
/// </summary>
public class ExperimentComponentComparer : IEqualityComparer<ExperimentComponent>
{
    /// <inheritdoc />
    public bool Equals(ExperimentComponent x, ExperimentComponent y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null)
        {
            return false;
        }

        if (y is null)
        {
            return false;
        }

        if (x.GetType() != y.GetType())
        {
            return false;
        }

        return x.Id == y.Id;
    }

    public int GetHashCode(ExperimentComponent obj) => obj.Id != null ? obj.Id.GetHashCode() : 0;
}
