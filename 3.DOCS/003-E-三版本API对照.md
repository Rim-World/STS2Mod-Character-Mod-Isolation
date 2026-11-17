# 003-E 三版本 API 对照

- 建立时间：2026-11-17 02:16
- 目标版本：0.111.0、0.113.1（0.107.1 仅保留历史对照，不再支持）
- 方法：逐文件读反编译源码，取签名与实现差异；不运行游戏。
- 结论：本项目运行期需要读取/补丁的成员在 0.111.0、0.113.1 上大多签名一致；差异集中在 `Mod` 程序集字段、角色数量、章节方法重载与 0.113 新增枚举/类型。单程序集可行，差异用反射与 API 白名单吸收。0.107.1 不再作为目标。

## 一、必须使用的成员

| 成员 | 0.107.1 | 0.111.0 | 0.113.1 | 签名/说明 |
|---|---|---|---|---|
| `ModManager.GetLoadedMods()` | `ModManager.cs:938` | `ModManager.cs:1102`（同实现） | `ModManager.cs:1102` | 返回 `IEnumerable<Mod>`，只含 Loaded |
| `ModManager.Mods` | 有 | 有 | 有 | 可作为备用 |
| `Mod.manifest` | `Mod.cs:35` | `Mod.cs:35` | `Mod.cs:35` | `ModManifest?` |
| `Mod.path` | 有 | 有 | 有 | 模组目录 |
| `Mod.assembly` | `Mod.cs:46` | 无 | 无 | 107 单程序集字段 |
| `Mod.assemblies` | 无 | `Mod.cs:48` | `Mod.cs:48` | 111+ `List<Assembly>` |
| `ModManifest.id` | `ModManifest.cs:19` | 同 | 同 | 模组 ID |
| `RunManager.IsInProgress` | `:90` | `:104` | `:107` | `State != null` |
| `RunManager.DebugOnlyGetState()` | `:1701` | `:1809` | `:2008` | 公开返回 `RunState?` |
| `RunManager.NetService` | `:121` | `:164` | `:181` | 判断多人 |
| `RunState.Players` | `:60` | `:60` | `:63` | 本局玩家 |
| `RunState.GameMode` | `:205` | `:205` | `:208` | 枚举；不引用 113 新值 |
| `Player.Character` | `:44` | `:44` | `:45` | 本局角色 |
| `Player.UnlockState` | `:79` | `:79` | `:80` | 玩家解锁状态 |
| `UnlockState.Characters` | `:57` | `:57` | `:57` | 角色集合出口 |
| `UnlockState.CharacterCardPools` | `:111` | `:111` | `:115` | 由 Characters 派生 |
| `ModelDb.AllCharacters` | `:123` | `:145` | `:146` | 5/5/6 个原版；模组后置追加 |
| `ModelDb.AllSharedEvents` | `:135` | `:157` | `:159` | 共享事件 |
| `ModelDb.AllSharedAncients` | `:167` | `:189` | `:192` | 共享先古 |
| `ModelDb.ActsByIndex` | `:289` | `:323` | `:327` | 章节随机列表 |
| `CardPoolModel.GetUnlockedCards` | `:84` | `:101` | `:101` | 卡池查询出口 |
| `RelicPoolModel.GetUnlockedRelics` | `:54` | `:54` | `:54` | 遗物池查询出口 |
| `PotionPoolModel.GetUnlockedPotions` | `:54` | `:54` | `:54` | 药水池查询出口 |
| `ActModel.AllEncounters` | `:144` | `:144` | `:144` | 遭遇集合 |
| `ActModel.AllEvents` | `:208` | `:208` | `:208` | 抽象属性；按具体章节补丁 |
| `ActModel.AllAncients` | `:202` | `:202` | `:202` | 抽象属性；按具体章节补丁 |
| `ActModel.GetUnlockedAncients` | `:300` | `:300` | `:300` | 抽象方法；按具体章节补丁 |
| `ActModel.PullNextEvent(RunState)` | `:437` | `:437` | `:463` | 事件消费 |
| `ActModel.GenerateRooms` | `:331` 三参 | `:331` 三参 | `:339` 四参 | 签名不兼容；按版本补丁存在的重载 |
| `ActModel.GetRandomList` | `:538` | `:538` | `:564` | 章节选择 |
| `StartRunLobby.BeginRunLocally` | `:461` | `:469` | `:544` | 私有；签名一致 |
| `Kaleidoscope.IsAllowedAtNeow` | `:24` | `:24` | `:24` | 计数判断三版一致 |
| `AbstractModel.Id` | `:31` | `:31` | `:33` | 模型标识 |

