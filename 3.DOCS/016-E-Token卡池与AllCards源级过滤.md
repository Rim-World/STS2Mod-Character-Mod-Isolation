# 016-E：Token 卡池与 AllCards 源级过滤

- 建立时间：2026-11-18 01:05
- 状态：已完成；0.113.1 完整局胜利，分类覆盖 0 残留
- 目的：解释 Token 卡池残留 11 张的成因，验证源级过滤，并完成 0.113.1 完整分类覆盖。
- 关联计划：[009-M](../2.PLANS/009-M-111兼容与检测显示.md)
- 关联需求：RQ-002、RQ-014、RQ-018

## 一、Token 卡是什么

`TokenCardPool` 不是普通奖励池。它存放 `CardRarity.Token` 卡牌，这些卡牌由战斗中的具体效果临时生成，例如：

- `Shiv.CreateInHand` 生成小刀；
- 观者模组的 `WatcherMiracle`、`WatcherInsight` 等；
- MGRMod 的 `FlawedGirl0`、`FlawedGirl1`、`Pale`；
- 0.113.1 新增的塔罗 Token。

普通战斗奖励、商店和事件奖励从角色池、无色池、诅咒池等取牌，不从 TokenCardPool 取牌。TokenCardPool 的作用是让这些战斗内生成的卡牌有统一的归属、图鉴与序列化入口。

因此，Token 池里的 11 张模组卡并不等于 11 张会出现在奖励中的卡；但它们仍属于模组内容。原版角色局应把它们从可查询的卡池视图中移除。

## 二、原残留现象

0.111.1 完整局的日志出现：

```text
[filter] category=cards removed=11 kept=14 reason=vanilla-run
```

探针随后独立调用 `TokenCardPool.GetUnlockedCards`，仍得到 25 张，其中 11 张为模组卡。即：

- 后置补丁确实执行了；
- 它修改了当次 `__result`；
- 但同一次开局的其他查询路径仍返回未过滤列表。

这说明只挂在 `CardPoolModel.GetUnlockedCards` 上的后置补丁不能覆盖所有实际调用点。具体是调用点内联、调用时序还是其他路径，未单独做运行时证明；不把单一机制写成事实。

## 三、修复

在 `PoolPatches` 增加 `CardPoolSourcePostfix`，在 `CardPoolModel.AllCards` 出口过滤角色模组卡：

```text
AllCards
  -> 只保留非角色模组卡
  -> 使用 ConditionalWeakTable 按池实例缓存结果
  -> GetUnlockedCards 无论是否走补丁入口，都只能拿到过滤后的源数组
```

同时保留 `GetUnlockedCards` 后置补丁，作为第二层保护。

`AllCards` 是 `ModelDb.AllCardPools` 的源数组，覆盖角色池、Token 池和其他共享池。过滤仍受 `FilterPolicy.CardsActive` 控制：只在原版角色局启用，模组角色局不启用。

## 四、0.113.1 预检

- 游戏：`v0.113.1`，commit `d09d48e5`
- DLL：`88235f1a16048279865e0478ed82ec2051ab43ccc2bd92e45180446bd062f7a0`
- 探针：`AutoSlayProbe`，强制原版摄政
- 日志：`8.TEST/0.2.0/20261118-0100-1131-token-check.stdout`

预检在 100 秒时终止，只验证开局分类覆盖，不是完整局。完整局结果见第五节。

关键日志：

```text
[patches] card-source -> CardPoolModel.get_AllCards
[filter] category=characters removed=2 kept=6 reason=vanilla-run
[filter] category=cards-source removed=11 kept=39 reason=vanilla-run
[probe] coverage category=cards total=717 isolated=0 ids=[]
[probe] cardpool id=TOKEN_CARD_POOL ... total=39 isolated=0 reflectTotal=39 reflectIsolated=0
```

分类覆盖：

| 分类 | 总数 | 模组归属数 |
|---|---:|---:|
| characters | 6 | 0 |
| card-pools | 13 | 0 |
| cards | 717 | 0 |
| relics | 331 | 0 |
| potions | 69 | 0 |
| shared-events | 19 | 0 |
| shared-ancients | 1 | 0 |
| acts | 3 | 0 |
| 各幕 encounters / monsters / events / ancients | 全部 | 0 |

TokenCardPool 从 50 张变为 39 张；11 张模组 Token 卡被移除。直接查询与反射查询均为 39 张，均为 0 个模组归属。

## 五、完整局

- 开始：2026-11-18 01:04
- 结束：2026-11-18 01:11
- 用时：439.10 秒
- 游戏：`v0.113.1`，commit `d09d48e5`
- 退出码：0
- 结果：`Victory! Run completed and returned to main menu`；`Run completed successfully with seed=1`
- 角色：`REGENT`（本局 6 个原版角色中的摄政）
- 日志：`8.TEST/0.2.0/20261118-0104-1131-full-vanilla.stdout`

过滤日志：

```text
[filter] category=characters removed=2 kept=6 reason=vanilla-run
[filter] category=cards-source removed=11 kept=39 reason=vanilla-run
[filter] category=relic-pool removed=1 kept=0 reason=vanilla-run
[filter] category=relics removed=1 kept=151 reason=vanilla-run
[filter] category=potion-pool removed=1 kept=0 reason=vanilla-run
```

分类覆盖：

| 分类 | 总数 | 模组归属数 |
|---|---:|---:|
| characters | 6 | 0 |
| card-pools | 13 | 0 |
| cards | 717 | 0 |
| relics | 331 | 0 |
| potions | 69 | 0 |
| shared-events | 19 | 0 |
| shared-ancients | 1 | 0 |
| acts | 3 | 0 |
| act0 encounters / monsters / events / ancients | 22 / 29 / 13 / 1 | 全部 0 |
| act1 encounters / monsters / events / ancients | 20 / 26 / 10 / 3 | 全部 0 |
| act2 encounters / monsters / events / ancients | 18 / 27 / 9 / 3 | 全部 0 |

TokenCardPool 的独立查询：

```text
[probe] cardpool id=TOKEN_CARD_POOL ... total=39 isolated=0 reflectTotal=39 reflectIsolated=0
```

`cards-source` 移除 11 张后，直接查询与反射查询均为 39 张，模组归属为 0。完整局没有出现模组卡、遗物、药水、事件、遭遇、先古或章节。

## 六、边界

- 完整局为 0.113.1；0.111.1 的旧完整局使用修复前 DLL，新增的 `AllCards` 源级过滤尚未在 0.111.1 复测。
- 0.113.1 的 6 个原版角色包含愚者。
- 未单独出牌验证「飞溅」；源级过滤已覆盖角色池，实际奖励与开局内容日志无模组内容。
- 卡牌库/图鉴是否隐藏模组条目仍不在 v1 范围；本次修的是卡池查询与生成路径。
- 设置页视觉仍由用户自测。
