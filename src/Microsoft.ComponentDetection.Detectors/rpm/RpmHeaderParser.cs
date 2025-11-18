namespace Microsoft.ComponentDetection.Detectors.Rpm;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Parses RPM package headers from binary BLOB data.
/// </summary>
internal static class RpmHeaderParser
{
    // RPM tag constants - https://github.com/rpm-software-management/rpm/blob/master/lib/header.cc
    private const int RPMTAG_NAME = 1000;
    private const int RPMTAG_VERSION = 1001;
    private const int RPMTAG_RELEASE = 1002;
    private const int RPMTAG_EPOCH = 1003;
    private const int RPMTAG_ARCH = 1022;
    private const int RPMTAG_SOURCERPM = 1044;
    private const int RPMTAG_PROVIDENAME = 1047;
    private const int RPMTAG_REQUIRENAME = 1049;
    private const int RPMTAG_VENDOR = 1011;

    // RPM Type constants
    private const int RPM_STRING_TYPE = 6;
    private const int RPM_INT32_TYPE = 4;
    private const int RPM_STRING_ARRAY_TYPE = 8;

    /// <summary>
    /// Parses an RPM header BLOB and extracts package information.
    /// </summary>
    /// <param name="headerBlob">The binary RPM header data.</param>
    /// <returns>Package information extracted from the header.</returns>
    public static RpmPackageInfo ParseHeader(ReadOnlySpan<byte> headerBlob)
    {
        if (headerBlob.Length < 8)
        {
            throw new ArgumentException("Invalid RPM header: too short", nameof(headerBlob));
        }

        // SQLite format: starts directly with index count and store size
        var indexCount = BinaryPrimitives.ReadInt32BigEndian(headerBlob[..4]);
        var storeSize = BinaryPrimitives.ReadInt32BigEndian(headerBlob[4..8]);

        // Calculate where the data store starts (8 bytes header + 16 bytes per index entry)
        var dataStoreOffset = 8 + (indexCount * 16);
        if (dataStoreOffset + storeSize > headerBlob.Length)
        {
            throw new ArgumentException(
                "Invalid RPM header: data store extends beyond buffer",
                nameof(headerBlob)
            );
        }

        var dataStore = headerBlob.Slice(dataStoreOffset, storeSize);

        // Read index entries
        var indexEntries =
            indexCount <= 64 ? stackalloc IndexEntry[indexCount] : new IndexEntry[indexCount];

        var indexSpan = headerBlob.Slice(8, indexCount * 16);
        for (var i = 0; i < indexCount; i++)
        {
            var entryOffset = i * 16;
            indexEntries[i] = new IndexEntry
            {
                Tag = BinaryPrimitives.ReadInt32BigEndian(indexSpan.Slice(entryOffset, 4)),
                Type = BinaryPrimitives.ReadInt32BigEndian(indexSpan.Slice(entryOffset + 4, 4)),
                Offset = BinaryPrimitives.ReadInt32BigEndian(indexSpan.Slice(entryOffset + 8, 4)),
                Count = BinaryPrimitives.ReadInt32BigEndian(indexSpan.Slice(entryOffset + 12, 4)),
            };
        }

        // Parse package info from index entries
        return ParsePackageInfo(indexEntries, dataStore);
    }

