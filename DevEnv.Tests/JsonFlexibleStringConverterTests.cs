using System.Text.Json;
using System.Text.Json.Serialization;
using DevEnv.Core.LavaEnv.Models;

namespace DevEnv.Tests;

public sealed class JsonFlexibleStringConverterTests
{
    private sealed class Holder
    {
        [JsonPropertyName("version")]
        [JsonConverter(typeof(JsonFlexibleStringConverter))]
        public string? Version { get; set; }

        [JsonPropertyName("enabled")]
        [JsonConverter(typeof(JsonFlexibleStringConverter))]
        public string? Enabled { get; set; }
    }

    private static Holder Read(string json) =>
        JsonSerializer.Deserialize<Holder>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Fact]
    public void Read_Number_KeepsIntegralForm()
    {
        Assert.Equal("2024", Read("""{"version":2024}""").Version);
    }

    [Fact]
    public void Read_FractionalNumber_UsesInvariantDecimalPoint()
    {
        using var _ = new InvariantCultureScope();

        Assert.Equal("1.5", Read("""{"version":1.5}""").Version);
    }

    [Fact]
    public void Read_Boolean_BecomesLowercaseText()
    {
        var holder = Read("""{"enabled":true,"version":false}""");

        Assert.Equal("true", holder.Enabled);
        Assert.Equal("false", holder.Version);
    }

    [Fact]
    public void Read_Null_BecomesNull()
    {
        Assert.Null(Read("""{"version":null}""").Version);
    }

    [Fact]
    public void Read_String_PassesThrough()
    {
        Assert.Equal("v1.2.3", Read("""{"version":"v1.2.3"}""").Version);
    }

    [Fact]
    public void Write_EmitsJsonString()
    {
        var json = JsonSerializer.Serialize(new Holder { Version = "1.2.3", Enabled = "true" });

        Assert.Contains("\"version\":\"1.2.3\"", json);
        Assert.Contains("\"enabled\":\"true\"", json);
    }
}