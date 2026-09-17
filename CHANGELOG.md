# 变更记录

## 0.31
**修复：Shift+点击子元素被拖拽吞掉**
- 按住 Shift 点击物品槽/合成槽时，若鼠标停在子元素上，不再触发面板拖拽。
- 拖拽仍可通过标题栏（`DragHandle`）直接开始。

---

## 0.22

### 🔧 玩家可见修复
| 问题 | 修复 |
|------|------|
| 无动画物品在背包/世界中不可见 | `PreDrawInInventory/World` 不再错误拦截原版绘制 |
| 手持武器绘制失效/与其他模组冲突 | 移除 IL 补丁，改用官方 `ModifyItemDraw` 钩子 |
| 世界掉落物位置偏移 | 锚点改为贴底边，与原版一致 |
| `Color` 运算符崩溃 | 修正 `Math.Clamp` 参数顺序 |
| 破坏非箱子方块时崩溃 | 改用 `Chest.DestroyChest`，并修正坐标推断逻辑 |
| `TriangleWave(0)` 除零崩溃 | 增加零值保护 |
| `GetRecent` 空引用 | 默认跳过 null/非激活实体；新增 `GetNearest` 替代 LINQ 热路径 |

### 🎮 新增 UI 能力
- **拖拽面板**：支持拖拽把手（`DragHandle`）、边缘吸附（`SnapDistance`）、Esc 关闭、位置持久化（存角色存档）。
- **拖拽保护**：拖拽期间自动阻断子元素交互（`BlockChildInputWhileDragging`），防止拖面板时分发物品到槽位。
- **UI 工具集**：`EternalUI` 提供一键创建面板/标题/关闭按钮/滚动条/列表；`EternalUITextBox` 支持文本变化事件。
- **全局拖拽状态**：`DragUISession` 提供 `IsAnyPanelDragging` 等跨面板查询。

### ⚠️ 破坏性变更（Breaking Changes）
- 删除 `FrameItem` 所有 IL 相关钩子（`PlayerGetItemDrawFrameHook` 等），持握绘制迁移至 `ModifyItemDraw`。
- 删除 `FrameTexture.GetFrameSourceRect(int, int, int)` 重载。
- `FrameTexture.TotalDuration`：`int?` → `int`。
- `ColorGradient.Gradients`：可写字典 → `IReadOnlyDictionary`。
- `FrameTextureSystem.Register*`：返回类型 `FrameTexture?` → `FrameTexture`（非空）。
- `Player.ResetVelocity` 重命名为 `ClampVelocity`（旧名标记 `[Obsolete]`）。

### 🛠️ 内部质量改进
- **FrameTexture/FrameTextureSystem**：修复加载失败刷屏、卸载泄漏、整图布局未标记加载、帧越界等问题；新增 `Precache/ReleaseFrames/TotalFrames` 等 API。
- **ColorGradient**：静态注册表随模组卸载清理；新增 `RegisterOrReplace/Unregister/TryGet`。
- **ApplyGradient**：逐字符拼接改为有界缓存；修复方括号破坏颜色标签；支持直接传入 `ColorGradient` 实例。
- **EternalPlayer**：重写飞行状态机（双击跳跃），修复卸装/死亡后状态残留、无输入时漂移。
- **DragUIState**：基于鼠标位移增量拖拽，修复父级偏移时漂移；新增 `RootElement` 自动推断、`CancelDrag/ClampIntoView`。
- **DragUISystem**：`ShowUI` 一步完成切换；修复每帧 `new GameTime()` 分配。
- 移除 `Mono.Cecil` / `MonoMod` / `System.Reflection` 全局 using（无 IL 补丁）。
- 新增 `EternalLog`（未加载时退化为控制台）、`EternalLibSystem`（集中清理静态状态）。

### 📝 多人同步相关
- `FrameItem.GetMode` 现在接收 `Player` 与 `Item` 参数，支持按玩家形态绘制，修复多人游戏中其他客户端永远使用形态 0 的问题。
- 新增 `FrameItemDrawContext`（GlobalItem），使背包图标也能按各自实例形态绘制。