    private static RpmPackageInfo ParsePackageInfo(
        ReadOnlySpan<IndexEntry> indexEntries,
        ReadOnlySpan<byte> dataStore
    )
    {
        var packageInfo = new RpmPackageInfo();

        foreach (var entry in indexEntries)
        {
            try
            {
                var data = ExtractData(entry, dataStore);

                switch (entry.Tag)
                {
                    case RPMTAG_NAME:
                        if (entry.Type == RPM_STRING_TYPE && data != null)
                        {
                            packageInfo.Name = ParseString(data);
                        }

                        break;

                    case RPMTAG_VERSION:
                        if (entry.Type == RPM_STRING_TYPE && data != null)
                        {
                            packageInfo.Version = ParseString(data);
                        }

                        break;

                    case RPMTAG_RELEASE:
                        if (entry.Type == RPM_STRING_TYPE && data != null)
                        {
                            packageInfo.Release = ParseString(data);
                        }

                        break;

                    case RPMTAG_EPOCH:
                        if (entry.Type == RPM_INT32_TYPE && data != null && data.Length >= 4)
                        {
                            packageInfo.Epoch = BinaryPrimitives.ReadInt32BigEndian(data);
                        }

                        break;

                    case RPMTAG_ARCH:
                        if (entry.Type == RPM_STRING_TYPE && data != null)
                        {
                            packageInfo.Arch = ParseString(data);
                        }

                        break;

                    case RPMTAG_SOURCERPM:
                        if (entry.Type == RPM_STRING_TYPE && data != null)
                        {
                            var sourceRpm = ParseString(data);
                            packageInfo.SourceRpm = sourceRpm == "(none)" ? null : sourceRpm;
                        }

                        break;

                    case RPMTAG_VENDOR:
                        if (entry.Type == RPM_STRING_TYPE && data != null)
                        {
                            var vendor = ParseString(data);
                            packageInfo.Vendor = vendor == "(none)" ? null : vendor;
                        }

                        break;

                    case RPMTAG_PROVIDENAME:
                        if (entry.Type == RPM_STRING_ARRAY_TYPE)
                        {
                            packageInfo.Provides = ParseStringArray(data);
                        }

                        break;

                    case RPMTAG_REQUIRENAME:
                        if (entry.Type == RPM_STRING_ARRAY_TYPE)
                        {
                            packageInfo.Requires = ParseStringArray(data);
                        }

                        break;
                }
            }
            catch (Exception)
            {
                // Skip malformed entries
                continue;
            }
        }

        return packageInfo;
    }

    private static ReadOnlySpan<byte> ExtractData(IndexEntry entry, ReadOnlySpan<byte> dataStore)
    {
        if (entry.Offset < 0 || entry.Offset >= dataStore.Length)
        {
            return [];
        }

        // Calculate data length based on type
        int dataLength;
        switch (entry.Type)
        {
            case RPM_STRING_TYPE:
                // Find null terminator
                dataLength = 0;
                for (var i = entry.Offset; i < dataStore.Length && dataStore[i] != 0; i++)
                {
                    dataLength++;
                }

                dataLength++; // Include null terminator
                break;

            case RPM_INT32_TYPE:
                dataLength = 4 * entry.Count;
                break;

            case RPM_STRING_ARRAY_TYPE:
                // Find the end of the string array (double null or end of data)
                dataLength = 0;
                var consecutiveNulls = 0;
                for (var i = entry.Offset; i < dataStore.Length; i++)
                {
                    dataLength++;
                    if (dataStore[i] == 0)
                    {
                        consecutiveNulls++;
                        if (consecutiveNulls >= 2)
                        {
                            break;
                        }
                    }
                    else
                    {
                        consecutiveNulls = 0;
                    }
                }

                break;

            default:
                // Unknown type, try to read count bytes
                dataLength = entry.Count;
                break;
        }

        if (entry.Offset + dataLength > dataStore.Length)
        {
            dataLength = dataStore.Length - entry.Offset;
        }

        return dataStore.Slice(entry.Offset, dataLength);
    }

    private static string ParseString(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return string.Empty;
        }

        // Find null terminator
        var length = data.IndexOf((byte)0);
        if (length < 0)
        {
            length = data.Length;
        }

        return Encoding.UTF8.GetString(data[..length]);
    }

    private static List<string> ParseStringArray(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return [];
        }

        var result = new List<string>();
        var start = 0;

        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0)
            {
                if (i > start)
                {
                    var str = Encoding.UTF8.GetString(data[start..i]);
                    if (!string.IsNullOrEmpty(str))
                    {
                        result.Add(str);
                    }
                }

                start = i + 1;
            }
        }

        return result;
    }

    private struct IndexEntry
    {
        public int Tag { get; set; }

        public int Type { get; set; }

        public int Offset { get; set; }

        public int Count { get; set; }
    }
}
