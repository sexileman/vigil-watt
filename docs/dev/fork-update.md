# Fork 发布与自动更新（`vigil-watt-update`）

> 本分支把自动更新从上游 `evren-oct/vigil-watt` 切到本 fork 的 GitHub Release，并补齐 tag 驱动的发布流程。
> 目标：以后只需改版本号、推一个 `v*` 标签，GitHub Actions 自动打包并发布；应用从本 fork 检查更新，不再被上游包覆盖。
> 本文只写 fork 相关流程；通用打包坑仍见 [build.md](build.md) §8。

## 1. 当前分支做了什么

| 文件 | 改动 |
|---|---|
| `src/VigilWatt.App/Services/UpdateService.cs` | `GitHubRepoUrl` 改为 `https://github.com/sexileman/vigil-watt`；`GiteeRepoUrl` 留空，回退源默认禁用 |
| `.github/workflows/release.yml` | 新增：推 `vigil-watt-update` 分支构建并上传 artifact；推 `v*` 标签构建并创建 GitHub Release |
| `src/VigilWatt.App/VigilWatt.App.csproj` | 版本 `1.2.3` → `1.2.4` |
| `docs/project/release-notes.md` | 新增 1.2.4 用户向更新说明 |
| `docs/project/changelog.md` | 记录更新源切换 |
| `docs/project/handoff.md` | 当前版本与更新源改为 fork |
| `docs/dev/build.md` | 标注本分支不再使用 Gitee 发布流程 |
| `src/VigilWatt.App/Views/AboutPage.xaml` / `.xaml.cs` | 更新源说明改为本分支 GitHub Release；项目链接改指 `sexileman/vigil-watt`，移除 Gitee 入口；不改布局 |
| `AGENTS.md` | 说明 fork 的发布入口 |

关于页只做了文案与链接同步：更新说明、GitHub 项目/Release 链接都指向本 fork，删除已不可用的 Gitee 按钮，布局和交互未动。

## 2. 一次性配置（需要你手动做）

### 2.1 确认 fork 是公开仓库

应用用 Velopack 的 `GithubSource` **匿名**读取 Release；仓库私有会 404。

打开 <https://github.com/sexileman/vigil-watt> → **Settings** → **General** → 拉到最下面 **Danger Zone**，确认 **Change visibility** 显示的是 `Public`。不要在这里改成 Private。

### 2.2 给 Actions 写 Release 的权限

GitHub 新 fork 默认可能只给 `GITHUB_TOKEN` 读权限，`softprops/action-gh-release` 会报 `Resource not accessible by integration`。

打开仓库 → **Settings** → **Actions** → **General** → **Workflow permissions**：

- 选 **Read and write permissions**；
- 勾选 **Allow GitHub Actions to create and approve pull requests**（可选，本流程不建 PR，但勾上不影响）；
- 点 **Save**。

### 2.3 确认 Actions 已启用

仓库顶部 **Actions** 标签能打开、没有 “Workflows aren’t being run on this forked repository” 提示。若有提示，点 **I understand my workflows, go ahead and enable them**。

### 2.4 不需要配置 Secrets

工作流只用自动注入的 `GITHUB_TOKEN`，不需要 PAT、不需要 SSH key、不需要 `GITEE_TOKEN`。

### 2.5 代码签名（可选）

不配也能用，只是首次运行 SmartScreen 会提示“未知发布者”。要消除提示需买代码签名证书，再给 `vpk pack` 加 `--signParams`；这不影响自动更新功能。

## 3. 发版流程（推荐，全自动）

在仓库根执行：

