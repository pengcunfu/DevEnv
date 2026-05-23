using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevEnv.Models
{
    /// <summary>允许 JSON 中的数字/布尔值自动转为字符串，避免版本号写成 2024.1 导致反序列化失败。</summary>
    public class JsonFlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var integer)
                    ? integer.ToString()
                    : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => throw new JsonException($"无法将 {reader.TokenType} 转换为字符串")
            };
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
