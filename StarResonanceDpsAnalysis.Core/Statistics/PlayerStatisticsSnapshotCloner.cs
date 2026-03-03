using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StarResonanceDpsAnalysis.Core.Statistics;

/// <summary>
/// Deep-clones PlayerStatistics into a detached snapshot for UI/history use.
/// The clone is no longer affected by later live section reset/clear.
/// </summary>
public static class PlayerStatisticsSnapshotCloner
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    public static PlayerStatistics Clone(PlayerStatistics source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var json = JsonConvert.SerializeObject(source, JsonSettings);
        var clone = JsonConvert.DeserializeObject<PlayerStatistics>(json, JsonSettings);

        if (clone == null)
        {
            throw new InvalidOperationException("Failed to clone PlayerStatistics.");
        }

        return clone;
    }

    private sealed class PrivateSetterContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (!property.Writable)
            {
                if (member is System.Reflection.PropertyInfo propertyInfo &&
                    propertyInfo.GetSetMethod(true) != null)
                {
                    property.Writable = true;
                }
            }

            return property;
        }
    }
}