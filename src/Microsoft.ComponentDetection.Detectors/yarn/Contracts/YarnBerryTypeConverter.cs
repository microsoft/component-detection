#nullable disable
namespace Microsoft.ComponentDetection.Detectors.Yarn.Contracts;

using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

/// <inheritdoc />
public class YarnBerryTypeConverter : IYamlTypeConverter
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    /// <inheritdoc />
    public bool Accepts(Type type) => type == typeof(YarnBerryLockfile);

    /// <inheritdoc />
    public object ReadYaml(IParser parser, Type type)
    {
        var metadata = new YarnBerryLockfileMetadata();
        var entries = new Dictionary<string, YarnBerryLockfileEntry>();

        if (parser.Current is MappingStart)
        {
            while (parser.Current is not DocumentEnd)
            {
                switch (parser.Current)
                {
                    case Scalar { Value: "__metadata" }:
                        parser.MoveNext();
                        metadata = Deserializer.Deserialize<YarnBerryLockfileMetadata>(parser);
                        break;
                    case Scalar scalar:
                        var key = scalar.Value;
                        parser.MoveNext();
                        var entry = Deserializer.Deserialize<YarnBerryLockfileEntry>(parser);
                        entries.Add(key, entry);
                        break;
                    default:
                        parser.MoveNext();
                        break;
                }
            }
        }

        return new YarnBerryLockfile
        {
            Metadata = metadata,
            Entries = entries,
        };
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object value, Type type) => throw new NotImplementedException();
}
