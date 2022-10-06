﻿using System;

namespace Microsoft.ComponentDetection.OrchestratorNS
{
    public class DetectorRunResult
    {
        public TimeSpan ExecutionTime { get; set; }

        public int ComponentsFoundCount { get; set; }

        public int ExplicitlyReferencedComponentCount { get; set; }

        public bool IsExperimental { get; set; }
    }
}
