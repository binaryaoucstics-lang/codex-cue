# Codex Cue for Windows

一个轻量的 Windows 桌面提问向导。Codex 通过本机 MCP 工具打开逐题界面，用户可直接点击单选、多选，也可以回答开放式问题。插件可通过生命周期钩子把需要用户回答的问题自动路由到桌面窗口，并在任务完成时提供 2–3 个可点击的下一步任务，无需再复制粘贴回答。

## 界面预览

<p align="center">
  <img src="docs/images/single-select.png" alt="Codex Cue 单选问题界面" width="680">
</p>
<p align="center"><em>单选问题：直接点击选项，也可以输入自定义答案。</em></p>

<table>
  <tr>
    <td width="50%"><img src="docs/images/multi-select.png" alt="Codex Cue 多选与自由输入界面"></td>
    <td width="50%"><img src="docs/images/review.png" alt="Codex Cue 答案复核界面"></td>
  </tr>
  <tr>
    <td align="center">多选与自由输入</td>
    <td align="center">提交前复核与返回修改</td>
  </tr>
</table>

## 安装教程

系统要求：Windows 10 22H2（19045）或更高版本、x64、.NET Framework 4.8。Windows 10/11 通常已包含所需运行时。

### 方法一：使用安装包（推荐）

1. 打开仓库的 [Releases](https://github.com/binaryaoucstics-lang/codex-cue/releases) 页面，下载：
   - `CodexCue-Setup-x64.exe`
   - `SHA256SUMS.txt`
2. 在下载目录打开 PowerShell，校验安装包（可选）：

   ```powershell
   Get-FileHash .\CodexCue-Setup-x64.exe -Algorithm SHA256
   Get-Content .\SHA256SUMS.txt
   ```

   两处 SHA-256 应完全相同。
3. 双击运行 `CodexCue-Setup-x64.exe`。安装器会为当前用户安装应用、注册个人 marketplace、启用插件，并备份同名旧版本。
4. 安装结束后，在 Codex CLI 输入 `/hooks`，找到 `codex-cue` 提供的 `SessionStart`、`UserPromptSubmit` 和 `Stop`，检查命令指向插件内的 `CodexCue.exe`，然后信任并启用三项钩子。
   如使用codex/chatgpt桌面应用，点击左下角进入设置，在编码-钩子页面点击信任并启用两个钩子
5. 关闭安装前已经打开的 Codex 任务，再新建一个任务。已打开的任务不会在运行中重新加载 Skill、MCP 或钩子。
6. 可用以下命令验证安装：

   ```powershell
   codex plugin list
   codex mcp list
   Get-CimInstance Win32_Process -Filter "Name='CodexCue.exe'" |
     Select-Object ProcessId, ExecutablePath, CommandLine
   ```

   预期结果：
   - `codex-cue@personal` 显示为 `installed, enabled`；
   - `codex_cue` MCP 显示为 `enabled`；
   - 至少一个 `CodexCue.exe --host` 常驻进程正在运行。
7. 在新任务中尝试：“请让我选择使用稳定版还是预览版，不要替我决定。”桌面向导应置顶出现。

安装器会启动独立桌面宿主并注册当前用户登录启动项。Codex 的问题和选项文字通过 MCP stdio JSON 与命名管道传输，不使用 PowerShell、临时文件或剪贴板中转。

安装器未签名，首次分发到其他电脑时 Windows SmartScreen 可能显示“未知发布者”。请先核对 `SHA256SUMS.txt`，再选择“更多信息 → 仍要运行”。正式公开发布建议使用 Authenticode 代码签名。

### 方法二：使用便携包

`CodexCue-portable-x64.zip` 包含程序、完整插件、许可证和署名文件，不包含测试、PDB、Node.js 或 Python 运行时。

1. 从 [Releases](https://github.com/binaryaoucstics-lang/codex-cue/releases) 下载 ZIP 和 `SHA256SUMS.txt` 并校验哈希。
2. 解压 ZIP，在解压目录运行：

```powershell
.\CodexCue.exe --install-plugin
```

3. 按安装包教程的第 4～7 步信任钩子、新建任务并验证。

该命令会把程序和插件安装到当前用户目录。若只做协议集成，可直接使用 `plugins\codex-cue\bin\CodexCue.exe --mcp`，不需要后台 HTTP 服务。

### 升级与卸载

- 升级：下载新版本安装包并直接运行。安装器会先创建带时间戳和 SHA-256 清单的旧版备份，再替换托管文件。
- 卸载：从 Windows“设置 → 应用 → 已安装的应用”中卸载 **Codex Cue**。卸载器会移除登录启动项，并在安全条件下恢复安装前备份。
- 若新版本修改了钩子定义，Codex 会要求重新在 `/hooks` 中审核和信任，这是 Codex 的安全机制。

## 工具接口

`ask_options` 接收动态数量的问题与选项；每题支持 `single`、`multiple` 和 `allowOther`。示例：

```json
{
  "questions": [
    {
      "id": "publish",
      "prompt": "选择发布方式",
      "mode": "single",
      "required": true,
      "allowOther": true,
      "options": [
        { "id": "installer", "label": "安装包", "recommended": true },
        { "id": "portable", "label": "便携包" }
      ]
    },
    {
      "id": "targets",
      "prompt": "选择包含内容",
      "mode": "multiple",
      "options": [
        { "id": "windows", "label": "Windows 程序" },
        { "id": "docs", "label": "使用文档" }
      ]
    }
  ],
  "reviewMode": "auto",
  "maxWaitMs": 900000
}
```

结果状态为 `submitted`、`cancelled` 或 `timed_out`。成功结果按问题原始顺序返回 `answers`，并标记 `source: desktop-wpf`。`option_prompt_status` 只报告版本和队列数量，不暴露问题或答案内容。

## 升级、备份与卸载

- 程序安装在 `%LOCALAPPDATA%\Programs\CodexCue`。
- 插件安装在 `%USERPROFILE%\plugins\codex-cue`。
- 每次替换前会在 `%LOCALAPPDATA%\CodexCue\backups` 创建带时间戳和 SHA-256 清单的备份。
- 安装失败会回滚旧插件与 marketplace；卸载时，未被用户修改的托管文件会移除并恢复安装前备份。
- 若安装后的 Skill 被手动修改，卸载器会保留它，避免删除用户内容。

## 构建与验证

开发机需要 PowerShell 5.1、Python、Git 和 MSBuild。构建脚本会下载 .NET 4.8 引用程序集；打包时固定使用 Inno Setup 6.7.3。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\e2e.ps1 -ArtifactRoot .\artifacts\staging
powershell -ExecutionPolicy Bypass -File .\tests\PackageTests.ps1
```

`package.ps1` 输出 Setup、便携 ZIP 和 `SHA256SUMS.txt`，并强制两个分发包分别小于 5,000,000 字节。当前运行时仅依赖 Windows/.NET Framework；Inno Setup、引用程序集和测试文件不会进入分发包。

在当前机器进行完整升级验收：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-local.ps1
```

## 故障排查

- 没有看到选项窗口：新建 Codex 任务，然后用 `codex mcp list` 检查 `codex_cue`，并确认任务管理器中存在带 `--host` 参数的 `CodexCue.exe`。
- 插件未启用：运行 `codex plugin add codex-cue@personal`，再新建任务。
- 宿主无响应：在任务管理器结束 `CodexCue.exe`；下一次工具调用会自动重启宿主。
- 安装异常：查看 `%LOCALAPPDATA%\CodexCue\install-status.json`，旧版文件仍保存在同目录的 `backups` 下。

## 开源许可证

本项目采用 [Apache License 2.0](LICENSE) 开源，并提供 [NOTICE](NOTICE) 署名文件。使用、修改或重新分发时必须遵守许可证要求，保留适用的版权、许可证与署名声明，并在修改文件中注明变更。
