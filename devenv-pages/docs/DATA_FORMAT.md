# 数据格式说明（软件下载站）

站点内容全部来自 `src/data/software.json`。**改这个文件 → 提交 → 自动上线**，无需改任何代码。

> 建议用 VS Code 打开该文件：已关联 `schema/software.schema.json`，漏字段、URL 不合法都会用红色波浪线标出，鼠标悬停有中文提示。

---

## 结构总览

```
software.json
├── sitename            站点名称
├── description         站点副标题
└── categories[]        分类列表
    └── 每个分类
        ├── name        分类名（Java / Python / Node.js …）
        ├── icon        分类图标 key（可省略，见「分类图标」）
        └── softwares[] 软件列表
            └── 每个软件
                ├── name             软件名
                ├── desc             一句话描述
                ├── official_site    官网链接（可省略）
                ├── default_source   默认下载来源（可省略，见下）
                └── versions[]       版本列表（第 1 个默认选中）
                    └── 每个版本
                        ├── version    版本号
                        ├── url        下载地址（必填，外链）
                        ├── file_name  文件名（必填）
                        ├── source     下载来源（可省略，继承 default_source）
                        ├── size       文件大小（可省略）
                        ├── os         平台 windows/macos/linux（可省略）
                        ├── arch       架构 x64/arm64（可省略）
                        └── remark     备注（可省略）
```

## 字段说明

| 字段 | 必填 | 说明 |
|---|---|---|
| `version` | 必填 | 版本号，纯文本即可（如 `"8"`、`"3.12.3"`） |
| `url` | 必填 | 下载地址。填**国内镜像**下载最快（见下） |
| `file_name` | 必填 | 文件名，展示在卡片上并用于"复制文件名" |
| `desc` | 选填 | 软件一句话描述 |
| `source` | 选填 | 下载来源，决定徽章颜色：含"镜像"→绿（国内高速）、"官网"→蓝、"GitHub"→紫 |
| `default_source` | 选填 | 软件所有版本来源一致时，写这里一次即可，版本级不用重复写 |
| `size` / `os` / `arch` / `remark` | 选填 | 展示信息与备注 |

## 国内高速镜像（推荐优先）

下载链接优先用国内镜像，速度快：

| 镜像 | 地址前缀 |
|---|---|
| 清华 TUNA | `https://mirrors.tuna.tsinghua.edu.cn/...` |
| 阿里云 | `https://mirrors.aliyun.com/...` |
| 华为云 | `https://repo.huaweicloud.com/...` |
| 中科大 USTC | `https://mirrors.ustc.edu.cn/...` |

例如 Adoptium（OpenJDK）镜像：
`https://mirrors.tuna.tsinghua.edu.cn/Adoptium/8/jdk/x64/windows/OpenJDK8U-jdk_x64_windows_hotspot_8u452b09.zip`

## 如何操作

### 新增一个软件

在对应分类的 `softwares` 数组末尾加一项：

```json
{
  "name": "MyTool",
  "desc": "我的工具",
  "default_source": "清华镜像",
  "versions": [
    { "version": "1.0.0", "url": "https://...", "file_name": "mytool-1.0.0.zip" }
  ]
}
```

### 新增一个版本

在软件的 `versions` 数组里加一项：

```json
{ "version": "2.0.0", "url": "https://...", "file_name": "mytool-2.0.0.zip", "source": "官网" }
```

### 新增一个分类

在 `categories` 数组里加一项（name 会作为导航标签）：

```json
{
  "name": "Editors",
  "icon": "editors",
  "softwares": []
}
```

## 分类图标

`icon` 填的是 SVG 图标 key（渲染为描边线条图标），可用取值如下：

| key | 图标 | 适用分类 |
|---|---|---|
| `go` | 终端/代码 | Go 等语言类 |
| `dotnet` | 靶心 | .NET 等框架类 |
| `editors` | 编辑笔 | 编辑器类 |
| `ai` | 机器人 | AI 工具类 |
| `api` | 烧瓶 | API 测试工具类 |
| `devtools` | 扳手 | 开发工具类 |
| `utils` | 盒子 | 常用工具类 |
| `pack` | 齿轮 | 打包构建类 |

不填 `icon` 时分类标题不带图标。

## 本地校验

```bash
python -m json.tool src/data/software.json
```

输出无报错即 JSON 合法；`npm run dev` 可本地预览效果。
