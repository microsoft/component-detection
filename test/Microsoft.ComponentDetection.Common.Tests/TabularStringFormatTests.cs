using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Common.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class TabularStringFormatTests
    {
        private Column[] columns;
        private TabularStringFormat tsf;
        private object[][] rows;

        [TestInitialize]
        public void TestInitialize()
        {
            columns = new Column[]
            {
                new Column { Header = "ColumnA", Width = 50, Format = null },
                new Column { Header = "ColumnB", Width = 60, Format = "prefix{0}suffix" },
                new Column { Header = "ColumnC", Width = 30, Format = null },
            };

            rows = new[]
            {
                // One row
                new[] { "a", "b", "c" },
            };

            tsf = new TabularStringFormat(columns);
        }

        [TestMethod]
        public void GenerateString_AllRowsObeyHeaderLength()
        {
            var generatedString = tsf.GenerateString(rows);

            // Column width + border characters, one per column + one to 'close' the table.
            var lineLength = columns.Sum(x => x.Width) + columns.Length + 1;
            var splitStrings = generatedString.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in splitStrings)
            {
                line.Should().HaveLength(lineLength);
            }
        }

        [TestMethod]
        public void GenerateString_ColumnHeadersAreWritten()
        {
            var generatedString = tsf.GenerateString(rows);

            var splitStrings = generatedString.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Second row has the headers
            var headerCells = splitStrings[1].Split(new[] { TabularStringFormat.DefaultVerticalLineChar }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < columns.Length; i++)
            {
                headerCells[i]
                    .Should().Contain(columns[i].Header);
            }
        }

        [TestMethod]
        public void GenerateString_RowContentsAreWritten()
        {
            var generatedString = tsf.GenerateString(rows);
            var splitStrings = generatedString.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Fourth row should have some info
            var rowCells = splitStrings[3].Split(new[] { TabularStringFormat.DefaultVerticalLineChar }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < columns.Length; i++)
            {
                rowCells[i]
                    .Should().Contain(rows[0][i].ToString());
            }
        }
    }
}
