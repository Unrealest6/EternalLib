# EternalLib

**English** · [中文](#中文说明)

A small, stable utility library for tModLoader **1.4.4.9 (v2026.07)** mods.
It provides reusable pieces that are tedious to get right: sequence-frame animations and item forms,
tile breaking with drop collection (including chests, walls and multiplayer-authoritative loot),
draggable UI panels, gradients and a few helpers.

Design goals: thin API, no global patches except one small hook, no per-mod state inside the library.
Anything mod-specific (extra drops, where loot goes, which item form is active) is answered by the
consumer mod through an interface, so several mods can use the library at the same time.

* **Tile breaking helpers** — `BreakHelper` / `IAoeMiningTool` / `BreakNet`: break tiles, generate and
  predict drops, normalize multi-tiles, loot chests, kill walls, run AOE mining, and settle everything
  on the server in multiplayer.
* **Frame animation items** — `FrameItem` / `FrameTexture` / `FramePlayer`: vertical or grid sheets with
  per-frame durations, multiple forms per item instance (saved and synced automatically).
* **UI toolkit** — `DragUIState<T>` (draggable panels with snapping and persisted position), `EternalUI`
  factories, a text box with `TextChanged`, `ColorGradient` for gradient text.
* **Misc** — `EternalLog` (auto-detects the calling mod's logger), `EternalExtension` (gradients, colors,
  nearest-entity queries, flight), `TileHelper`, `EternalPlayer`.

Requires tModLoader 1.4.4.9. Build with `dotnet build EternalLib.csproj -c Debug`.
Licensed under the MIT license. Used by [AvaritiaMod](https://github.com/Unrealest6/AvaritiaMod).

---

## 中文说明

无尽贪婪（AvaritiaMod）等模组共用的基础库，面向 tModLoader **1.4.4.9（v2026.07）**。
定位是“薄而稳”：只提供可复用的基础能力，除下面这一处外不注册任何全局补丁（无 IL 补丁、无 `On_` detour），
因此不会与其它模组争夺同一方法的 MonoMod 补丁，也不会因为原版方法变动而失效。
与模组相关的规则（额外掉落、掉落交给谁、当前算不算范围挖掘）一律由使用方通过接口提供，
库里不保存使用方的数值，因此多个模组可以同时使用。

> 唯一一处 detour：为了让“破坏时现算随机奖励”的方块（罐子等）的掉落进得了使用方的收纳容器，
> 库在 `WorldGen.SpawnThingsFromPot`（奖励的唯一生成点）上挂了 `On_WorldGen` 钩子，
> 且只在“范围挖掘上下文”里才接管（见 [物块破坏与范围挖掘](#物块破坏与范围挖掘)）。

## 模块一览

| 模块 | 类型 | 说明 |
| --- | --- | --- |
| `FrameTexture` | class | 序列帧：整图 / 垂直单方向 / 二维网格，支持逐帧时长时间轴、循环 / 乒乓 / 单次播放 |
| `FrameTextureSystem` | ModSystem | 序列帧的注册、加载、逐 tick 推进与卸载 |
| `FrameItem` | ModItem | 序列帧物品基类：背包 / 世界 / 手持绘制、多形态切换、形态的存档与联机同步 |
| `FramePlayer` | ModPlayer | 按玩家同步/缓存手持物形态（供绘制远程玩家） |
| `ItemHelper` | static class | 保 `maxStack` 的物品克隆等通用操作 |
| `BreakHelper` | static class | 物块破坏、掉落生成/预测、箱子、抹墙、范围挖掘（`AoeSwing`）与服务端权威结算 |
| `IAoeMiningTool` / `Int32Modifier` | interface / struct | 范围挖掘工具的契约：额外掉落表与掉落交付都由工具自己回答（库不持有使用方数值） |
| `BreakNet` | static class | 上述功能的联机消息（库自己的包通道） |
| `BreakDropCollector` | GlobalItem | 破坏期间把游戏生成的掉落收进当前收集窗口 |
| `BreakGlobalTile` | GlobalTile | 屏蔽自动掉落（`CanDrop`）与罐子奖励的兜底（`KillTile` / `TileFrame`） |
| `EternalNet` | static class | 库的网络层入口（消息枚举 + 分发） |
| `ColorGradient` | class | 渐变定义与注册表 |
| `EternalExtension` | static class | 字符串渐变着色、颜色明暗运算、`Player` 飞行、最近实体查询、容差比较、三角波 |
| `EternalPlayer` | ModPlayer | `player.CanFly = true` + 空中双击跳跃键 切换自由飞行 |
| `TileHelper` | static class | 箱子类物块的安全开采与内容物提取 |
| `DragUIState<T>` / `DragUISystem<T,TState>` | UIState / ModSystem | 可拖拽面板，带出界回弹、边缘吸附、标题栏拖拽、Esc 关闭与位置持久化 |
| `EternalUI` | static class | 面板 / 标题 / 关闭按钮 / 滚动条列表 工厂 |
| `EternalUITextBox` | UITextBox | 带占位符与 `TextChanged` 事件的输入框 |
| `UIPositionStore` | ModPlayer | 按角色存档持久化面板位置 |

## 快速上手

### 序列帧物品

```csharp
public sealed class InfinityIngot : FrameItem
{
    protected override int FrameCount => 6;
    protected override int FrameDuration => 3;
    protected override FrameDef[] FrameTimeline =>
    [
        0, 0, 0, 1, 1, 1, 2, 2, 3, 4, 5, 4, 3, 2, 1
    ];
    // FrameTimeline 中的数字即帧下标，可写成 new FrameDef(2, 12) 单独指定该帧停留 12 tick
}
```

* 主贴图按 `FrameCount` 竖直切分；`FrameTimeline` 描述播放顺序与每帧时长。
* 额外形态用 `FrameTextures` 提供，并在 `HoldItem` 等钩子里改写 `Mode`：

```csharp
public override Dictionary<byte, FrameTexture?> FrameTextures => new()
{
    [1] = FrameTextureSystem.Register("WorldBreaker2", "Mod/Content/Items/Tools/WorldBreaker2", 9, FrameTimeline, 3)
};
```

> `FrameTextures` 每次访问都会执行属性体，基类只缓存首次结果（`ModeTextures`，按实例弱引用缓存）。
> 为了不影响切换形态，请不要在 `Mode` 之外动态改变其内容。

* `Mode` 是**物品实例**上的状态：`Item.ModItem` 本来就按物品实例创建，
  而 `FrameItem` 进一步声明了 `CloneNewInstances => true` 并重写 `Clone(Item)` 复制 `Mode`。
  这样背包里两件同款物品互不影响，鼠标上那一份、容器里那一份也不会与背包里的同步变化，
  丢到地上（`Item.NewItem`）或容器取放（都走 `Item.Clone()`）之后形态同样不会归零。
* **形态的存档与联机由基类负责**：`SaveData` / `LoadData` / `NetSend` / `NetReceive` 已经实现，
  存档键为 `FrameItemMode`（并兼容读取旧键 `AvaritiaMode`），依赖方不需要再写一遍。
* 多形态物品只需给出形态数量与“每个形态的属性”：

```csharp
public sealed class MyTool : FrameItem
{
    public override byte ModeCount => 2;                       // 形态 0 / 1
    protected override void ApplyModeStats(Item heldItem, byte mode)
        => heldItem.pick = mode == 0 ? int.MaxValue : 0;        // 每 tick 应用到手持实例
    protected override bool ToggleModePressed                    // 切换键（默认 Shift + 右键点按）
        => Main.keyState.IsKeyDown(Keys.LeftShift) && Main.mouseRight && Main.mouseRightRelease;
}
```

* 远程玩家的手持物形态通过 `FramePlayer.HeldItemMode` 同步（切换或换到形态不同的同款物品时补发一次，
  不做每帧发包）；本地玩家 / 单人直接读物品实例，无需关心。

### 物块破坏与范围挖掘

接入方式是**让自己的工具物品实现 `IAoeMiningTool`**：库只认接口，因此多个模组可以各自接入、
互不干扰（库里不保存任何使用方的数值或委托）。

```csharp
// 工具自己的规则：哪些物块要额外产出、收拢到的掉落交给谁
public sealed class MyAoeTool : ModItem, IAoeMiningTool
{
    public IReadOnlyDictionary<bool[], Int32Modifier>? ExtraDropModifier => new Dictionary<bool[], Int32Modifier>
    {
        [TileID.Sets.Ore] = new Int32Modifier(Main.rand.Next(4, 41)),   // 矿石 ×4~40（每格现掷一次）
        [TileID.Sets.CanBeDugByShovel] = new Int32Modifier(2)           // 任何别的物块也能这样加倍
    };
    public void DeliverDrops(Player? player, List<Item> drops, Vector2 position)
        => MyContainer.Pack(drops, position);                              // 默认散落到世界
}
```

* `ExtraDropModifier` 的**键是按物块类型索引的开关表**（`bool[]`，下标 = `Tile.Type`），
  所以可以直接传 `TileID.Sets.Ore` 这类现成的 `bool[]`，也可以自己造一张表只点亮某几个类型；
  值是 <see cref="Int32Modifier"/>（`ApplyTo(x) = (x + Base) * Additive * Multiplicative + Flat`）。
  该属性**每格读取一次并重新求值**，所以“每格随机倍率”直接写在 getter 里即可。
* 两个成员都有默认实现：不想要额外掉落就不必实现 `ExtraDropModifier`（默认 `null` = 不加成）；
  `DeliverDrops` 的默认实现是散落到世界（任何情况下东西都不会凭空消失）。

```csharp
// 一次挥动：构造结算状态（带责任人）→ 逐格结算 → 结束
BreakHelper.AoeSwing swing = new(player);                 // 从玩家手持物品解析工具（没有则散落）
BreakHelper.ProcessAoeTile(Framing.GetTileSafely(x, y), x, y, swing);
swing.KillWall(x, y);
BreakHelper.FinishAoeSwing(swing, Main.MouseWorld);        // 发服务端结算请求 + 交付掉落
```

* 责任人在构造 `AoeSwing` 时从玩家手持物品解析；多人下服务端结算（`RequestLootTiles` /
  `ServerKillWalls`）用发包玩家手上的那件工具，保证额外掉落与收纳策略在服务端同样生效。
* **“形态”由使用方自己管**：库不关心形态，工具在自己的 `UseItem` 里判断“现在是不是范围挖掘形态”，
  需要的话也可以在 `DeliverDrops` 里按形态决定收纳还是散落。
* **“破坏时现算随机奖励”的方块**（原版罐子、回响罐…）默认已在
  `BreakHelper.RandomLootTiles` 里；自己的这类方块在 `PostSetupContent` 里加进去即可
  （它们在 `KillTile` 里现算奖励，且无视 `noItem` / `CanDrop`，必须特判成整段放进收集窗口）。
* 库只在**异常**时写日志（漏网掉落 `Warn`、掉落生成 / 反射异常 `Error`），
  需要写日志时用 `EternalLog.Info/Warn/Error`，它会自动使用调用方模组的 Logger。

### 独立使用序列帧

```csharp
FrameTexture halo = FrameTextureSystem.Register("HaloNoise", "Mod/Assets/Textures/HaloNoise", 8, 3);
Texture2D frame = halo.GetCurrentFrame();      // 已切割好的单帧（自动缓存）
Rectangle src = halo.GetCurrentSourceRect();   // 或直接用图集 + 源矩形绘制
halo.Precache = true;                          // 加载后立即切割全部帧，避免首次绘制卡顿
```

### 渐变文本

```csharp
ColorGradient.Register("Rainbow", [Color.Red, Color.Orange, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Violet],
    millisecondsPerColor: 1000.0 / 60.0, direction: GradientDirection.Left);

line.Text = line.Text.ApplyGradient("Rainbow");          // 按注册名
line.Text = line.Text.ApplyGradient(someGradientObject); // 或直接传 ColorGradient 实例
```

### 自由飞行

```csharp
public override void UpdateEquip(Player player) => player.CanFly = true;
```

### 箱子开采

```csharp
if (TileHelper.TryProcessChestMining(x, y, drop: false, out Item[] items)) { /* items 为箱内物品快照 */ }
```

### 拖拽面板

```csharp
public sealed class MyPanelUI : DragUIState<UIPanel>
{
    // 位置随角色存档持久化；不设置则每次打开都用默认位置
    protected override string? PositionKey => "MyMod/MyPanel";
    // 只允许拖动标题栏，因此不需要再按住 Shift
    protected override UIElement? DragHandle => _title;
    protected override bool RequireModifierKey => false;
    // 松手时距离屏幕边缘 24 像素内自动贴边
    protected override float SnapDistance => 24f;
    // 允许 Esc 关闭（此时可以删掉宿主里自己写的 Escape 处理）
    protected override bool CloseOnEscape => true;

    private UIText? _title;

    public override void OnInitialize()
    {
        Element = EternalUI.CreatePanel(new Vector2(320, 200));
        Append(Element);
        _title = EternalUI.CreateTitle("标题");
        Element.Append(_title);
        Element.Append(EternalUI.CreateCloseButton("关闭",
            () => ModContent.GetInstance<MyUISystem>().HideUI()));
    }
}
```

要点：

* 基类默认“按住 Shift + 左键拖动面板任意位置”；设置 `DragHandle` 后只有该元素能起拖，配合
  `RequireModifierKey => false` 即可实现“拖标题栏移动”。
  > 若 `RequireModifierKey` 为 `false` 却没有设置 `DragHandle`，面板内的任何点击（包括点物品格）都会被当成拖拽起点。
* **按下点落在子元素上时永远不起拖**（不论 `RequireModifierKey` / `DragHandle` 如何设置）。
  这是必需的：起拖会丢弃原版的“按下缓存”（`UserInterface.ClearPointers`），
  而原版点击是“按下记录目标、松手派发”，缓存被丢弃后这次点击再也不会派发——
  于是“按住 Shift 点击物品格/合成槽”这类依赖修饰键的子元素点击会整个失效。
  面板本体拖拽因此只在**空白处**生效，其余区域交给子元素。
* 拖拽期间会占用鼠标（`mouseInterface`），避免松手时误用/误放物品。
* `CloseOnEscape` 默认 `false`，因为宿主通常已自己处理 Escape；打开后建议删掉重复实现，
  否则会播放两次关闭音效。
* `PositionKey` 依赖 `UIPositionStore`（按角色存档保存，不写世界、不参与多人同步）。
  若面板是“绑定某个物块实体”的（例如工作台），仍应像 AvaritiaMod 那样把位置写进 `TileEntity`。

### 常用组件

```csharp
// 输入框：原版 UITextBox 没有任何文本变化事件，这里通过逐帧比对提供 TextChanged
EternalUITextBox search = new(maxLength: 32)
{
    PlaceholderText = "搜索配方"
};
search.Width.Set(200, 0f);
search.Height.Set(30, 0f);
search.TextChanged += query => RefreshList(query);

// 列表 + 滚动条（原版 UIList 自带滚轮支持）
UIList list = EternalUI.CreateList(new Vector2(92, 160), 0.52f, out UIScrollbar scrollbar);
```

## 构建

```powershell
dotnet build EternalLib.csproj -c Debug
```

最后一次打包步骤会写入 `Documents\My Games\Terraria\tModLoader\Mods\EternalLib.tmod`；
若该文件被正在运行的 tModLoader 占用（或当前进程无写权限），打包会以 `TML001` 失败，
但 `bin\Debug\net8.0\EternalLib.dll` 已经生成，依赖它的模组可以正常引用。

## 版本

* `build.txt` 中的版本号与 `EternalLib.ApiVersion` 常量保持一致，依赖方可用它做特性探测。
* 变更记录见 [CHANGELOG.md](CHANGELOG.md)。

## 宿主侧配合：拖拽面板时屏蔽自己的输入

面板拖拽期间库会把根元素的 `IgnoresMouseInteraction` 置位，子元素不再收到点击，
但如果宿主还有**独立于 UI 事件**的拖拽逻辑（例如物品槽的“按住左键拖拽分堆”），
需要用全局会话状态再挡一层：

```csharp
// 1) 每帧判断
if (DragUISession.IsAnyPanelDragging)
{
    return; // 不要开始/继续自己的拖拽
}

// 2) 事件式：面板刚被拖起的那一帧就取消自己的拖拽
public sealed class MyBridgeSystem : ModSystem
{
    public override void Load() => DragUISession.DragStarted += MyDrag.Cancel;
    public override void Unload() => DragUISession.DragStarted -= MyDrag.Cancel; // 必须退订
}
```

## 按物品实例 / 按玩家区分的物品形态

```csharp
public sealed class MyTool : FrameItem
{
    // item 是“正在被绘制的那个物品实例”：
    // 背包/世界绘制 = 该格子或该掉落物自己的实例；手持绘制 = 对应玩家手上的实例。
    protected override byte GetMode(Player drawPlayer, Item? item) => MyModeStore.Get(item);
}
```

* 手持绘制（`ModifyItemDraw`）会传入 `drawInfo.drawPlayer` 与该玩家的手持物品实例；
* 背包绘制（`PreDrawInInventory`）的签名里没有 Item，库通过 `FrameItemDrawContext`
  （一个只作用于 `FrameItem` 的 `GlobalItem`，先于 ModItem 钩子执行）把正在绘制的实例压栈，
  需要“形态跟随物品实例”的物品可以从中取值；
* 世界掉落绘制（`PreDrawInWorld`）用 `whoAmI` 取回 `Main.item[whoAmI]` 这个实例；
* 默认实现返回这个 ModItem 实例自己的 `Mode`（形态天然跟随物品实例），不区分实例/玩家的物品无需改动。
