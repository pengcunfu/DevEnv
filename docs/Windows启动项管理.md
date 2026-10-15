# WinBootSelfStarting - 功能说明

## 已实现的功能

### 1. 应用图标设置
- ✅ 系统图标：使用 `icon.ico` 作为应用程序图标
- ✅ 任务栏图标：应用程序在任务栏显示自定义图标
- ✅ 窗口图标：主窗口标题栏显示图标

配置位置：
- `WinBootSelfStarting.csproj` - ApplicationIcon 配置
- `MainWindow.xaml` - Icon 属性设置

### 2. 窗口居中显示
- ✅ 程序启动时自动居中展示
- ✅ 默认窗口大小：1000x600

配置位置：
- `MainWindow.xaml` - WindowStartupLocation="CenterScreen"

### 3. 右键菜单支持
- ✅ DataGrid 支持右键菜单
- ✅ 菜单项包括：
  - 启用
  - 禁用
  - 删除
  - 刷新

配置位置：
- `MainWindow.xaml` - DataGrid.ContextMenu

### 4. 管理员权限运行
- ✅ 应用程序配置为需要管理员权限
- ✅ 启动时自动请求 UAC 提升
- ✅ 确保能够管理系统级启动项、服务和计划任务

配置位置：
- `app.manifest` - requestedExecutionLevel="requireAdministrator"
- `WinBootSelfStarting.csproj` - ApplicationManifest 配置

### 5. 服务项管理
- ✅ 列出所有自动启动的 Windows 服务
- ✅ 显示服务状态（运行中/已停止）
- ✅ 显示启动类型（Auto/Automatic）
- ✅ 支持禁用服务（将启动类型改为手动）
- ✅ 支持删除服务

服务管理功能：
- 使用 WMI (System.Management) 查询服务列表
- 使用 `sc` 命令管理服务配置
- 仅显示启动类型为 "Auto" 或 "Automatic" 的服务

### 6. 计划任务管理
- ✅ 列出在登录或启动时运行的计划任务
- ✅ 显示任务状态
- ✅ 支持禁用计划任务
- ✅ 支持删除计划任务

计划任务管理功能：
- 使用 `schtasks.exe` 查询和管理计划任务
- 自动过滤仅显示 LOGON 或 STARTUP 触发器的任务

### 7. 增强的 UI 功能
- ✅ 搜索框：支持按名称或命令搜索
- ✅ 类型筛选：支持按类型筛选启动项
  - 全部
  - 注册表
  - 启动文件夹
  - 服务
  - 计划任务
- ✅ 扩展的列显示：
  - 名称
  - 命令/路径
  - 类型
  - 状态（服务/任务）
  - 启动类型
  - 已启用状态
- ✅ 状态栏：显示当前显示的项目数量

## 启动项管理类型

### 支持的启动项类型
1. **注册表启动项** (Registry)
   - HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run

2. **启动文件夹** (StartupFolder)
   - %APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup

3. **Windows 服务** (Service)
   - 自动启动的系统服务

4. **计划任务** (ScheduledTask)
   - 登录或启动时触发的任务

5. **已禁用项** (DisabledRegistry, DisabledFolder)
   - 由程序禁用的启动项

## 操作功能

### 对于注册表和启动文件夹项
- ✅ 添加新启动项
- ✅ 启用/禁用启动项
- ✅ 删除启动项

### 对于服务
- ✅ 禁用服务（改为手动启动）
- ✅ 删除服务
- ⚠️ 注意：删除系统服务需谨慎

### 对于计划任务
- ✅ 禁用任务
- ✅ 删除任务

## 技术实现

### 依赖项
- .NET 8.0 Windows
- WPF (Windows Presentation Foundation)
- System.Management (用于 WMI 服务查询)

### 权限要求
- 需要管理员权限（UAC）
- 用于管理系统级启动项和服务

## 编译说明

### Debug 版本
```bash
dotnet build
```

### Release 版本
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

输出位置：
- Debug: `bin\Debug\net8.0-windows\`
- Release: `bin\Release\net8.0-windows\win-x64\publish\`

## 使用 install.py 安装

项目包含 `install.py` 脚本，可以自动安装程序：

```bash
python install.py
```

该脚本会：
1. 复制编译后的程序到指定目录
2. 创建开始菜单快捷方式
3. 可选创建桌面快捷方式

## 注意事项

1. **权限要求**：程序必须以管理员身份运行才能管理系统启动项
2. **服务管理**：删除或禁用系统服务可能影响系统稳定性，请谨慎操作
3. **计划任务**：某些系统计划任务对系统运行至关重要，删除前请确认
4. **备份建议**：在进行大量修改前，建议先记录或备份当前启动项配置

## 未来可能的增强

- [ ] 支持 HKEY_LOCAL_MACHINE 注册表项（系统级启动项）
- [ ] 导出/导入启动项配置
- [ ] 启动项性能影响分析
- [ ] 启动延迟设置
- [ ] 批量操作支持
