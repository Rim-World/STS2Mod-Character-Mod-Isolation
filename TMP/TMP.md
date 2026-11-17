# 临时文件登记

- 建立时间：2026-11-17 02:16
- 当前版本：0.2.0
- 说明：0.2.0 施工与验证期间的备份、无头日志与 GUI 测试输出。

## 0.2.0

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---:|---|---|
| autoslay-save-backup-20261117-2045/ | 2026-11-17 20:45 | 否 | 48128 | 0.2.0 AutoSlayer 测试前的 settings/profile/存档目录备份；按文件夹登记 | 是 |
| settings-before-disable-mgr.json | 2026-11-17 20:40 | 否 | 13.0 | 禁用 MGR 测试前的 settings.save 备份 | 是 |
| AutoSlayProbe-deployed-2051/ | 2026-11-17 20:59 | 否 | 12 | AutoSlayer 测试闸门；测试后从游戏 mods 移出 | 是 |
| godot-before-disabled-test.log | 2026-11-17 20:40 | 否 | 223.0 | 禁用 MGR 测试前的旧 godot 日志 | 否 |
| godot-before-2035.log | 2026-11-17 20:35 | 否 | 109.0 | 0.2.0 首次无头测试前的旧 godot 日志 | 否 |
| gui-test.stdout | 2026-11-17 20:40 | 否 | 167.0 | 正常窗口设置页导航测试输出 | 是 |
| 111-test-backup-20261117-2105/ | 2026-11-17 21:05 | 否 | 340 | 0.111.1 测试前的 settings/profile/progress 备份；按文件夹登记 | 是 |
| 111-test-backup-20261117-2135/ | 2026-11-17 21:35 | 否 | 354 | 0.111.1 禁用 MGR 前的设置/档案/进度与测试闸门部署副本；按文件夹登记 | 是 |
| AutoSlayProbe-111/ | 2026-11-17 21:09 | 否 | 14 | 0.111/0.111.1 测试闸门构建产物 | 是 |
| 113-test-backup-20261117-2202/ | 2026-11-17 22:02 | 否 | 47551 | 0.113.1 同一 DLL 测试前的设置、档案与 saves 备份；按文件夹登记 | 是 |
| AutoSlayProbe-deployed-2203/ | 2026-11-17 22:08 | 否 | 14 | 0.113.1 同一 DLL AutoSlayer 测试闸门；测试后从游戏 mods 移出 | 是 |
| mgr-test-backup-20261117-2221/ | 2026-11-17 22:21 | 否 | 47670 | MGR 强制角色测试前的设置、档案与 saves 备份；按文件夹登记 | 是 |
| AutoSlayProbe-mgr-2221/ | 2026-11-17 22:23 | 否 | 17 | 强制 MGR 角色测试闸门；测试后从游戏 mods 移出 | 是 |
| mgr-test-current-run-2222/ | 2026-11-17 22:24 | 否 | 93 | 强制 MGR 测试产生的 current_run 与备份；测试后从游戏存档目录移出 | 是 |
| full-vanilla-20261117-2345/ | 2026-11-17 23:45 | 否 | 47862 | 完整原版角色局前的设置、档案、存档、模组安装副本与构建日志；按文件夹登记 | 是 |
| full-vanilla-after-test-20261118-0033/ | 2026-11-18 00:33 | 否 | 48078 | 完整局测试后、恢复前的存档副本；用于对比与回退 | 是 |
| AutoSlayProbe-full-fixed-0033/ | 2026-11-18 00:33 | 否 | 28 | 分类覆盖测试探针；测试后从游戏 mods 移出 | 是 |

