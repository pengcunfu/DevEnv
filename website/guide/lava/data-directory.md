# 数据目录

熔岩将下载的软件包、绿色版服务、配置等集中在**数据目录**，与主程序共用根路径 `文档\FNSoftware\DevEnv\`（主程序用 `data.json` / `icons`，熔岩用 `config.json` / `apps` / `cache` 等，互不覆盖）。

默认根路径为用户「文档」下的提供商与程序目录：

```
文档\FNSoftware\DevEnv\
```

例如：`C:\Users\<用户名>\Documents\FNSoftware\DevEnv`。

其中 `FNSoftware` 为软件提供商目录，`DevEnv` 为程序数据目录。可在 **工具 → 数据目录** 中查看实际路径，并通过编辑熔岩的 `config.json` 调整 `cache_dir` / `apps_dir` 等子路径。

::: tip
若曾使用旧版独立 DevEnv / 默认目录 `D:\devenv`，可将其中内容整体复制到上述新路径后再打开「熔岩环境」。
:::

## 与进程的关系

- `apps` 下各子目录对应不同软件的解压结果。
- 进程配置中的可执行文件路径相对于数据目录解析。
- 迁移数据目录时，请整体复制原目录内容到新路径。
