using System.IO;
using System.Text.Json;
using DevBox.Core.LavaEnv.Models;

namespace DevBox.Core.LavaEnv.Services
{
  public static class ResourceLoader
  {
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      PropertyNameCaseInsensitive = true,
      ReadCommentHandling = JsonCommentHandling.Skip,
      AllowTrailingCommas = true
    };

    public static T? LoadBundledJson<T>(string fileName)
    {
      var path = ResourcePaths.GetBundledResourcePath(fileName);
      if (!File.Exists(path))
        return default;

      var json = File.ReadAllText(path);
      return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
  }
}
