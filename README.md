# 熔岩环境管理工具 (LavaEnv)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows%20Presentation%20Foundation-blue.svg)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows/)

> 一款集成了多种开发工具的 Windows 桌面应用程序，专为提升开发者工作效率而设计。

**帮助文档**：[https://pengcunfu.github.io/devenv/](https://pengcunfu.github.io/devenv/)

## 项目简介

熔岩环境管理工具（仓库代号 DevEnv / 发布包 LavaEnv）是一款基于 .NET 10.0 和 WPF 的 Windows 桌面应用，集成常用开发工具，为开发者提供一站式环境管理。

### 5. 文件哈希计算器

**哈希值计算工具**

- 支持多种哈希算法：MD5、SHA-1、SHA-256、SHA-384、SHA-512
- 文件拖拽支持
- 文本哈希计算
- 一键复制哈希值
- 实时计算进度显示
- 文件大小和计算用时统计
- 多算法并行计算

**技术特色**：
- 异步计算，大文件处理流畅
- 8KB 分块读取，内存优化
- 自动进度反馈
- 精确计算耗时统计

### 核心理念

- **一站式工具集** - 集成常用开发工具，减少工具切换
- **简洁高效** - 直观的界面设计，操作简单快捷
- **安全可靠** - 所有操作都有权限检测和错误处理
- **持续扩展** - 模块化设计，易于添加新功能

## 功能特性

### 1. 系统服务管理

**Windows 服务管理器**

- 查看所有系统服务状态
- 一键启动/停止服务
- 实时状态监控
- 服务状态颜色标识

**功能亮点**：
- 异步操作，不阻塞UI
- 自动刷新服务状态
- 启动/停止权限检测

### 2. JSON 格式化工具

**JSON 格式化器**

- JSON 字符串格式化/压缩
- 语法高亮显示
- 错误检测和提示
- 支持大文件处理

**使用场景**：
- API 响应数据格式化
- 配置文件美化
- 数据验证和调试

### 3. 软件下载工具

**软件下载器**

- 批量下载软件
- 下载进度实时显示
- 断点续传支持
- 下载历史记录

**支持功能**：
- 多线程下载
- 下载速度显示
- 下载完成通知
- 下载失败重试

### 4. Hosts 文件编辑器

**Hosts 文件管理器**

- 双重编辑模式：文本编辑 + 按条编辑
- IP 地址验证
- 添加/修改/删除 hosts 条目
- 保留注释和空行

**便民工具**：
- **定位到文件** - 在资源管理器中快速定位 hosts 文件
- **在记事本中打开** - 使用系统记事本进行外部编辑
- **环境变量** - 快速访问系统环境变量设置

**安全特性**：
- 管理员权限检测
- 自动创建备份文件 (.backup)
- IP 地址格式验证
- 友好的错误提示

## 快速开始

### 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10/11 |
| .NET 版本 | .NET 10.0 或更高版本 |
| 内存 | 最少 512 MB RAM |
| 磁盘空间 | 100 MB 可用空间 |
| 权限 | 某些功能需要管理员权限 |

### 安装与运行

#### 方式一：直接运行（推荐）

1. 下载最新版本的发布包
2. 解压到任意目录
3. 右键点击 `DevEnv.exe`，选择"以管理员身份运行"
4. 开始使用！

#### 方式二：源码运行

```bash
# 克隆仓库
git clone https://github.com/pengcunfu/devenv.git
cd devenv

# 构建项目
dotnet build

# 运行程序
dotnet run
```

> **注意**：Hosts 文件编辑功能需要管理员权限才能运行

## 使用指南

### 主界面说明

启动程序后，您将看到主窗口，包含：

1. **顶部菜单栏**
   - 工具菜单：包含所有功能入口
   - 帮助菜单：关于信息

2. **服务管理区域**
   - 实时显示系统服务列表
   - 每个服务显示状态、启动/停止按钮

3. **状态栏**
   - 显示程序当前状态

### 常用操作

#### 管理系统服务

1. 在服务列表中找到目标服务
2. 查看当前状态（运行中/已停止）
3. 点击"启动"或"停止"按钮
4. 等待操作完成

#### 格式化 JSON

1. 点击 `工具 > JSON 格式化器`
2. 在文本框中粘贴或输入 JSON 字符串
3. 点击"格式化"或"压缩"按钮
4. 查看格式化后的结果

#### 下载软件

1. 点击 `工具 > 软件下载器`
2. 添加下载任务（输入软件信息）
3. 开始下载，查看进度
4. 下载完成后查看历史记录

#### 编辑 Hosts 文件

1. 点击 `工具 > Hosts 文件编辑器`

**文本编辑模式**：
- 直接编辑完整的 hosts 文件内容
- 支持语法高亮
- 适合批量修改

**按条编辑模式**：
- 以列表形式管理条目
- 点击"添加"创建新条目
- 选择条目后点击"修改"或"删除"
- 自动验证 IP 地址格式

**便民工具**：
- **定位到文件**：在文件资源管理器中选中 hosts 文件
- **在记事本中打开**：使用系统记事本编辑
- **环境变量**：打开系统环境变量设置窗口

> **提示**：修改 hosts 文件后，建议运行 `ipconfig /flushdns` 命令刷新 DNS 缓存

#### 计算文件哈希

1. 点击 `工具 > 文件哈希计算器`

**文件哈希计算**：
- 选择文件：使用"浏览"按钮选择文件，或直接拖拽文件到窗口
- 粘贴路径：使用"粘贴文件路径"从剪贴板粘贴文件路径
- 选择算法：MD5、SHA-1、SHA-256、SHA-384、SHA-512
- 计算哈希：点击"计算哈希"计算单个算法，或"全部算法"同时计算所有算法

**文本哈希计算**：
- 切换到"文本哈希"标签页
- 在文本框中输入或粘贴文本内容
- 选择哈希算法
- 点击"计算文本哈希"

**便民功能**：
- **拖拽支持**：直接拖拽文件到窗口开始计算
- **一键复制**：点击"复制"按钮将哈希值复制到剪贴板
- **进度显示**：大文件计算时显示实时进度
- **详细信息**：显示文件大小、计算耗时等统计信息

## 技术架构

### 技术栈

- **框架**：.NET 10.0
- **UI 技术**：WPF (Windows Presentation Foundation)
- **架构模式**：MVVM (Model-View-ViewModel)
- **异步编程**：async/await
- **数据绑定**：WPF Data Binding
- **命令模式**：ICommand 接口

### 项目结构

```
DevEnv/
├── App.xaml                 # 应用程序定义
├── App.xaml.cs             # 应用程序逻辑
├── AssemblyInfo.cs         # 程序集信息
├── DevEnv.csproj          # 项目文件
│
├── Models/                 # 数据模型
│   └── HostsEntry.cs      # Hosts 条目模型
│
├── ViewModels/            # 视图模型
│   ├── MainViewModel.cs   # 主窗口视图模型
│   ├── HostsFileEditViewModel.cs  # Hosts 编辑视图模型
│   └── HashCalculatorViewModel.cs # 哈希计算视图模型
│
├── Views/                 # 视图
│   ├── MainWindow.xaml           # 主窗口
│   ├── MainWindow.xaml.cs        # 主窗口逻辑
│   ├── JsonFormatterWindow.xaml  # JSON 格式化窗口
│   ├── JsonFormatterWindow.xaml.cs
│   ├── SoftwareDownloadWindow.xaml  # 下载工具窗口
│   ├── SoftwareDownloadWindow.xaml.cs
│   ├── HostsFileEditWindow.xaml  # Hosts 编辑窗口
│   ├── HostsFileEditWindow.xaml.cs
│   ├── ImageConverterWindow.xaml # 图像转换窗口
│   ├── ImageConverterWindow.xaml.cs
│   ├── HashCalculatorWindow.xaml # 哈希计算窗口
│   ├── HashCalculatorWindow.xaml.cs
│   ├── EditHostsEntryDialog.xaml  # 条目编辑对话框
│   └── EditHostsEntryDialog.xaml.cs
│
├── Services/              # 服务层
│   ├── JsonFormatterService.cs  # JSON 格式化服务
│   └── HashCalculatorService.cs # 哈希计算服务
│
├── Converters/            # 值转换器
│   ├── StringToBrushConverter.cs
│   └── FileSizeConverter.cs
│
└── Resources/             # 资源文件
    └── icons/            # 图标资源
```

### 核心设计模式

#### MVVM 模式

- **Model**：数据模型和业务逻辑
- **View**：XAML 界面设计
- **ViewModel**：数据绑定和命令处理

#### 命令模式

使用 `ICommand` 接口实现 UI 与业务逻辑的分离：

```csharp
public ICommand AddEntryCommand { get; }
public ICommand ModifyEntryCommand { get; }
public ICommand DeleteEntryCommand { get; }
```

#### 异步编程

所有耗时操作都使用异步模式，避免 UI 阻塞：

```csharp
private async Task LoadHostsFile()
{
    await Task.Run(() =>
    {
        // 耗时操作
    });
}
```

## 安全与权限

### 权限要求

| 功能 | 权限要求 |
|------|----------|
| 服务管理 | 普通用户权限 |
| JSON 格式化 | 普通用户权限 |
| 软件下载 | 普通用户权限 |
| Hosts 编辑 | **管理员权限** |

### 安全措施

1. **权限检测**
   - 启动时检测管理员权限
   - 操作前验证权限状态
   - 友好的权限不足提示

2. **数据保护**
   - 操作前自动创建备份
   - 严格的输入验证
   - 异常情况安全恢复

3. **错误处理**
   - 全局异常捕获
   - 用户友好的错误信息
   - 操作失败自动回滚

## 版本历史

### v1.0.0 (2025-01-20)

#### 首次发布
- 系统服务管理功能
- JSON 格式化工具
- 软件下载工具
- Hosts 文件编辑器
- 图像格式转换器
- 文件哈希计算器

### 计划中的功能

#### v1.1.0
- [ ] 系统信息查看器
- [ ] 网络连接监控
- [ ] 端口占用检测
- [ ] 进程管理工具

#### v1.2.0
- [ ] 代码片段管理
- [ ] 快速笔记功能
- [ ] 项目模板生成
- [ ] 快捷键支持

## 贡献指南

我们欢迎所有形式的贡献！无论是 Bug 报告、功能建议还是代码贡献。

### 贡献方式

1. **Fork 项目**
2. **创建功能分支**
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **提交更改**
   ```bash
   git commit -m "Add: your feature description"
   ```
4. **推送分支**
   ```bash
   git push origin feature/your-feature-name
   ```
5. **创建 Pull Request**

### 开发指南

#### 环境搭建

1. 安装 .NET 10.0 SDK
2. 安装 Visual Studio 2022 或 VS Code
3. 克隆项目到本地
4. 打开 `DevEnv.sln` 解决方案

#### 代码规范

- 遵循 C# 编码规范
- 添加必要的注释和文档
- 保持代码简洁清晰
- 编写单元测试（可选）

## 开源许可

本项目采用 [MIT License](LICENSE) 开源许可证。

```
MIT License

Copyright (c) 2025 熔岩环境管理工具 Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## 常见问题

### Q: 提示"没有权限访问 hosts 文件"怎么办？

**A**: 请以管理员身份运行程序：
1. 右键点击 `DevEnv.exe`
2. 选择"以管理员身份运行"
3. 重新尝试操作

### Q: 修改 hosts 文件后不生效？

**A**: 需要刷新 DNS 缓存：
1. 以管理员身份打开命令提示符
2. 运行命令：`ipconfig /flushdns`
3. 重启浏览器或相关应用

### Q: 服务启动/停止失败？

**A**: 检查以下几点：
1. 确认当前用户有足够权限
2. 确认服务状态允许操作
3. 查看 Windows 事件日志获取详细信息

### Q: 如何导出/导入配置？

**A**: 程序配置存储在程序目录的 `config` 文件夹中，直接复制该文件夹即可备份配置。

## 联系我们

- **项目主页**：https://github.com/pengcunfu/devenv
- **问题反馈**：https://github.com/pengcunfu/devenv/issues
- **功能建议**：https://github.com/pengcunfu/devenv/discussions

## 致谢

感谢以下开源项目和资源：

- [.NET](https://dotnet.microsoft.com/) - 微软 .NET 平台
- [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) - Windows Presentation Foundation
- 所有贡献者和测试用户的支持！

---

<div align="center">

### 如果这个项目对您有帮助，请给我们一个 Star！

**让开发更简单高效**

Made by [pengcunfu](https://github.com/pengcunfu)

</div>
