# 熔岩环境 · 常见问题

## 找不到「熔岩环境」？

请确认使用的是已嵌入熔岩模块的 DevEnv 版本。左侧导航应包含 **熔岩环境** Tab；若没有，请升级到最新 [Release](https://github.com/pengcunfu/DevEnv/releases/latest)。

## 无法编辑 Hosts 文件

从全局启动器搜索框右侧的「内置工具」文件夹点击 **Hosts 文件编辑器** 打开时会请求管理员权限，在 UAC 对话框中选择 **是** 即可打开可写编辑器。若选择 **否** 或取消 UAC，编辑器不会打开。

编辑器底部会实时给出校验结果：绿色表示格式正常，黄色为警告（如下划线主机名、重复条目），红色为错误（如无效 IP、缺少主机名、非法主机名）。点击展开的问题条目可定位到对应行。

也可右键 DevEnv 快捷方式 → **以管理员身份运行** 整个程序（一般不必）。

## 修改 Hosts 后不生效

在管理员命令提示符中执行：

```bat
ipconfig /flushdns
```

然后重启浏览器或相关应用。

## 服务显示「未安装」

表示应用目录中还没有对应绿色包。请使用 **软件下载** 安装，或手动将绿色版解压到 `apps` 下正确子目录（需与配置一致）。

## 启动失败或端口冲突

1. 用 `netstat -ano | findstr :端口号` 查看端口占用。
2. 关闭占用端口的其他程序，或修改该软件配置中的端口。
3. 查看该软件日志目录中的错误信息。

## 以前独立安装的 DevEnv / LavaEnv 怎么办？

熔岩已并入 DevEnv，不再单独发布 exe。可将原数据目录内容复制到 `文档\FNSoftware\DevEnv\`，再在 DevEnv 的「熔岩环境」中继续使用。详见 [数据目录](./data-directory.md)。

## 文档与反馈

- 帮助文档：<https://devenv.cn/guide/lava/getting-started>
- 问题反馈：<https://github.com/pengcunfu/DevEnv/issues>
