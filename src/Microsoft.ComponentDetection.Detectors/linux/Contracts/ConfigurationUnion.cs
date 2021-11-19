using System.Collections.Generic;

namespace Microsoft.ComponentDetection.Detectors.Linux.Contracts
{
    public struct ConfigurationUnion
    {
        public object[] AnythingArray;
        public Dictionary<string, object> AnythingMap;
        public bool? Bool;
        public double? Double;
        public long? Integer;
        public string String;

        public static implicit operator ConfigurationUnion(object[] anythingArray)
        {
            return new ConfigurationUnion { AnythingArray = anythingArray };
        }

        public static implicit operator ConfigurationUnion(Dictionary<string, object> anythingMap)
        {
            return new ConfigurationUnion { AnythingMap = anythingMap };
        }

        public static implicit operator ConfigurationUnion(bool @bool)
        {
            return new ConfigurationUnion { Bool = @bool };
        }

        public static implicit operator ConfigurationUnion(double @double)
        {
            return new ConfigurationUnion { Double = @double };
        }

        public static implicit operator ConfigurationUnion(long integer)
        {
            return new ConfigurationUnion { Integer = integer };
        }

        public static implicit operator ConfigurationUnion(string @string)
        {
            return new ConfigurationUnion { String = @string };
        }

        public bool IsNull => AnythingArray == null && Bool == null && Double == null && Integer == null && AnythingMap == null &&
                              String == null;
    }
}
