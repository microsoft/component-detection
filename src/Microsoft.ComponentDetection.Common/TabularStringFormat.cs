#nullable disable
namespace Microsoft.ComponentDetection.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class TabularStringFormat
{
    public const char DefaultVerticalLineChar = '|';
    public const char DefaultHorizontalLineChar = '_';

    private readonly IList<Column> columns;
    private readonly char horizontalLineChar;
    private readonly char verticalLineChar;
    private readonly string tableTitle;

    public TabularStringFormat(IList<Column> columns, char horizontalLineChar = DefaultHorizontalLineChar, char verticalLineChar = DefaultVerticalLineChar, string tableTitle = null)
    {
        this.columns = columns;
        this.horizontalLineChar = horizontalLineChar;
        this.verticalLineChar = verticalLineChar;
        this.tableTitle = tableTitle;
    }

    public string GenerateString(IEnumerable<IList<object>> rows)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(this.tableTitle))
        {
            this.PrintTitleSection(sb);
        }
        else
        {
            this.WriteFlatLine(sb, false);
        }

        sb.Append(this.verticalLineChar);
        foreach (var column in this.columns)
        {
            sb.Append(column.Header.PadRight(column.Width));
            sb.Append(this.verticalLineChar);
        }

        this.WriteFlatLine(sb);
        foreach (var row in rows)
        {
            sb.Append(this.verticalLineChar);
            if (row.Count != this.columns.Count)
            {
                throw new InvalidOperationException("All rows must have length equal to the number of columns present.");
            }

            for (var i = 0; i < this.columns.Count; i++)
            {
                var dataString = this.columns[i].Format != null ? string.Format(this.columns[i].Format, row[i]) : row[i].ToString();
                sb.Append(dataString.PadRight(this.columns[i].Width));
                sb.Append(this.verticalLineChar);
            }

            this.WriteFlatLine(sb);
        }

        return sb.ToString();
    }

    private void PrintTitleSection(StringBuilder sb)
    {
        this.WriteFlatLine(sb, false);
        var tableWidth = this.columns.Sum(column => column.Width);
        sb.Append(this.verticalLineChar);
        sb.Append(this.tableTitle.PadRight(tableWidth + this.columns.Count - 1));
        sb.Append(this.verticalLineChar);

        sb.AppendLine();
        sb.Append(this.verticalLineChar);
        for (var i = 0; i < this.columns.Count - 1; i++)
        {
            sb.Append(string.Empty.PadRight(this.columns[i].Width, this.horizontalLineChar));
            sb.Append(this.horizontalLineChar);
        }

        sb.Append(string.Empty.PadRight(this.columns[this.columns.Count - 1].Width, this.horizontalLineChar));
        sb.Append(this.verticalLineChar);
        sb.AppendLine();
    }

    private void WriteFlatLine(StringBuilder sb, bool withPipes = true)
    {
        var splitCharacter = withPipes ? this.verticalLineChar : this.horizontalLineChar;
        sb.AppendLine();
        sb.Append(splitCharacter);
        for (var i = 0; i < this.columns.Count; i++)
        {
            sb.Append(string.Empty.PadRight(this.columns[i].Width, this.horizontalLineChar));
            sb.Append(splitCharacter);
        }

        sb.AppendLine();
    }
}
