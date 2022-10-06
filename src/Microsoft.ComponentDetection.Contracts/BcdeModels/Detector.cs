﻿using System.Collections.Generic;
using Microsoft.ComponentDetection.Contracts.TypedComponentNS;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Microsoft.ComponentDetection.Contracts.BcdeModels
{
    [JsonObject(MemberSerialization.OptOut, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Detector
    {
        public string DetectorId { get; set; }

        public bool IsExperimental { get; set; }

        public int Version { get; set; }

        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public IEnumerable<ComponentType> SupportedComponentTypes { get; set; }
    }
}
