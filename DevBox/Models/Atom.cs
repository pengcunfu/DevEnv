using System.ComponentModel;

namespace ProgramBox.Models
{
    /// <summary>
    /// 应用程序原子基类
    /// </summary>
    public class Atom : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _iconPath = string.Empty;
        private string _description = string.Empty;

        /// <summary>
        /// 应用名称
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        /// <summary>
        /// 图标路径
        /// </summary>
        public string IconPath
        {
            get => _iconPath;
            set
            {
                _iconPath = value;
                OnPropertyChanged(nameof(IconPath));
            }
        }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag { get; set; } = string.Empty;

        public Atom() { }

        public Atom(string name, string iconPath, string tag, string description = "")
        {
            Name = name;
            IconPath = iconPath;
            Tag = tag;
            Description = description;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Web应用原子类
    /// </summary>
    public class WebAtom : Atom
    {
        private string _url = string.Empty;

        /// <summary>
        /// 网址
        /// </summary>
        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                OnPropertyChanged(nameof(Url));
            }
        }

        public WebAtom() { }

        public WebAtom(string name, string iconPath, string tag, string url, string description = "")
            : base(name, iconPath, tag, description)
        {
            Url = url;
        }
    }

    /// <summary>
    /// 本地应用原子类
    /// </summary>
    public class NativeAtom : Atom
    {
        private string _execPath = string.Empty;

        /// <summary>
        /// 执行路径
        /// </summary>
        public string ExecPath
        {
            get => _execPath;
            set
            {
                _execPath = value;
                OnPropertyChanged(nameof(ExecPath));
            }
        }

        public NativeAtom() { }

        public NativeAtom(string name, string iconPath, string tag, string execPath, string description = "")
            : base(name, iconPath, tag, description)
        {
            ExecPath = execPath;
        }

        public NativeAtom(string name, string execPath)
            : base(name, execPath, name)
        {
            ExecPath = execPath;
        }
    }

    /// <summary>
    /// 文件夹快捷方式（显示名为别名，不修改磁盘上的文件夹名称）
    /// </summary>
    public class FolderAtom : Atom
    {
        private string _folderPath = string.Empty;

        /// <summary>
        /// 文件夹实际路径
        /// </summary>
        public string FolderPath
        {
            get => _folderPath;
            set
            {
                _folderPath = value;
                OnPropertyChanged(nameof(FolderPath));
            }
        }

        public FolderAtom() { }

        public FolderAtom(string alias, string iconPath, string tag, string folderPath, string description = "")
            : base(alias, iconPath, tag, description)
        {
            FolderPath = folderPath;
        }
    }

    /// <summary>
    /// 内置工具原子类
    /// </summary>
    public class InsAtom : Atom
    {
        private string _key = string.Empty;

        /// <summary>
        /// 键值
        /// </summary>
        public string Key
        {
            get => _key;
            set
            {
                _key = value;
                OnPropertyChanged(nameof(Key));
            }
        }

        public InsAtom() { }

        public InsAtom(string name, string iconPath, string tag, string key, string description = "")
            : base(name, iconPath, tag, description)
        {
            Key = key;
        }

        public InsAtom(string tag, string name, string description = "")
            : base(name, $"/Assets/Icons/{tag}.png", tag, description)
        {
            Key = tag;
        }
    }

    /// <summary>
    /// Windows工具原子类
    /// </summary>
    public class WinToolAtom : InsAtom
    {
        public WinToolAtom() { }

        public WinToolAtom(string name, string iconPath, string tag, string key, string description = "")
            : base(name, iconPath, tag, key, description)
        {
        }

        public WinToolAtom(string tag, string name, string description = "")
            : base(name, $"/Assets/Icons/win/{tag}.png", tag, description)
        {
            Key = tag;
        }
    }
}