```powershell
# 1. 改版本号（唯一来源）
#    src/VigilWatt.App/VigilWatt.App.csproj 的 <Version> 改成 1.2.5

# 2. 改两份说明
#    docs/project/release-notes.md 加 "## 1.2.5" 段（用户向，必须）
#    docs/project/changelog.md 加一条（开发向，可选但建议）

# 3. 本地先验证
dotnet build VigilWatt.sln -c Debug
dotnet test src/tests/VigilWatt.Core.Tests --no-build --filter "FullyQualifiedName~Mchose|FullyQualifiedName~G435"

# 4. 提交并推送分支（需要你明确授权）
git add -A
git commit -m "release: 1.2.5"
git push origin vigil-watt-update

# 5. 打标签并推送（标签会触发 Actions）
git tag -a v1.2.5 -m "VigilWatt 1.2.5"
git push origin v1.2.5
```

推送标签后打开 **Actions → Release**，等 `build` 任务变绿；**Releases** 页会出现 `v1.2.5`，附件应包含：

- `releases.win.json`
- `RELEASES`
- `VigilWattApp-1.2.5-full.nupkg`
- `VigilWattApp-win-Setup.exe`
- `VigilWattApp-win-Portable.zip`

工作流会校验 **标签版本 = csproj 版本**，不一致直接失败，避免发错包。

### 3.1 只验证、不发布

推 `vigil-watt-update` 分支会自动构建并上传 artifact，**不会创建 Release**；推 `v*` 标签才会发布。这样 feature 分支阶段也能先看 CI 是否绿。

`workflow_dispatch` 还要求 workflow 已在默认分支 `main` 上；只推 feature 分支时，Actions 页可能看不到 **Run workflow**。要手动触发就先合并到 `main`，或直接用分支推送验证。

### 3.2 本地手动打包（CI 不可用时）

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1
```

产物在 `dist/`。手动建 Release 时把 `dist/` **全部文件**一起上传，缺 `releases.win.json` 或 `.nupkg` 都会让自动更新失败。

### 3.3 推不上去：22 端口被断

本机访问 GitHub 的 22 端口会被代理断开（报 `Connection closed by 198.18.0.16 port 22`），改走 443：

```powershell
$env:GIT_SSH_COMMAND = "ssh -o BatchMode=yes -o StrictHostKeyChecking=accept-new -o HostName=ssh.github.com -p 443"
git push -u origin vigil-watt-update
```

要长期生效，在 `%USERPROFILE%\.ssh\config` 里写：

```sshconfig
Host github.com
  HostName ssh.github.com
  Port 443
  User git
