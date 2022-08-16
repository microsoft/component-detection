﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.ComponentDetection.Detectors.Yarn.Parsers
{
    /// <summary>
    /// https://github.com/yarnpkg/yarn/issues/5629
    ///
    /// Yarn uses something that is "Almost-YAML", and we haven't found a YAML parser
    ///  that understands Almost-YAML yet, so here's ours...
    ///
    /// In V1, this represents a file of newline delimited blocks in the form:
    ///
    /// {blockName}:
    ///   {key} "{value}"
    ///   {foo} "{bar}"
    ///   {subBlock}:
    ///     {key} "{value}"
    ///   {baz} "{bar}"
    ///
    /// {otherBlockName}:
    ///   {key} "{value}"
    ///   ...
    ///
    /// In V2, pure YAML is the new standard. This represents a file of newline delimited blocks
    /// in the form where values can optionally be wrapped in quotes:
    /// {blockName}:
    ///   {key}: value
    ///   {foo}: "bar"
    ///   {subBlock}:
    ///     {key}: {value}
    ///   {baz}: {bar}
    ///
    /// {otherBlockName}:
    ///   {key}: "{value}"
    ///   ...
    /// </summary>
    public class YarnBlockFile : IYarnBlockFile
    {
        private static readonly Regex YarnV1Regex = new Regex("(.*)\\s\"(.*)\"", RegexOptions.Compiled);

        private static readonly Regex YarnV2Regex = new Regex("(.*):\\s\"?(.*)", RegexOptions.Compiled);

        private int fileLineIndex = 0;

        private readonly IList<string> fileLines = new List<string>();

        public string VersionHeader { get; set; }

        public YarnLockVersion YarnLockVersion { get; set; }

        private YarnBlockFile(IList<string> parsedFileLines)
        {
            this.fileLines = parsedFileLines;

            if (this.fileLines.Count > 0)
            {
                this.ReadVersionHeader();
            }
            else
            {
                this.VersionHeader = string.Empty;
                this.YarnLockVersion = YarnLockVersion.Invalid;
            }
        }

        public static async Task<YarnBlockFile> CreateBlockFileAsync(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var fileLines = new List<string>();
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    fileLines.Add(await reader.ReadLineAsync());
                }
            }

            return new YarnBlockFile(fileLines);
        }

        public IEnumerator<YarnBlock> GetEnumerator()
        {
            while (this.ReadToNextMajorBlock())
            {
                yield return this.ParseBlock();
            }

            yield break;
        }

        private void ReadVersionHeader()
        {
            this.YarnLockVersion = YarnLockVersion.Invalid;

            do
            {
                if (this.fileLines[this.fileLineIndex].StartsWith("#"))
                {
                    if (this.fileLines[this.fileLineIndex].Contains("yarn lockfile"))
                    {
                        this.YarnLockVersion = YarnLockVersion.V1;
                        this.VersionHeader = this.fileLines[this.fileLineIndex];
                        break;
                    }
                }
                else if (string.IsNullOrEmpty(this.fileLines[this.fileLineIndex]))
                {
                    // If the comment header does not specify V1, a V2 metadata block will follow a line break
                    if (this.IncrementIndex())
                    {
                        if (this.fileLines[this.fileLineIndex].StartsWith("__metadata:"))
                        {
                            this.VersionHeader = this.fileLines[this.fileLineIndex];
                            this.YarnLockVersion = YarnLockVersion.V2;

                            var metadataBlock = this.ParseBlock();

                            if (metadataBlock.Values.ContainsKey("version") && metadataBlock.Values.ContainsKey("cacheKey"))
                            {
                                break;
                            }

                            this.VersionHeader = null;
                            this.YarnLockVersion = YarnLockVersion.Invalid;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            while (this.IncrementIndex());
        }

        /// <summary>
        /// Parses a block and its sub-blocks into <see cref="YarnBlock"/>.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        private YarnBlock ParseBlock(int level = 0)
        {
            var currentLevelDelimiter = "  ";
            for (var i = 0; i < level; i++)
            {
                currentLevelDelimiter = currentLevelDelimiter + "  ";
            }

            // Assuming the pointer has been set up to a block
            var block = new YarnBlock { Title = this.fileLines[this.fileLineIndex].TrimEnd(':').Trim('\"').Trim() };

            while (this.IncrementIndex())
            {
                if (!this.fileLines[this.fileLineIndex].StartsWith(currentLevelDelimiter) || string.IsNullOrWhiteSpace(this.fileLines[this.fileLineIndex]))
                {
                    break;
                }

                if (this.fileLines[this.fileLineIndex].EndsWith(":"))
                {
                    block.Children.Add(this.ParseBlock(level + 1));
                    this.fileLineIndex--;
                }
                else
                {
                    var toParse = this.fileLines[this.fileLineIndex].Trim();

                    // Yarn V1 and V2 have slightly different formats, where V2 adds a : between property name and value
                    // Match on the specified version
                    var matches = this.YarnLockVersion == YarnLockVersion.V1 ? YarnV1Regex.Match(toParse) : YarnV2Regex.Match(toParse);

                    if (matches.Groups.Count != 3) // Whole group + two captures
                    {
                        continue;
                    }

                    block.Values.Add(matches.Groups[1].Value.Trim('\"'), matches.Groups[2].Value.Trim('\"'));
                }

                if (!this.Peek() || !this.fileLines[this.fileLineIndex].StartsWith(currentLevelDelimiter) || string.IsNullOrWhiteSpace(this.fileLines[this.fileLineIndex]))
                {
                    break;
                }
            }

            return block;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Increments the internal pointer so that it is at the next block.
        /// </summary>
        /// <returns></returns>
        private bool ReadToNextMajorBlock()
        {
            string line;
            do
            {
                if (!this.IncrementIndex())
                {
                    return false;
                }
                else
                {
                    line = this.fileLines[this.fileLineIndex];
                }
            }
            while (string.IsNullOrWhiteSpace(line) || line.StartsWith(" ") || line.StartsWith("\t") || line.StartsWith("#"));

            return true;
        }

        private bool IncrementIndex()
        {
            this.fileLineIndex++;

            return this.Peek();
        }

        /// <summary>
        /// Checks to see if any lines are left in the file contents.
        /// </summary>
        /// <returns></returns>
        private bool Peek()
        {
            if (this.fileLineIndex >= this.fileLines.Count)
            {
                return false;
            }

            return true;
        }
    }
}
