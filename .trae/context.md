## 项目背景

这是一个 FFXIV Dalamud 插件 Questionable 的**国服汉化分支**（cn 分支），基于上游 PunishXIV/Questionable 进行中文翻译和定制。

- 仓库地址：`https://github.com/youdianfengbao/Questionable`
- 上游仓库：`https://github.com/PunishXIV/Questionable`
- 上游分支：upstream/new-main
- 分支名：cn
- 远程 origin：`https://github.com/youdianfengbao/Questionable.git`

## 合并冲突处理原则

当从 upstream/new-main 合并到 cn 分支产生冲突时，遵循以下原则：

1. **代码逻辑变更** → 以上游为准
2. **UI 中文文本** → 保留 cn 分支的中文翻译（`_L("中文")` 格式）
3. **自动生成文件**（如 packages.lock.json） → 全部采用上游
4. **pluginmaster.json** → 版本号与下载链接跟随上游版本，下载链接加 `-cn` 后缀

## 特殊改动清单

### 1. 友好部族分页去橙色标记
文件：`Questionable/Windows/JournalComponents/AlliedSocietyJournalComponent.cs`（约第159-176行）
改动：注释掉了分类标题标橙色的整个 if 块。原逻辑是在有未检查任务时标橙色，改为直接走黄色逻辑（有未完成任务标黄）。被注释的代码中保留了上游最新条件逻辑（`> 30 && !Contains("FATE")`），可作为参考。
效果：不再根据 LastChecked 系统标记橙色，只有未完成任务时标黄色。

### 2. pluginmaster.json 版本管理
文件：`pluginmaster.json`
- `AssemblyVersion`、`DownloadLinkInstall`、`DownloadLinkUpdate` 这三个字段需要与上游版本号一致
- 下载链接格式：``https://github.com/youdianfengbao/Questionable/releases/download/{上游版本号}-cn/latest.zip``
- 其他字段（Author、Name、Punchline、Description 等）保持中文不变

### 3. 中文翻译保留的文件
以下文件在合并时如有冲突，始终保留中文翻译文本：

| 文件 | 保留的中文内容 |
|------|--------------|
| `Questionable/Windows/ConfigComponents/DebugConfigComponent.cs` | 第17行 `_L("高级")` 替代 `_L("Advanced")`；帮助文本全部使用中文 |
| `Questionable/Windows/ConfigComponents/NotificationConfigComponent.cs` | `_L("桌面通知")` 替代 `"NotificationMaster settings"`；`_L("需要安装 NotificationMaster 插件。")` 替代英文帮助文本 |
| `Questionable/Windows/ConfigComponents/SinglePlayerDutyConfigComponent.cs` | 所有 Tab 标签使用中文：主线任务、职业/特职任务、职能任务、通用职能任务、其他任务 |
| `Questionable/Controller/Steps/Shared/AetheryteShortcut.cs` | 第114行 `"等待(区域: ...)"` 替代 `"Wait(territory: ...)"` |
| `Questionable/Model/EAlliedSociety.cs` | 保留 `EAlliedSocietyExtensions.ToFriendlyString()` 扩展方法，包含全部20个部族的中文名称（蜥蜴人族、妖精族……尤卡巨人族），同时保留上游新增的 `AlliedSocietyConverter` 类 |
| `Questionable/Windows/JournalComponents/QuestJournalUtils.cs` | 菜单项使用中文翻译，`#if DEBUG` 包裹已移除 |

### 4. 任务路径 JSON 文件
文件：`QuestPaths/.../*.json`
如 1336_The Lode Warrior.json 这类有中文 Comment 的文件，保留中文 Comment，同时采纳上游新增的 `LastChecked` 等字段。

### 5. 项目文件
`Questionable/Questionable.csproj` 和 `Questionable/packages.lock.json` 均以接受上游版本为准（上游引入了 NotificationMasterAPI 和 PunishLib 等新依赖）。同时保留 cn 分支特有的 I18N 国际化和 I18N.xml 嵌入资源相关配置。

## 常用操作流程

### 同步上游
```bash
git fetch upstream
git merge upstream/new-main
# 解决冲突
git add -A
git commit -m "同步上游v{版本号}"
```

### 更新 pluginmaster.json 版本号
修改 `pluginmaster.json` 中 `AssemblyVersion`、`DownloadLinkInstall`、`DownloadLinkUpdate` 的版本号与上游 `Directory.Build.targets` 中的 `<Version>` 一致。

### 提交推送
```bash
git add -A
git commit -m "更新 pluginmaster.json"
git push origin cn
```
