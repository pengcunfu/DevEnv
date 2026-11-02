# 下载与环境

## 软件下载

1. 在 **熔岩环境** Tab 点击顶栏 **软件下载**。
2. 选择软件与版本，开始下载。
3. 支持 zip、7z、tar.gz 等格式的自动解压（视版本配置而定）。

下载任务可在 **下载管理** 中查看进度与历史记录。

## 环境扫描

**环境扫描** 会检测本机已安装或可用的开发环境，例如：

- Java、Python、Node.js / npm
- Go、PHP、.NET

用于快速了解当前机器环境，与绿色版进程管理相互独立。

## 镜像源配置

在 **镜像配置** 中可为以下工具设置国内或自定义镜像（以界面支持项为准）：

- pip、npm、pnpm、Yarn
- Maven、Composer、Go、NuGet 等

配置保存后，后续下载或命令行工具会按你的设置使用对应镜像。

### 自定义镜像清单

镜像列表没有写死在程序里，而是读取用户数据目录下的 `mirrors_config.json`
（`文档\FNSoftware\DevEnv\mirrors_config.json`）。首次打开镜像页时，程序会从内置模板生成该文件；
之后可直接用文本编辑器修改，或在镜像页点 **编辑镜像清单**。

- 顶层按工具名分组：`pip`、`npm`、`pnpm`、`yarn`、`maven`、`go`、`nuget`、`composer`，名称不区分大小写。
- 每项字段：
  - `key`：唯一标识，同一工具内不可重复，配置镜像时按它定位；
  - `name`：界面显示名称；
  - `url`：写入工具的镜像地址；
  - `trusted_host`：仅 pip 使用，留空表示不设置；
  - `test_url`：测速地址，省略时使用 `url`（Go 代理地址带 `,direct`，建议单独写测速地址）；
  - `official`：是否为官方源，配置官方源时会恢复工具默认设置。
- 修改后回到镜像页点 **刷新** 即可生效，无需重启。文件请以 UTF-8 保存；解析失败时会自动回退到内置模板，不会影响页面加载；删除该文件后下次加载会重新复制内置模板。

```json
{
  "pip": [
    {
      "key": "tsinghua",
      "name": "清华大学",
      "url": "https://pypi.tuna.tsinghua.edu.cn/simple",
      "trusted_host": "pypi.tuna.tsinghua.edu.cn"
    },
    {
      "key": "official",
      "name": "官方源",
      "url": "https://pypi.org/simple",
      "official": true
    }
  ]
}
```
