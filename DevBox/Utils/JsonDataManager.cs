using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ProgramBox.Models;

namespace ProgramBox.Utils
{
    /// <summary>
    /// JSON数据管理器
    /// </summary>
    public class JsonDataManager
    {
        private readonly string _jsonPath;

        public JsonDataManager(string? jsonPath = null)
        {
            _jsonPath = jsonPath ?? AppHelper.JsonPath;
            InitializeData();
        }

        /// <summary>
        /// 应用程序数据
        /// </summary>
        public AppData Data { get; private set; } = new();

        /// <summary>
        /// 初始化数据
        /// </summary>
        private void InitializeData()
        {
            if (!File.Exists(_jsonPath))
            {
                CreateDefaultData();
                Save();
                return;
            }

            try
            {
                var json = File.ReadAllText(_jsonPath);
                var data = JsonConvert.DeserializeObject<AppData>(json);
                Data = data ?? new AppData();
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"读取配置文件失败: {ex.Message}");
                CreateDefaultData();
            }
        }

        /// <summary>
        /// 创建默认数据
        /// </summary>
        private void CreateDefaultData()
        {
            Data = new AppData();

            // 添加默认的内置工具
            Data.InsAtomList.Add(new InsAtom("cpp", "C++环境管理", "管理C++编译器和开发工具"));
            Data.InsAtomList.Add(new InsAtom("java", "Java环境管理", "管理Java JDK和相关工具"));
            Data.InsAtomList.Add(new InsAtom("python", "Python环境管理", "管理Python解释器和包管理"));
            Data.InsAtomList.Add(new InsAtom("php", "PHP环境管理", "管理PHP解释器和Composer"));
            Data.InsAtomList.Add(new InsAtom("node", "Node.js环境管理", "管理Node.js和npm包"));
            Data.InsAtomList.Add(new InsAtom("mysql", "MySQL服务管理", "管理MySQL数据库服务"));
            Data.InsAtomList.Add(new InsAtom("redis", "Redis服务管理", "管理Redis缓存服务"));
            Data.InsAtomList.Add(new InsAtom("minio", "MinIO服务管理", "管理MinIO对象存储服务"));
            Data.InsAtomList.Add(new InsAtom("rabbitmq", "RabbitMQ服务管理", "管理RabbitMQ消息队列"));
            Data.InsAtomList.Add(new InsAtom("nginx", "Nginx服务管理", "管理Nginx Web服务器"));

            // 添加默认的Windows工具
            Data.WinToolAtomList.Add(new WinToolAtom("disk", "磁盘管理", "管理磁盘分区和存储"));
            Data.WinToolAtomList.Add(new WinToolAtom("device", "设备管理器", "管理硬件设备和驱动"));
            Data.WinToolAtomList.Add(new WinToolAtom("task", "任务管理器", "查看和管理系统进程"));
            Data.WinToolAtomList.Add(new WinToolAtom("planned_task", "计划任务", "管理系统定时任务"));
            Data.WinToolAtomList.Add(new WinToolAtom("regedit", "注册表", "编辑Windows注册表"));
            Data.WinToolAtomList.Add(new WinToolAtom("env", "环境变量", "管理系统环境变量"));
            Data.WinToolAtomList.Add(new WinToolAtom("res", "资源监视器", "监控系统资源使用"));
            Data.WinToolAtomList.Add(new WinToolAtom("config", "系统配置", "配置系统启动和服务"));
            Data.WinToolAtomList.Add(new WinToolAtom("info", "系统信息", "查看系统详细信息"));
            Data.WinToolAtomList.Add(new WinToolAtom("event", "事件查看器", "查看系统事件日志"));
            Data.WinToolAtomList.Add(new WinToolAtom("firewall", "防火墙", "配置Windows防火墙"));
            Data.WinToolAtomList.Add(new WinToolAtom("server", "服务", "管理Windows系统服务"));
            Data.WinToolAtomList.Add(new WinToolAtom("computer", "计算机管理", "综合系统管理工具"));
            Data.WinToolAtomList.Add(new WinToolAtom("security", "本地安全策略", "配置安全策略"));
            Data.WinToolAtomList.Add(new WinToolAtom("control", "控制面板", "系统设置控制面板"));
        }

        /// <summary>
        /// 保存数据到文件
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_jsonPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(Data, Formatting.Indented);
                File.WriteAllText(_jsonPath, json);
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加Web应用
        /// </summary>
        public void AddWebAtom(WebAtom atom)
        {
            Data.WebAtomList.Add(atom);
            Save();
        }

        /// <summary>
        /// 添加本地应用
        /// </summary>
        public void AddNativeAtom(NativeAtom atom)
        {
            Data.NativeAtomList.Add(atom);
            Save();
        }

        /// <summary>
        /// 移除Web应用
        /// </summary>
        public void RemoveWebAtom(WebAtom atom)
        {
            Data.WebAtomList.Remove(atom);
            Save();
        }

        public WebAtom? FindWebByUrl(string url)
        {
            return Data.WebAtomList.FirstOrDefault(w => WebShortcutHelper.IsSameUrl(w.Url, url));
        }

        /// <summary>
        /// 移除本地应用
        /// </summary>
        public void RemoveNativeAtom(NativeAtom atom)
        {
            Data.NativeAtomList.Remove(atom);
            Save();
        }

        /// <summary>
        /// 按可执行路径查找本地应用
        /// </summary>
        public NativeAtom? FindNativeByExecPath(string execPath)
        {
            return Data.NativeAtomList.FirstOrDefault(a =>
                NativeAppHelper.IsSameExecutable(a.ExecPath, execPath));
        }

        public void AddFolderAtom(FolderAtom atom)
        {
            Data.FolderAtomList.Add(atom);
            Save();
        }

        public void RemoveFolderAtom(FolderAtom atom)
        {
            Data.FolderAtomList.Remove(atom);
            Save();
        }

        public FolderAtom? FindFolderByPath(string folderPath)
        {
            return Data.FolderAtomList.FirstOrDefault(f =>
                IconCacheHelper.IsSamePath(f.FolderPath, folderPath));
        }
    }

    /// <summary>
    /// 应用程序数据类
    /// </summary>
    public class AppData
    {
        [JsonProperty]
        public ObservableCollection<WebAtom> WebAtomList { get; set; } = new();

        [JsonProperty]
        public ObservableCollection<NativeAtom> NativeAtomList { get; set; } = new();

        [JsonProperty]
        public ObservableCollection<FolderAtom> FolderAtomList { get; set; } = new();

        [JsonProperty]
        public ObservableCollection<InsAtom> InsAtomList { get; set; } = new();

        [JsonProperty]
        public ObservableCollection<WinToolAtom> WinToolAtomList { get; set; } = new();

        [JsonProperty]
        public AppConfig Config { get; set; } = new();
    }
}
