using System;
using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.OrchestratorNS.Exceptions
{
    [Serializable]
    public class InvalidDetectorCategoriesException : Exception
    {
        public InvalidDetectorCategoriesException()
        {
        }

        public InvalidDetectorCategoriesException(string message)
            : base(message)
        {
        }

        public InvalidDetectorCategoriesException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected InvalidDetectorCategoriesException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
