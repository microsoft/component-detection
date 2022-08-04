using System;
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
            fileLines = parsedFileLines;

            if (fileLines.Count > 0)
            {
                ReadVersionHeader();
            }
            else
            {
                VersionHeader = string.Empty;
                YarnLockVersion = YarnLockVersion.Invalid;
            }
        }

        public static async Task<YarnBlockFile> CreateBlockFileAsync(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            List<string> fileLines = new List<string>();
            using (StreamReader reader = new StreamReader(stream))
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
            while (ReadToNextMajorBlock())
            {
                yield return ParseBlock();
            }

            yield break;
        }

        private void ReadVersionHeader()
        {
            YarnLockVersion = YarnLockVersion.Invalid;

            do
            {
                if (fileLines[fileLineIndex].StartsWith("#"))
                {
                    if (fileLines[fileLineIndex].Contains("yarn lockfile"))
                    {
                        YarnLockVersion = YarnLockVersion.V1;
                        VersionHeader = fileLines[fileLineIndex];
                        break;
                    }
                }
                else if (string.IsNullOrEmpty(fileLines[fileLineIndex]))
                {
                    // If the comment header does not specify V1, a V2 metadata block will follow a line break
                    if (IncrementIndex())
                    {
                        if (fileLines[fileLineIndex].StartsWith("__metadata:"))
                        {
                            VersionHeader = fileLines[fileLineIndex];
                            YarnLockVersion = YarnLockVersion.V2;

                            YarnBlock metadataBlock = ParseBlock();

                            if (metadataBlock.Values.ContainsKey("version") && metadataBlock.Values.ContainsKey("cacheKey"))
                            {
                                break;
                            }

                            VersionHeader = null;
                            YarnLockVersion = YarnLockVersion.Invalid;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            while (IncrementIndex());
        }

        /// <summary>
        /// Parses a block and its sub-blocks into <see cref="YarnBlock"/>.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        private YarnBlock ParseBlock(int level = 0)
        {
            string currentLevelDelimiter = "  ";
            for (int i = 0; i < level; i++)
            {
                currentLevelDelimiter = currentLevelDelimiter + "  ";
            }

            // Assuming the pointer has been set up to a block
            YarnBlock block = new YarnBlock { Title = fileLines[fileLineIndex].TrimEnd(':').Trim('\"').Trim() };

            while (IncrementIndex())
            {
                if (!fileLines[fileLineIndex].StartsWith(currentLevelDelimiter) || string.IsNullOrWhiteSpace(fileLines[fileLineIndex]))
                {
                    break;
                }

                if (fileLines[fileLineIndex].EndsWith(":"))
                {
                    block.Children.Add(ParseBlock(level + 1));
                    fileLineIndex--;
                }
                else
                {
                    string toParse = fileLines[fileLineIndex].Trim();

                    // Yarn V1 and V2 have slightly different formats, where V2 adds a : between property name and value
                    // Match on the specified version
                    var matches = YarnLockVersion == YarnLockVersion.V1 ? YarnV1Regex.Match(toParse) : YarnV2Regex.Match(toParse);

                    if (matches.Groups.Count != 3) // Whole group + two captures
                    {
                        continue;
                    }

                    block.Values.Add(matches.Groups[1].Value.Trim('\"'), matches.Groups[2].Value.Trim('\"'));
                }

                if (!Peek() || !fileLines[fileLineIndex].StartsWith(currentLevelDelimiter) || string.IsNullOrWhiteSpace(fileLines[fileLineIndex]))
                {
                    break;
                }
            }

            return block;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
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
                if (!IncrementIndex())
                {
                    return false;
                }
                else
                {
                    line = fileLines[fileLineIndex];
                }
            }
            while (string.IsNullOrWhiteSpace(line) || line.StartsWith(" ") || line.StartsWith("\t") || line.StartsWith("#"));

            return true;
        }

        private bool IncrementIndex()
        {
            fileLineIndex++;

            return Peek();
        }

        /// <summary>
        /// Checks to see if any lines are left in the file contents.
        /// </summary>
        /// <returns></returns>
        private bool Peek()
        {
            if (fileLineIndex >= fileLines.Count)
            {
                return false;
            }

            return true;
        }
    }
}
