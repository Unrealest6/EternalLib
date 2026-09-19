# 变更记录

## 0.33

**范围挖掘的接入方式改为“工具实现接口”（破坏性变更）**
- 新增 `IAoeMiningTool`：由使用库的模组在自己的工具物品上实现，库只认接口、不保存使用方的数值。
  * `ExtraDropModifier`：额外掉落表，键是**按物块类型索引的开关表**（`bool[]`，下标 = `Tile.Type`，
    可直接传 `TileID.Sets.Ore` 这类现成数组），值是 `Int32Modifier`；属性每次读取都重新求值，
    所以“每格现掷随机倍率”写在 getter 里即可。默认 `null` = 不加成。
  * `DeliverDrops`：收拢到的掉落交给谁；默认散落到世界（保证东西不会凭空消失）。
- 删除 `IsAoeMiningActive` / `OreDropMultiplier` 以及更早的静态接入点
  （`IsAoeTool` / `DeliverDrops` / `OreMultiplierMin` / `OreMultiplierMaxExclusive` / `ShouldCapturePotReward` / `ResetHooks`）。
  `AoeSwing` 只保留责任人上下文（`new AoeSwing(player)`），形态约定由使用方自己在 `UseItem` 里判断。
- 新增 `BreakHelper.ApplyExtraDrops(...)`（`ProcessAoeTile` 与 `GetTileItemDrops` 共用）；
  `GetTileItemDrops(x, y, extraDrops)` 可直接给单块挖掘复用同一张表。
- 新增注册表 `BreakHelper.RandomLootTiles`（默认含原版罐子 / 回响罐），模组可加入自己的“破坏时现算随机奖励”方块。
- `FrameItem.GetModeFor(player, item)` 公开：本地玩家读物品实例、远程玩家读 `FramePlayer.HeldItemMode`。
- **修复**：紧挨箱子的方块被范围挖掘时“挖掉了却什么都不掉”（`Chest.FindChestByGuessing` 会把附近任意格子猜成箱子）；
  库自己的交付被误报成“漏网掉落”。
- **协议健壮性**：库内消息枚举写死数值（调整顺序不再改变协议），每个包都带上 `EternalNet.ProtocolVersion`
  并在接收时校验（不一致直接丢弃并告警）；手持形态快照的条目数加上界校验。
- **库不再携带使用方数据**：`FrameItem` 不再硬编码使用方的旧存档键（改由使用方覆写 `LegacyModeTagKeys`）；
  `TileHelper` 的箱子开采与 `BreakHelper` 重复且把内容物直接掉在世界上，已标记为过时并指向 `BreakHelper`。
- **资源与状态**：`FrameTexture.Unload` 改为同步释放帧纹理（延迟队列在卸载后可能不再执行，会泄漏显存）；
  漏网告警的调用栈回溯改由 `BreakHelper.BreakDiagnostics`（默认关闭）控制，且计数器随库加载 / 卸载复位。
- 日志：只在异常时写日志，日志与异常消息统一使用英文，并走 `EternalLog`。

## 0.32

**新增：物块破坏与范围挖掘框架**
- `BreakHelper`：破坏（屏蔽自动掉落 + 收集窗口）、掉落生成与预测、多格物块左上角归一化、箱子内容物与本体、
  抹墙、范围挖掘（`AoeSwing` / `ProcessAoeTile` / `FinishAoeSwing`）与多人下的服务端权威结算。
- `BreakNet`：破坏 / 批量破坏 / 抹墙 / 结算 / 箱子的联机消息（库自己的包通道）。
- `BreakGlobalTile`（`CanDrop` 屏蔽 + 随机奖励兜底）与 `BreakDropCollector`（生成瞬间收进窗口）；
  库从此在 `WorldGen.SpawnThingsFromPot` 上挂了唯一的 `On_` detour，且只在范围挖掘上下文里接管。

**新增：序列帧物品形态的存档与联机**
- `FrameItem` 自带形态的存档与同步（`SaveData` / `LoadData` / `NetSend` / `NetReceive`）与 `Shift + 右键` 切换；
  新增 `FramePlayer` 按玩家同步手持形态；新增库内网络层 `EternalNet` 与 `ItemHelper`。

## 0.31

- 修复：Shift 点击子元素被面板拖拽吞掉；`FrameItem.Mode` 会“串味”且丢到地上后重置
  （`CloneNewInstances` + `Clone` 按物品实例隔离形态）。

## 0.22 及更早

- 序列帧系统（`FrameTexture` / `FrameTextureSystem`）、渐变文本与颜色运算、UI 工厂与可拖拽面板
  （出界回弹 / 边缘吸附 / 位置持久化）、带 `TextChanged` 的输入框、自由飞行、`EternalLog`。
