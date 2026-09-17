# EternalLib

无尽贪婪（AvaritiaMod）等模组共用的基础库，面向 tModLoader **1.4.4.9（v2026.07）**。

库的定位是“薄而稳”：只提供可复用的基础能力，不注册任何全局补丁（无 IL 补丁、无 `On_` detour），
因此不会与其它模组争夺同一方法的 MonoMod 补丁，也不会因为原版方法变动而失效。

## 模块一览

| 模块 | 类型 | 说明 |
| --- | --- | --- |
| `FrameTexture` | class | 序列帧：整图 / 垂直单方向 / 二维网格，支持逐帧时长时间轴、循环 / 乒乓 / 单次播放 |
| `FrameTextureSystem` | ModSystem | 序列帧的注册、加载、逐 tick 推进与卸载 |
| `FrameItem` | ModItem | 序列帧物品基类，自动处理背包 / 世界 / 手持绘制与多形态切换 |
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

> `FrameTextures` 每次访问都会执行属性体，基类只缓存首次结果（`ModeTextures`）。
> 为了不影响切换形态，请不要在 `Mode` 之外动态改变其内容。

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
* 默认实现返回全局 `Mode`，不区分实例/玩家的物品无需改动。
