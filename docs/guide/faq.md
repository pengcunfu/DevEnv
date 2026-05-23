# 常见问题

## 无法编辑 Hosts 文件

点击 **工具 → Hosts 文件编辑器** 后，在 UAC 对话框中选择 **是** 即可临时以管理员身份打开编辑器。若选择 **否** 或取消 UAC，则无法保存修改。

也可右键 `DevEnv.exe` → **以管理员身份运行** 整个程序（一般不必）。

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

## 如何更新 DevEnv

在 [Releases](https://github.com/pengcunfu/devenv/releases) 下载新版本 zip，解压覆盖或换新目录；`config.json` 与 `apps` 数据目录可保留后拷贝回去。

## 文档与反馈

- 帮助文档：<https://pengcunfu.github.io/devenv/>
- 问题反馈：<https://github.com/pengcunfu/devenv/issues>
