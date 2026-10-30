# 参与轻贴开发

提交修改前，可先在 Issues 说明问题或建议。请勿提交个人便签、账号信息或未经遮挡的私人截图。

## 本地构建

使用 Windows 与系统自带的 .NET Framework C# 编译器，在 PowerShell 中运行：

```powershell
.\build.ps1 -Configuration Release -Output 轻贴.exe
```

如需完整功能检查和打包，在没有同版本发布目录的源码副本中运行：

```powershell
.\build-release.ps1
```

检查会启动临时测试窗口，并使用独立测试数据目录。生成物位于 `artifacts/` 和 `releases/`，不要提交到源码仓库。正式程序及 SHA-256 校验文件作为 GitHub Release 附件分发。

## 提交修改

- 描述具体问题、修改后的行为以及验证方式。
- 界面修改附上遮挡私人内容后的截图，并注明显示缩放比例。
- 保持旧版便签数据兼容；不要把个人 `%LOCALAPPDATA%\QingTie` 数据加入测试。
- 发布新版本时同步更新版本信息、更新记录、使用说明和发布检查中的版本断言。

核心界面与行为位于 `QingTie.cs`，图标生成位于 `IconBuilder.cs`，正式版本检查位于 `tools/ReleaseCheck.cs`。
