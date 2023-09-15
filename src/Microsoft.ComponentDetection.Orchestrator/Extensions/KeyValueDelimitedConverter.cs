namespace Microsoft.ComponentDetection.Orchestrator.Extensions;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

/// <summary>
/// Converts a comma separated string of key value pairs to a dictionary.
/// </summary>
public class KeyValueDelimitedConverter : TypeConverter
{
    /// <inheritdoc />
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is string str)
        {
            var result = new Dictionary<string, string>();
            var pairs = str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (keyValue.Length != 2)
                {
                    throw new FormatException($"Invalid key value pair: {pair}");
                }

                result.Add(keyValue[0], keyValue[1]);
            }

            return result;
        }

        return base.ConvertFrom(context, culture, value);
    }
}
