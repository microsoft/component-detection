﻿namespace Microsoft.ComponentDetection.Orchestrator.Services.GraphTranslation;
using System.ComponentModel;

public class GraphTranslationServiceMetadata
{
    /// <summary>
    /// Gets the priority level for the exported service.
    /// This allows the importer of the graph translation service to pick the most preferred service.
    /// </summary>
    [DefaultValue(0)]
    public int Priority { get; }
}
