using DevEnv.Core.Models;
using Newtonsoft.Json.Linq;

namespace DevEnv.Core.Services;

/// <summary>开发环境服务的配置持久化（data.json 的 DevServiceList 段）。</summary>
public sealed class DevServiceStore
{
    public const string PropertyName = "DevServiceList";

    private readonly DataStore _data;

    public DevServiceStore(DataStore data) => _data = data;

    public IReadOnlyList<DevServiceConfig> GetAll() => Parse(_data.GetList(PropertyName));

    public void SaveAll(IReadOnlyList<DevServiceConfig> services) =>
        _data.SaveList(PropertyName, ToJson(services));

    /// <summary>
    /// 解析配置数组。缺失的 Id 用「item-序号」补齐，保证同一次读取里能按 Id 定位；
    /// 字段缺失按空串处理，交给界面提示补全，而不是让整条配置消失。
    /// </summary>
    public static IReadOnlyList<DevServiceConfig> Parse(JArray array)
    {
        var list = new List<DevServiceConfig>();
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JObject obj)
                continue;

            var id = ReadString(obj, "Id");
            list.Add(new DevServiceConfig
            {
                Id = id.Length > 0 ? id : $"item-{i}",
                Name = ReadString(obj, "Name"),
                WorkingDirectory = ReadString(obj, "WorkingDirectory"),
                StartCommand = ReadString(obj, "StartCommand"),
                StopCommand = ReadString(obj, "StopCommand"),
                ProcessName = ReadString(obj, "ProcessName"),
            });
        }

        return list;
    }

    public static JArray ToJson(IEnumerable<DevServiceConfig> services)
    {
        var array = new JArray();
        foreach (var service in services)
        {
            array.Add(new JObject
            {
                ["Id"] = service.Id,
                ["Name"] = service.Name,
                ["WorkingDirectory"] = service.WorkingDirectory,
                ["StartCommand"] = service.StartCommand,
                ["StopCommand"] = service.StopCommand,
                ["ProcessName"] = service.ProcessName,
            });
        }

        return array;
    }

    private static string ReadString(JObject obj, string name)
    {
        var token = obj[name];
        if (token is null || token.Type is JTokenType.Null or JTokenType.Undefined)
            return string.Empty;
        return (token.Type == JTokenType.String
            ? token.Value<string>() ?? string.Empty
            : token.ToString()).Trim();
    }
}
