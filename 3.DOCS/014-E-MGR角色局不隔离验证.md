# 014-E：MGR 角色局不隔离验证

- 建立时间：2026-11-17 22:24
- 目的：确认本模组在 MGR 角色局中不生效，MGR 角色与卡牌不被过滤。
- 关联计划：[009-M](../2.PLANS/009-M-111兼容与检测显示.md)
- 关联需求：RQ-019、RQ-020

## 一、环境

| 项 | 值 |
|---|---|
| 游戏 | Slay the Spire 2 v0.113.1 |
| 模组版本 | CharacterModIsolation 0.2.0 |
| DLL SHA-256 | `7986c302608fcd94412028c8e47a96633ef102fc4aeae2c9e266b34352395a20` |
| RitsuLib | 0.6.14，`compat branch: 0.113.0` |
| 已启用角色模组 | `MGRMod`、`Watcher` |
| 自动识别 | `mods=41 character=2`；两个模组均为 `kind=Character` |
| 测试探针 | `AutoSlayProbe`，强制选择 MGR 角色 |

## 二、方法

测试探针中的 Harmony 补丁拦截 AutoSlayer 的角色选择方法，选择 `MGR_MOD_CHARACTER_MGR_CHARACTER`。随后执行：

```text
--headless --autoslay --seed=1 --mode=standard
```

本次只验证角色局状态与前几个房间，不要求跑完整局。测试在 90 秒后由超时终止；终止不是模组错误。

## 三、结果

| 项 | 日志 |
|---|---|
| 强制选择角色 | `[probe] forced character=MGR_MOD_CHARACTER_MGR_CHARACTER` |
| 实际开局角色 | `character=CHARACTER.MGR_MOD_CHARACTER_MGR_CHARACTER` |
| RunContext 状态 | `cmi_active=False` |
| RunContext 原因 | `cmi_reason=mod-character` |
| 起始卡组 | 4 张 `MGR_MOD_CARD_MGR_STRIKE`、4 张 `MGR_MOD_CARD_MGR_DEFEND`、`LITTLE_PARADE`、`LULLABY` |
| 第一次奖励 | `MGR_MOD_CARD_POOL_MGR_CARD_POOL`；卡牌 `MAGURO_DASH`、`ENCORE`、`COWARD_ROCKET` |
| 后续奖励 | 仍为 MGR 卡池与 MGR 卡牌 |
| 过滤日志 | `[filter]` 计数为 0 |

结论：本模组识别到当前局是 MGR 角色局，`RunContext` 明确进入 `mod-character` 非隔离状态。MGR 起始卡组和 MGR 卡池奖励均保留，没有发生内容过滤。

## 四、清理

- `settings.save`、`profile.save`、`modded/profile.save` 哈希与测试前一致。
- `modded/profile1/saves/progress.save` 已恢复。
- 测试产生的 `modded/profile1/saves/current_run.save` 与 `.backup` 已移出到 `TMP/0.2.0/mgr-test-current-run-2222/`。
- 测试探针已移出游戏 `mods`，保存在 `TMP/0.2.0/AutoSlayProbe-mgr-2221/`。
- 无游戏进程残留。

## 五、限制

- 本次只验证 MGR 角色局不隔离，没有跑完整局。
- 没有测试 Watcher 角色局；Watcher 与 MGR 同时启用时已被自动识别为两个角色模组。
- 设置页视觉仍由用户自测。
