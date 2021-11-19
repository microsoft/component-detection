using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.ComponentDetection.Common
{
    public class TabularStringFormat
    {
        private IList<Column> columns;
        private int totalWidth;
        private char horizontalLineChar;
        private char verticalLineChar;
        private string tableTitle;

        public const char DefaultVerticalLineChar = '|';
        public const char DefaultHorizontalLineChar = '_';

        public TabularStringFormat(IList<Column> columns, char horizontalLineChar = DefaultHorizontalLineChar, char verticalLineChar = DefaultVerticalLineChar, string tableTitle = null)
        {
            this.columns = columns;
            totalWidth = columns.Count + 1 + columns.Sum(x => x.Width);
            this.horizontalLineChar = horizontalLineChar;
            this.verticalLineChar = verticalLineChar;
            this.tableTitle = tableTitle;
        }

        public string GenerateString(IEnumerable<IList<object>> rows)
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(tableTitle))
            {
                PrintTitleSection(sb);
            }
            else
            {
                WriteFlatLine(sb, false);
            }

            sb.Append(verticalLineChar);
            foreach (var column in columns)
            {
                sb.Append(column.Header.PadRight(column.Width));
                sb.Append(verticalLineChar);
            }

            WriteFlatLine(sb);
            foreach (var row in rows)
            {
                sb.Append(verticalLineChar);
                if (row.Count() != columns.Count)
                {
                    throw new InvalidOperationException("All rows must have length equal to the number of columns present.");
                }

                for (var i = 0; i < columns.Count; i++)
                {
                    var dataString = columns[i].Format != null ? string.Format(columns[i].Format, row[i]) : row[i].ToString();
                    sb.Append(dataString.PadRight(columns[i].Width));
                    sb.Append(verticalLineChar);
                }

                WriteFlatLine(sb);
            }

            return sb.ToString();
        }

        private void PrintTitleSection(StringBuilder sb)
        {
            WriteFlatLine(sb, false);
            var tableWidth = columns.Sum(column => column.Width);
            sb.Append(verticalLineChar);
            sb.Append(tableTitle.PadRight(tableWidth + columns.Count() - 1));
            sb.Append(verticalLineChar);

            sb.AppendLine();
            sb.Append(verticalLineChar);
            for (var i = 0; i < columns.Count - 1; i++)
            {
                sb.Append(string.Empty.PadRight(columns[i].Width, horizontalLineChar));
                sb.Append(horizontalLineChar);
            }

            sb.Append(string.Empty.PadRight(columns[columns.Count - 1].Width, horizontalLineChar));
            sb.Append(verticalLineChar);
            sb.AppendLine();
        }

        private void WriteFlatLine(StringBuilder sb, bool withPipes = true)
        {
            var splitCharacter = withPipes ? verticalLineChar : horizontalLineChar;
            sb.AppendLine();
            sb.Append(splitCharacter);
            for (var i = 0; i < columns.Count; i++)
            {
                sb.Append(string.Empty.PadRight(columns[i].Width, horizontalLineChar));
                sb.Append(splitCharacter);
            }

            sb.AppendLine();
        }
    }
}