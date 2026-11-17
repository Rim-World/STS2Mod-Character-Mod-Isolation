# 013-E：同一 DLL 的 0.111.1 / 0.113.1 验证

- 建立时间：2026-11-17 22:10
- 目的：确认同一个 `CharacterModIsolation.dll` 同时适用于 0.111.1 与 0.113.1，而不是按版本分发两个 DLL。
- 关联计划：[009-M](../2.PLANS/009-M-111兼容与检测显示.md)
- 关联需求：RQ-009、RQ-019、RQ-020、RQ-021

## 一、环境

| 项 | 值 |
|---|---|
| 游戏 | Slay the Spire 2 v0.113.1，commit `d09d48e5` |
| RitsuLib | 0.6.14，运行时选择 `compat branch: 0.113.0` |
| DLL | `CharacterModIsolation.dll`，SHA-256 `7986c302608fcd94412028c8e47a96633ef102fc4aeae2c9e266b34352395a20` |
| 构建来源 | 0.111.1 程序集构建的 0.2.0 DLL；切到 0.113.1 后未重新构建 |
| 正式模组计数 | 39；自动识别 `MGRMod` |
| 测试探针 | `AutoSlayProbe`；启用时模组计数 40 |

## 二、0.113.1 加载

日志：`8.TEST/0.2.0/2026-11-17-2158-1131-same-binary-load.stdout`

- 51 个补丁安装，0 失败。
- 设置页注册。
- `[auto] built. mods=39 character=1`。
- `MGRMod kind=Character reason=已注册角色`。
- `[settings] enabled=38 autoCharacter=1 [MGRMod]`。
- `[init] main menu ready`。

RitsuLib 日志：

```text
Version: 0.6.14 [compat branch: 0.113.0]
```

## 三、0.113.1 AutoSlayer

命令：

```text
--headless --autoslay --seed=1 --mode=standard
```

结果：

| 项 | 值 |
|---|---|
| 退出码 | 0 |
| 用时 | 437.53 秒 |
| 角色 | `CHARACTER.REGENT` |
| 难度 | A10 |
| 种子 | 1 |
| 结果 | `victory` |
| 进度 | 3/3 幕，49 层 |
| 过滤 | `characters removed=1 kept=6 reason=vanilla-run` |

`kept=6` 包含 0.113.1 的新原版角色「愚者」；0.111.1 的同项结果为 `kept=5`。

`MGR_MOD` 只出现在 MGRMod 启动注册日志。AutoSlayer 开局之后的内容决策段中，`MGR_MOD` 引用计数为 0。

## 四、0.111.1 对照

同一 DLL 已在 0.111.1 完成：

- 49 个补丁，0 失败。
- `characters removed=1 kept=5`。
- AutoSlayer 摄政 A10，49 层上限后主动放弃。
- 禁用 MGRMod 后 `enabled=33 autoCharacter=0 []`。

用户确认不单独复测精确 0.111.0；当前实测基线为 0.111.1。

## 五、副作用与恢复

- `settings.save`、`profile.save`、`modded/profile.save` 哈希与测试前一致。
- `modded/profile1/saves/progress.save` 被 AutoSlayer 更新，已恢复；备份与恢复后哈希一致。
- `modded/profile2/saves/current_run.save` 与测试前一致。
- 测试探针已移出游戏 `mods`，保存在 `TMP/0.2.0/AutoSlayProbe-deployed-2203/`。
- 无游戏进程残留。

## 六、限制

- 设置页视觉由用户自行测试。
- 模组角色实际游玩不在验收范围；本模组的设计预期是在模组角色局不隔离。
- 精确 0.111.0 未测试。
- AutoSlayer 日志中的 `WorldEnvironment` 父节点错误来自 AutoSlayer/场景切换，不影响本模组过滤，也未中断跑局。

## 七、结论

同一份 DLL（SHA-256 `7986...`）在 0.111.1 与 0.113.1 均完成加载、设置页注册、自动识别与原版角色局过滤。0.113.1 完成 AutoSlayer 胜利。单 DLL 兼容已由实测支持。
