using System;
using System.Runtime.Serialization;

namespace Microsoft.ComponentDetection.Orchestrator.Exceptions
{
    [Serializable]
    public class InvalidDetectorFilterException : Exception
    {
        public InvalidDetectorFilterException()
        {
        }

        public InvalidDetectorFilterException(string message)
            : base(message)
        {
        }

        public InvalidDetectorFilterException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected InvalidDetectorFilterException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}