```

这和更新源无关：应用读取 Release 走 HTTPS，不需要 SSH。

## 4. 自动更新怎么工作

1. 应用启动时（以及关于页点“检查更新”）调用 `UpdateService.CheckAsync()`。
2. `GithubSource` 读取 `https://github.com/sexileman/vigil-watt/releases` 的**最新非 draft、非 prerelease** Release。
3. 用 Release 里的 `releases.win.json` 比较版本；比当前版本高才提示。
4. 用户确认后下载 `*-full.nupkg`（CI 默认只出全量包；delta 可选，见 §6）。
5. `ApplyUpdatesAndRestart` 交给 Velopack 的 `Update.exe`：等应用退出 → 替换文件 → 重启。
6. 数据目录在应用根目录的 `data\`（安装版被 `current\` 的上一级保住），Velopack 只替换 `current\`，升级不会清设置/历史。

**版本必须严格递增**：`1.2.4` 只能发一次；重发要改成 `1.2.5`。同一个版本重复 `vpk pack` 会报 `release in channel win equal or greater`。

**Gitee 回退默认关闭**：`GiteeRepoUrl = ""`。这样 GitHub 不通时直接报错，而不是去上游 Gitee 拉一个不带 G3/G435 的包把当前版本覆盖掉。

## 5. 第一次迁移（重要）

已经装过旧版的机器，旧版检查的是**上游** `evren-oct/vigil-watt`，看不到本 fork 的 Release。所以：

1. 手动下载并运行 `VigilWattApp-win-Setup.exe`（1.2.4），覆盖安装；
2. 安装包 `packId` 仍是 `VigilWattApp`，设置、历史、日志都保留；
3. 从 1.2.4 起，应用才会检查 `sexileman/vigil-watt`，以后可以自动更新。

便携版（Portable.zip）始终不能自动更新；它没有安装目录，`IsInstalled=false`。

## 6. 关于 delta 包

`vpk pack` 只有在 `dist/` 里保留上一版 `*-full.nupkg` 时才会生成 delta。GitHub Actions 每次都是干净 checkout，**默认只出全量包**（约 35MB），应用会下载全量包，功能不受影响。

要 delta 的话二选一：

- 在本地保留 `dist/`，用 `scripts/build-installer.ps1` 打包后手动上传；
- 给 workflow 加 `actions/cache` 缓存上一版 `dist/`，或先下载上一个 Release 的 `*-full.nupkg` 再打包。

不做也可以，只是每次更新下载量大一点。

## 7. 需要我配置的清单（照着勾）

- [ ] fork `sexileman/vigil-watt` 是 **Public**
- [ ] **Settings → Actions → General → Workflow permissions** 选 **Read and write permissions** 并保存
- [ ] **Actions** 标签已启用（fork 首次需要点 enable）
- [ ] 不需要任何 Secrets
- [ ] 已装旧版的机器：手动装一次 1.2.4
- [ ] 以后发版：改 csproj 版本 + release-notes + 推 `v*` 标签
- [ ] （可选）要 Gitee 镜像时，自建公开镜像、填 `GiteeRepoUrl`、上传同一套资产
- [ ] （可选）代码签名证书，消除 SmartScreen 提示

## 8. 常见问题

| 现象 | 原因 | 处理 |
|---|---|---|
| Actions 报 `Resource not accessible by integration` | `GITHUB_TOKEN` 只有读权限 | 按 §2.2 改成 Read and write |
| Release 没出现，Actions 是绿的 | 手动触发或推分支只出 artifact，不建 Release | 推 `v*` 标签，或手动建 Release |
| 应用提示“已是最新版本”，但 Release 有新版 | Release 是 draft/prerelease；版本不高于当前；附件缺 `releases.win.json`；仓库私有 | 逐项检查 Release 状态、版本号、附件、可见性 |
| 应用检查更新 404 | 仓库没有 Release，或仓库是 Private | 先发第一个 Release；确认 Public |
| 下载失败 / 校验失败 | 附件没传全，或代理截断 | 确认 `dist/` 全部文件都在；重试 |
| 更新后变回上游版本 | 装的是旧版（仍读上游），或手动填了上游 Gitee | 手动装 1.2.4；检查 `GiteeRepoUrl` 不要指向上游 |
| 提示“未安装的运行副本” | 从 `bin/publish/Portable` 直接跑 | 用 Setup.exe 安装后再检查更新 |
| 标签推了但 Actions 没跑 | fork 的 Actions 未启用；标签不是 `v*` | 按 §2.3 启用；标签用 `v1.2.4` |
| 打包报同版本已存在 | `1.2.4` 已发过 | 升到 `1.2.5`，不要复用版本号 |

## 9. 回滚

- **只回滚应用**：在 Releases 里下载上一个版本的 `Setup.exe` 覆盖安装；数据保留。
- **回滚更新源**：把 `UpdateService.GitHubRepoUrl` 改回上游并重新发版；不推荐，会让新设备支持被上游包覆盖。
- **不要删除旧 Release**：旧版本的用户还在读它；删除会让检查更新 404。要下线就发一个更高版本。

## 10. 验收（本分支已做/待做）

已做：

- `dotnet build VigilWatt.sln -c Debug`：0 警告 0 错误；
- 单元测试：设备识别、G3/G435 帧解析全过；
- 本地 `scripts/build-installer.ps1` 能出 `dist/`（1.2.4）。

待做（需要远程权限/你的机器）：

- 推 `vigil-watt-update` 分支；
- 推 `v1.2.4` 标签，确认 Actions 成功、Release 附件齐全；
- 在旧版 1.2.3 机器上手动装 1.2.4，再发 1.2.5 验证自动更新。
