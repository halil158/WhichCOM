using System.Text.Json;
using System.Text.Json.Serialization;
using WhichCOM.Core.Settings;

namespace WhichCOM.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ChipTable))]
[JsonSerializable(typeof(WhichComSettings))]
[JsonSerializable(typeof(List<SerialPortInfo>))]
public sealed partial class WhichComJsonContext : JsonSerializerContext;