## 0.1.0

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---:|---|---|
| autoslay-save-backup-20261117-1913/ | 2026-11-17 19:13 | 否 | 48128 | AutoSlayer 测试前的 profile1 与 modded profile1 存档目录备份；按文件夹登记 | 是 |
| settings.save.before-autoslay-20261117-1908 | 2026-11-17 19:08 | 否 | 12.8 | 场景测试前的 settings.save 备份 | 是 |
| profile.save.before-autoslay-20261117-1908 | 2026-11-17 19:08 | 否 | 0.1 | 场景测试前的 profile.save 备份 | 是 |
| headless-release-godot.log | 2026-11-17 05:08 | 否 | 218.4 | 发布前最终 DLL 无头日志 | 是 |
| headless-release.stdout.log | 2026-11-17 05:08 | 否 | 220.9 | 发布前最终 DLL stdout | 是 |
| headless-manual-mgr-godot.log | 2026-11-17 05:04 | 否 | 230.6 | MGR 启用 + 手动模式无头日志 | 是 |
| headless-manual-mgr.stdout.log | 2026-11-17 05:04 | 否 | 234.1 | MGR 启用 + 手动模式 stdout | 是 |
| headless-current-godot.log | 2026-11-17 05:01 | 否 | 215.9 | 最终构建无头加载日志 | 是 |
| headless-current.stdout.log | 2026-11-17 05:01 | 否 | 218.1 | 最终构建 stdout | 否 |
| headless-mgr-godot.log | 2026-11-17 04:59 | 否 | 233.2 | MGRMod 启用后的无头启动与自动识别 | 是 |
| headless-mgr.stdout.log | 2026-11-17 04:59 | 否 | 236.7 | MGRMod 启用后的 stdout | 否 |
| settings-save-before-mgr.json | 2026-11-17 04:58 | 否 | 12.7 | MGR 临时启用前的 settings.save 备份 | 是 |
| settings-save-backup-before-mgr.json | 2026-11-17 04:58 | 否 | 12.7 | MGR 临时启用前的 settings.save.backup 备份 | 否 |
| build-01.log | 2026-11-17 04:38 | 否 | 14.8 | 构建日志 | 否 |
| build-02.log | 2026-11-17 04:38 | 否 | 0.4 | 构建日志 | 否 |
| build-03.log | 2026-11-17 04:40 | 否 | 0.4 | 构建日志 | 否 |
| build-04.log | 2026-11-17 04:41 | 否 | 0.4 | 构建日志 | 否 |
| build-05.log | 2026-11-17 04:43 | 否 | 0.4 | 构建日志 | 否 |
| build-06.log | 2026-11-17 04:45 | 否 | 0.4 | 构建日志 | 否 |
| build-07.log | 2026-11-17 04:46 | 否 | 0.4 | 构建日志 | 否 |
| build-08.log | 2026-11-17 04:47 | 否 | 0.4 | 构建日志 | 否 |
| build-09.log | 2026-11-17 04:49 | 否 | 0.4 | 构建日志 | 否 |
| build-10.log | 2026-11-17 04:50 | 否 | 0.4 | 构建日志 | 否 |
| build-11.log | 2026-11-17 04:51 | 否 | 0.4 | 构建日志 | 否 |
| godot-before.log | 2026-11-17 04:38 | 否 | 54.2 | 启动前日志 | 否 |
| headless-01-godot.log | 2026-11-17 04:39 | 否 | 205.7 | 无头启动日志 | 否 |
| headless-01.stdout.log | 2026-11-17 04:39 | 否 | 211.0 | 无头启动日志 | 否 |
| headless-051-godot.log | 2026-11-17 04:52 | 否 | 216.5 | 无头启动日志 | 是 |
| headless-051.stdout.log | 2026-11-17 04:52 | 否 | 219.8 | 无头启动日志 | 否 |
| headless-final-godot.log | 2026-11-17 04:49 | 否 | 217.4 | 无头启动日志 | 否 |
| headless-final.stdout.log | 2026-11-17 04:49 | 否 | 219.8 | 无头启动日志 | 否 |
| headless-manual-godot.log | 2026-11-17 04:44 | 否 | 212.3 | 无头启动日志 | 否 |
| headless-manual.stdout.log | 2026-11-17 04:44 | 否 | 214.5 | 无头启动日志 | 否 |
| headless-manual2-godot.log | 2026-11-17 04:45 | 否 | 209.8 | 无头启动日志 | 否 |
| headless-manual2.stdout.log | 2026-11-17 04:45 | 否 | 211.9 | 无头启动日志 | 否 |
| headless-manual51-godot.log | 2026-11-17 04:54 | 否 | 217.3 | 无头启动日志 | 是 |
| headless-manual51.stdout.log | 2026-11-17 04:54 | 否 | 219.4 | 无头启动日志 | 否 |
| mgr-decompile/ | 2026-11-17 04:28 | 否 | 1890.2 | MGRMod 反编译查询；批量文件按文件夹登记 | 是 |
| settings-auto-backup.json | 2026-11-17 04:45 | 否 | 0.3 | 设置备份 | 否 |
| settings-auto-backup2.json | 2026-11-17 04:53 | 否 | 0.3 | 设置备份 | 否 |

## 0.0.0

| 文件名 | 创建时间 | 是否清理 | 文件大小（KB） | 用途 | 引用和复用 |
|---|---|---|---|---|---|
| 无 | - | - | - | - | - |
