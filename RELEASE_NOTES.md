# 轻贴 1.0.0

发布日期：2026-10-30

轻贴的首个整理发布版本：一款免安装、本地保存的 Windows 桌面便签工具。

## 主要功能

- 直接输入、自动保存，多张便签独立管理。
- 四个色系共 32 色，可选置顶及透明度。
- 自由移动和缩放，平滑滚轮与细滚动条。
- 收为悬浮球；在球旁拖入「展开 / 关闭」，展开后恢复原来的窗口与阅读状态。
- 本地数据、文本导出及系统托盘操作，无需账号。

## 下载

- [Windows 免安装版：QingTie-1.0.0-windows.zip](https://github.com/cuimingr/QingTie/releases/download/v1.0.0/QingTie-1.0.0-windows.zip)
- [SHA-256 校验：SHA256SUMS-1.0.0.txt](https://github.com/cuimingr/QingTie/releases/download/v1.0.0/SHA256SUMS-1.0.0.txt)

完整解压后，双击「轻贴.exe」。包内附带 README、使用说明和更新记录，不含个人便签及开发测试数据。

适用环境：Windows 10 / 11 桌面系统，.NET Framework 4.8 或更高的 4.x 版本。本次已在 Windows 64 位环境完成现有功能检查及正式程序检查。

## 从旧版更新

先在系统托盘右键退出旧版，再解压运行新版。便签会从原来的 `%LOCALAPPDATA%\QingTie` 目录继续读取；建议更新前备份该目录。

关闭便签会保留内容及关闭状态，可通过托盘「显示全部」找回。完全结束程序请使用托盘「退出」。

## 当前范围

程序未进行数字签名；本版不含云同步、提醒、开机自启和富文本编辑。



