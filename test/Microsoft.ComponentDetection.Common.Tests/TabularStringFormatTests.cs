﻿using System;
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
            this.columns = new Column[]
            {
                new Column { Header = "ColumnA", Width = 50, Format = null },
                new Column { Header = "ColumnB", Width = 60, Format = "prefix{0}suffix" },
                new Column { Header = "ColumnC", Width = 30, Format = null },
            };

            this.rows = new[]
            {
                // One row
                new[] { "a", "b", "c" },
            };

            this.tsf = new TabularStringFormat(this.columns);
        }

        [TestMethod]
        public void GenerateString_AllRowsObeyHeaderLength()
        {
            var generatedString = this.tsf.GenerateString(this.rows);

            // Column width + border characters, one per column + one to 'close' the table.
            var lineLength = this.columns.Sum(x => x.Width) + this.columns.Length + 1;
            var splitStrings = generatedString.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in splitStrings)
            {
                line.Should().HaveLength(lineLength);
            }
        }

        [TestMethod]
        public void GenerateString_ColumnHeadersAreWritten()
        {
            var generatedString = this.tsf.GenerateString(this.rows);

            var splitStrings = generatedString.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Second row has the headers
            var headerCells = splitStrings[1].Split(new[] { TabularStringFormat.DefaultVerticalLineChar }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < this.columns.Length; i++)
            {
                headerCells[i]
                    .Should().Contain(this.columns[i].Header);
            }
        }

        [TestMethod]
        public void GenerateString_RowContentsAreWritten()
        {
            var generatedString = this.tsf.GenerateString(this.rows);
            var splitStrings = generatedString.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Fourth row should have some info
            var rowCells = splitStrings[3].Split(new[] { TabularStringFormat.DefaultVerticalLineChar }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < this.columns.Length; i++)
            {
                rowCells[i]
                    .Should().Contain(this.rows[0][i].ToString());
            }
        }
    }
}