## 二、差异

### 2.1 Mod 程序集字段

107：

```csharp
public Assembly? assembly;
```

111/113：

```csharp
public List<Assembly> assemblies = new List<Assembly>();
```

处理：反射读 `assemblies`；字段不存在时读 `assembly`；两者都没有时用 `Assembly.Location` 是否位于 `Mod.path` 下补充。

### 2.2 角色数量

| 版本 | `ModelDb.AllCharacters` 数量 | 差异 |
|---|---|---|
| 0.107.1 | 5 | 无愚者 |
| 0.111.0 | 5 | 无愚者 |
| 0.113.1 | 6 | 新增 `Fool` |

处理：不硬编码数量；原版角色按 `type.Assembly == typeof(ModelDb).Assembly` 判断。

### 2.3 章节方法重载

| 版本 | `GenerateRooms` |
|---|---|
| 0.107.1/0.111.0 | `GenerateRooms(Rng, UnlockState, bool isMultiplayer = false)`（三参数） |
| 0.113.1 | `GenerateRooms(Rng, UnlockState, bool isMultiplayer = false, bool isBlitz = false)`（四参数）与 `GenerateRooms(RunState)` |

处理：按参数数量定位并补丁各版本存在的方法：107/111 补三参数，113 补四参数。113 的 `GenerateRooms(RunState)` 调用四参数方法，因此被覆盖。代码不得直接调用该方法，避免编译期绑定到 0.113 的四参数签名。

### 2.4 GameMode

| 版本 | 值 |
|---|---|
| 0.107.1/0.111.0 | None、Standard、Daily、Custom |
| 0.113.1 | None、Standard、Daily、Dread、Blitz |

处理：不引用 `Dread`/`Blitz`；不按枚举值分支。需要模式判断时用 `ToString()` 或跳过。

### 2.5 0.113 专属类型

- `Fool`、`FoolCardPool`
- `CardTag.Shoot`、`CardTag.Wah/Cho/Ki`
- `ImpaledPower`
- `GameMode.Dread/Blitz`

处理：运行期不引用。需要名称时用字符串或反射。

### 2.6 StartRunLobby 玩家类型

| 版本 | `Players` 元素 | `character` |
|---|---|---|
| 0.107.1 | `LobbyPlayer` | 公共字段 `character` |
| 0.111.0/0.113.1 | `StartRunLobbyPlayer` | 公共字段 `character` |

处理：`__instance` 反射读 `Players`，再反射读每个元素的 `character`；不写死元素类型。

### 2.7 池查询方法的虚方法差异

- `CardPoolModel.GetUnlockedCards` 是非虚方法，补丁基类即可。
- `RelicPoolModel.GetUnlockedRelics` 与 `PotionPoolModel.GetUnlockedPotions` 是虚方法。原版各池重写这两个方法，且多数重写不调用基类。必须按具体类型逐个补丁其声明实现，不能只补基类。

## 三、单程序集可行性

前提：

1. 编译基线为 0.113.1 的 `sts2.dll`。
2. 运行期只调用上表成员；差异成员用反射。
3. 不引用 0.113 专属类型、枚举值、方法重载。
4. 补丁目标用名称 + 参数类型定位；找不到时记录并跳过。

满足后，一个 DLL 可在三版加载。0.107.1/0.111.0 的程序集未留存，因此旧版无法编译验证；运行验证需要旧版游戏环境。

## 四、未验证

- 未验证：单程序集在 0.107.1 与 0.111.0 的实际加载。确认方法：旧版游戏内启动，读日志，确认无 `MissingMethodException`/`TypeLoadException`。
- 未验证：三版 `Kaleidoscope` 的 `IsAllowedAtNeow` 在过滤后的行为。源码一致，运行未测。
- 未验证：三版 `ActModel.GenerateRooms` 前缀补丁的顺序。需要运行日志确认 BaseLib 后置追加在过滤之前。
- 未确认：0.113.1 的 `Fool` 相关内容是否会影响原版六角色局的池顺序。当前判断不影响，未运行核对。

## 五、取证路径

| 内容 | 路径 |
|---|---|
| 107 反编译 | `STS2-Reference/1.STS2-107Reference/decompiled` |
| 111 反编译 | `STS2-Reference/2.STS2-111Reference/decompiled` |
| 113.1 反编译 | `STS2-Reference/4.STS2-113.1Reference/decompiled` |
| 113.1 程序集 | `4.STS2-113.1Reference/assemblies/sts2.dll` |
| 版本信息 | 游戏目录 `release_info.json` |
