namespace Microsoft.ComponentDetection.Orchestrator.Extensions;

using System;
using System.ComponentModel;
using System.Globalization;

/// <summary>
/// Converts a semicolon separated string to an array of strings.
/// </summary>
public class SemicolonDelimitedConverter : TypeConverter
{
    /// <inheritdoc />
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        if (value is string str)
        {
            return str.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return base.ConvertFrom(context, culture, value);
    }
}
