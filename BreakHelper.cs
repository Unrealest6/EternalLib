namespace EternalLib
{
    /// <summary>
    /// 物块破坏、掉落生成与范围挖掘的通用逻辑。
    /// <para>破坏与掉落：收集窗口（<see cref="ActiveDropSink"/>）、屏蔽自动掉落、多格物块左上角归一化、
    /// 箱子内容物、抹墙，以及多人下的服务端权威结算。</para>
    /// <para>范围挖掘的规则由工具物品实现 <see cref="IAoeMiningTool"/> 提供（额外掉落与掉落交付），
    /// 库不保存使用方的数值；“破坏时现算随机奖励”的方块可在 <see cref="RandomLootTiles"/> 里登记。</para>
    /// </summary>
    public static class BreakHelper
    {
        /// <summary>缓存的原版掉落计算方法（原版为私有静态方法，只能反射调用，必须缓存）。</summary>
        private static readonly MethodInfo? KillTileGetItemDropsMethod =
            typeof(WorldGen).GetMethod("KillTile_GetItemDrops", BindingFlags.NonPublic | BindingFlags.Static);
        public static bool InBounds(int x, int y) => x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY;
        /// <summary>请求服务端破坏一格物块（多人客户端用；单人 / 服务端直接忽略）。</summary>
        public static void RequestServerKillTile(int x, int y, bool noItem = true) => BreakNet.RequestServerKillTile(x, y, noItem);
        /// <summary>批量请求服务端破坏物块（多人客户端用，一次可携带多格坐标）。</summary>
        public static void RequestServerKillTiles(IReadOnlyList<Point16> tiles, bool noItem = true) => BreakNet.RequestServerKillTiles(tiles, noItem);
        /// <summary>
        /// 把多格方块（箱子等）的任意一格换算成左上角坐标。
        /// <para>原版与 tModLoader 的多格判定都以左上角为准，帧坐标每 36 像素（2 格）为一组：
        /// 右列/下行的帧坐标不是 36 的整数倍，各减一格才是左上角。</para>
        /// </summary>
        public static Point16 GetMultiTileOrigin(Tile tile, int x, int y)
        {
            int originX = x;
            int originY = y;
            if (tile.TileFrameX % 36 != 0)
            {
                originX--;
            }
            if (tile.TileFrameY % 36 != 0)
            {
                originY--;
            }
            return new Point16(originX, originY);
        }
        /// <summary>
        /// 取指定物块的原版掉落（包含概率、条件与运气等全部判定）。
        /// </summary>
        /// <returns>是否存在有效掉落</returns>
        public static bool TryGetDrop(int x, int y, Tile tile, out int itemType, out int stack)
        {
            itemType = 0;
            stack = 0;
            if (KillTileGetItemDropsMethod is null || !InBounds(x, y))
            {
                return false;
            }
            // 参数表：x, y, tile, type, stack, prefix, size, noItem
            object[] parameters = [x, y, tile, 0, 0, 0, 0, false];
            KillTileGetItemDropsMethod.Invoke(null, parameters);
            itemType = (int)parameters[3];
            stack = (int)parameters[4];
            return itemType > 0 && stack > 0;
        }
        /// <summary>
        /// 主动破坏方块时屏蔽原版 / 模组的自动掉落（经 <see cref="BreakGlobalTile.CanDrop"/>）。
        /// <para>不能只靠 <c>noItem</c>：本版 tModLoader 对家具类方块（<c>Main.tileFrameImportant</c>）
        /// 会忽略它，箱子 / 工作台 / 收集器都会额外掉一份。</para>
        /// </summary>
        public static bool SuppressTileDrops { get; private set; }

        /// <summary>
        /// 破坏一格物块：本地立即破坏，多人客户端同时请求服务端破坏
        /// （掉落要由调用方自己用 <see cref="TryGetDrop"/> / <see cref="SpawnDrop"/> 产出）。
        /// </summary>
        /// <param name="sink">非 null 时，破坏过程中游戏自己生成的掉落（见 <see cref="IsRandomLootTile"/>）
        /// 会被收进这个收集窗口，而不是留在世界上。</param>
        public static void BreakTile(int x, int y, bool noItem = true, List<Item>? sink = null)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            //只有客户端才需要“请求服务端”；服务端自己执行即可，否则会把包广播回所有客户端
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                BreakNet.RequestServerKillTile(x, y, noItem: true);
            }
            BreakTileLocal(x, y, noItem, sink);
        }
        /// <summary>
        /// 缓存的原版掉落生成方法 <c>WorldGen.KillTile_DropItems</c>（<c>internal</c>，只能反射）。
        /// <para>只有 <c>includeLargeObjectDrops = true</c> 时它才会处理箱子 / 梳妆台 / 篝火，
        /// 而 <c>WorldGen.KillTile</c> 内部传的是 <c>false</c>——家具与箱子的掉落只能从这里拿到。</para>
        /// </summary>
        private static readonly MethodInfo? KillTileDropItemsMethod =
            typeof(WorldGen).GetMethod("KillTile_DropItems", BindingFlags.NonPublic | BindingFlags.Static);
        /// <summary>
        /// 当前正在收拢掉落的列表：非 null 时，游戏自己生成的掉落地物会被
        /// <see cref="BreakDropCollector"/> 直接搬进这里，而不是留在世界里。
        /// <para>设置与恢复在同一个调用栈内完成（挖掘逻辑是单线程的）。</para>
        /// </summary>
        public static List<Item>? ActiveDropSink { get; private set; }
        /// <summary>
        /// 用原版掉落函数为这一格生成掉落，直接收进 <paramref name="sink"/>（不留在世界里）。
        /// <para>必须在方块尚未被破坏时调用：函数要读帧坐标判断风格 / 箱子种类。
        /// 之后应由调用方用 <see cref="BreakTile"/> 屏蔽掉落破坏方块，避免产出第二份。</para>
        /// </summary>
        /// <returns>收进的掉落数量</returns>
        public static int GenerateTileDrops(int x, int y, List<Item>? sink)
        {
            if (sink is null || !InBounds(x, y) || KillTileDropItemsMethod is null)
            {
                Warn($"GenerateTileDrops returned early: sink={sink is not null} inBounds={InBounds(x, y)} methodFound={KillTileDropItemsMethod is not null}");
                return 0;
            }
            Tile tile = Framing.GetTileSafely(x, y);
            if (!tile.HasTile)
            {
                Warn($"GenerateTileDrops: no tile at ({x},{y})");
                return 0;
            }
            int before = sink.Count;
            List<Item>? previousSink = ActiveDropSink;
            ActiveDropSink = sink;
            try
            {
                KillTileDropItemsMethod.Invoke(null, [x, y, tile, true, false]);
                if (sink.Count == before)
                {
                    TileLoader.GetItemDrops(x, y, tile, true, true);
                }
            }
            catch (Exception exception)
            {
                Error($"Drop generation failed: {exception.GetType().Name} {exception.Message}");
            }
            finally
            {
                ActiveDropSink = previousSink;
            }
            return sink.Count - before;
        }
        /// <summary>
        /// 破坏方块并<b>保留</b>游戏的自动掉落（不屏蔽 <c>CanDrop</c>），同时用收集窗口把这些掉落收进
        /// <paramref name="sink"/>。
        /// <para>专门用于<b>原版家具</b>：它的掉落只在引擎自己破坏方块的那条路径里产生
        /// （<c>TileLoader.KillTile</c>；`KillTile_DropItems` 与 `TileLoader.GetItemDrops` 都算不出来），
        /// 所以只能“让它自己掉”，再由 <see cref="BreakDropCollector"/> 在生成瞬间接住。</para>
        /// </summary>
        private static void BreakTileWithDrops(int x, int y, List<Item>? sink)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            List<Item>? previousSink = ActiveDropSink;
            ActiveDropSink = sink;
            try
            {
                WorldGen.KillTile(x, y);
            }
            finally
            {
                ActiveDropSink = previousSink;
            }
        }
        /// <summary>
        /// 只做本地破坏（不发包），并屏蔽自动掉落。供需要自己批量发包的调用方使用
        /// </summary>
        public static void BreakTileLocal(int x, int y, bool noItem = true, List<Item>? sink = null)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            //破坏期间屏蔽自动掉落：本版 tModLoader 的多格物块掉落走 CanDrop，
            //掉落一律由调用方（范围挖掘 / 箱子内容物 / GenerateTileDrops）自己结算。
            SuppressTileDrops = true;
            List<Item>? previousSink = ActiveDropSink;
            if (sink is not null)
            {
                ActiveDropSink = sink;
            }
            try
            {
                WorldGen.KillTile(x, y, noItem: noItem);
            }
            finally
            {
                SuppressTileDrops = false;
                ActiveDropSink = previousSink;
            }
        }
        /// <summary>
        /// 抹掉一格墙体，并把掉落收进 <paramref name="sink"/>。
        /// <para>非环境墙（玩家放置的墙）被抹掉时，<c>WorldGen.KillWall</c> 会直接往世界里掉一份墙体物品，
        /// 用收集窗口在生成瞬间接走。</para>
        /// </summary>
        public static void KillWallWithDrops(int x, int y, List<Item>? sink)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            List<Item>? previousSink = ActiveDropSink;
            if (sink is not null)
            {
                ActiveDropSink = sink;
            }
            try
            {
                WorldGen.KillWall(x, y);
            }
            finally
            {
                ActiveDropSink = previousSink;
            }
        }
        /// <summary>最近一次范围挖掘结束时的 <see cref="Main.GameUpdateCount"/>。</summary>
        public static uint LastAoeSwingTick { get; private set; }
        /// <summary>是否挥过范围挖掘。<see cref="Main.GameUpdateCount"/> 刚进世界时就是 0，
        /// 不能拿 0 当“没挥过”的哨兵值。</summary>
        private static bool _hasAoeSwung;
        /// <summary>距离上次范围挖掘结束过了多少帧（没挥过时返回 <see cref="int.MaxValue"/>）。</summary>
        public static int TicksSinceLastAoeSwing
            => _hasAoeSwung ? (int)(Main.GameUpdateCount - LastAoeSwingTick) : int.MaxValue;
        /// <summary>诊断用：列出世界坐标附近的“随机奖励”方块，附带帧坐标便于定位是哪一格。</summary>
        public static string DescribeNearbyPots(Vector2 worldPosition, int radius = 3)
        {
            int centerX = (int)(worldPosition.X / 16f);
            int centerY = (int)(worldPosition.Y / 16f);
            List<string> found = [];
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (!InBounds(x, y))
                    {
                        continue;
                    }
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile && IsRandomLootTile(tile.TileType))
                    {
                        found.Add($"{tile.TileType}@({x},{y})帧({tile.TileFrameX},{tile.TileFrameY})");
                    }
                }
            }
            return found.Count == 0 ? "无" : string.Join(" ", found);
        }
        /// <summary>
        /// 外部（框架级联 <c>WorldGen.CheckPot</c>、别的模组、延迟的框架检查）破坏罐子时，
        /// 临时打开一个收集窗口用：返回原来的窗口，交给 <see cref="EndExternalCapture"/> 还原。
        /// </summary>
        public static List<Item>? BeginExternalCapture(List<Item> sink)
        {
            List<Item>? previous = ActiveDropSink;
            ActiveDropSink = sink;
            ExternalCaptureActive = true;
            return previous;
        }
        /// <summary>结束 <see cref="BeginExternalCapture"/> 开的临时窗口。</summary>
        public static void EndExternalCapture(List<Item>? previous)
        {
            ActiveDropSink = previous;
            ExternalCaptureActive = false;
        }
        /// <summary>当前是否是“外部破坏”临时开的收集窗口（不是我们主动挖掘时开的那种）。</summary>
        public static bool ExternalCaptureActive { get; private set; }
        /// <summary>刚挥完多少帧内，仍然把罐子奖励算作本次范围挖掘的产物（覆盖延迟的框架级联）。</summary>
        private const int PostSwingCaptureTicks = 30;
        /// <summary>
        /// “破坏时现算随机奖励”的方块类型注册表（默认含原版罐子）。
        /// <para>这类方块的奖励不在掉落表里，而是原版破坏时用 <c>WorldGen.SpawnThingsFromPot</c> 现算的，
        /// 且无视 <c>noItem</c> 与 <see cref="GlobalTile.CanDrop"/>，必须整段放进收集窗口破坏。
        /// 模组可在 <c>PostSetupContent</c> 里加入自己的方块；<see cref="ResetRegistries"/> 会还原成默认值。</para>
        /// </summary>
        public static ISet<int> RandomLootTiles { get; } = new HashSet<int>();
        /// <summary>把注册表还原成默认值，并复位运行期状态（库加载 / 卸载时调用）。</summary>
        public static void ResetRegistries()
        {
            RandomLootTiles.Clear();
            RandomLootTiles.Add(TileID.Pots);
            RandomLootTiles.Add(TileID.PotsEcho);
            //运行期状态一并复位：热重载后残留的窗口 / 屏蔽标记会让新一次挖掘行为异常
            ActiveDropSink = null;
            ExternalCaptureActive = false;
            SuppressTileDrops = false;
            _hasAoeSwung = false;
            LastAoeSwingTick = 0;
            _lastSwingPlayerIndex = -1;
            BreakDropCollector.ResetLeakLog();
        }
        /// <summary>取某个玩家手持物品上的范围挖掘工具（手持的不是这种工具时返回 null）。</summary>
        public static IAoeMiningTool? GetHeldAoeTool(Player? player)
            => player is null ? null : IAoeMiningTool.GetIAoeMiningTool(player.HeldItem);
        /// <summary>
        /// 给 <paramref name="drops"/> 里 <paramref name="fromIndex"/> 之后新增的掉落套用额外掉落加成。
        /// <para>表里的键是按物块类型索引的开关表（下标 = <c>Tile.Type</c>），命中的每一项依次套用。</para>
        /// </summary>
        public static void ApplyExtraDrops(List<Item> drops, int fromIndex, int tileType,
            IReadOnlyDictionary<bool[], Int32Modifier>? extraDrops)
        {
            if (extraDrops is null || drops.Count <= fromIndex)
            {
                return;
            }
            foreach ((bool[] mask, Int32Modifier modifier) in extraDrops)
            {
                if (tileType >= mask.Length || !mask[tileType])
                {
                    continue;
                }
                for (int i = Math.Max(0, fromIndex); i < drops.Count; i++)
                {
                    drops[i].stack = modifier.ApplyTo(drops[i].stack);
                }
            }
        }
        /// <summary>
        /// 把一次挥动的掉落交付出去：优先交给这件工具自己的策略（<see cref="IAoeMiningTool.DeliverDrops"/>），
        /// 没有工具时散落到世界——保证任何路径下东西都不会凭空消失。
        /// <para>调用后 <paramref name="drops"/> 会被清空。</para>
        /// </summary>
        public static void Deliver(List<Item> drops, Vector2 position, Player? player = null, IAoeMiningTool? tool = null)
        {
            if (drops.Count <= 0)
            {
                return;
            }
            Delivering = true;
            try
            {
                if (tool is not null)
                {
                    tool.DeliverDrops(player, drops, position);
                }
                else
                {
                    ScatterDrops(drops, position);
                }
            }
            finally
            {
                Delivering = false;
            }
            drops.Clear();
        }
        /// <summary>
        /// 正在交付本次挖掘的掉落（<see cref="Deliver"/> 内部）。
        /// <para>交付过程中生成的物品（使用方造的收纳容器）是正常产物，<see cref="BreakDropCollector"/>
        /// 的漏网告警据此跳过。</para>
        /// </summary>
        public static bool Delivering { get; private set; }
        /// <summary>把掉落散落到世界上（默认交付方式，不会清空列表）。</summary>
        public static void ScatterDrops(IEnumerable<Item> drops, Vector2 position)
        {
            int tileX = (int)(position.X / 16f);
            int tileY = (int)(position.Y / 16f);
            foreach (Item item in drops)
            {
                SpawnDrop(tileX, tileY, item);
            }
        }
        /// <summary>
        /// 这一格是否属于“我们自己的范围挖掘上下文”，是的话取出责任人（谁在挖、用哪件工具）。
        /// <para>三种情况：①刚挥完 30 帧内（覆盖支撑被拆后延迟发生的框架级联）；
        /// ②本地玩家正握着范围挖掘工具且这一格在其挖掘范围内；③服务端取离这一格最近的这类玩家
        /// （多人下罐子常被 <c>Player.ItemCheck_CutTiles</c> 割掉，奖励由服务端生成）。</para>
        /// </summary>
        public static bool TryGetAoeMiningContext(int i, int j, out Player? player, out IAoeMiningTool? tool)
        {
            player = null;
            tool = null;
            //①刚挥完 30 帧内（覆盖支撑方块被拆后延迟发生的框架级联）：责任人 = 上一次挥动的发起者
            if (TicksSinceLastAoeSwing <= PostSwingCaptureTicks)
            {
                player = _lastSwingPlayerIndex >= 0 && _lastSwingPlayerIndex < Main.maxPlayers
                    ? Main.player[_lastSwingPlayerIndex]
                    : null;
                tool = GetHeldAoeTool(player);
                return true;
            }
            //②单人 / 客户端：本地玩家；③服务端：离这一格最近的玩家（服务端没有“本地玩家”）
            Player? candidate = Main.netMode == NetmodeID.Server ? FindNearestAoeMiner(i, j) : Main.LocalPlayer;
            if (candidate is not null
                && GetHeldAoeTool(candidate) is { } candidateTool
                && candidate.IsInTileInteractionRange(i, j, TileReachCheckSettings.Simple))
            {
                player = candidate;
                tool = candidateTool;
                return true;
            }
            return false;
        }
        /// <summary>是否处于我们自己的范围挖掘上下文（不需要责任人时用这个）。</summary>
        public static bool IsInAoeMiningContext(int i, int j) => TryGetAoeMiningContext(i, j, out _, out _);
        /// <summary>上一次范围挖掘的发起者（用于把延迟生成的奖励交付回同一件工具的收纳策略）。</summary>
        private static int _lastSwingPlayerIndex = -1;
        /// <summary>
        /// 服务端：找出离这一格最近的、手持范围挖掘工具的玩家。
        /// <para>自己遍历 <see cref="Main.player"/> 而不是用 <c>WorldGen.GetPlayerForTile</c>：
        /// 后者在玩家数组未就绪时会越界。</para>
        /// </summary>
        private static Player? FindNearestAoeMiner(int tileX, int tileY)
        {
            Player? nearest = null;
            float nearestDistance = float.MaxValue;
            float centerX = tileX * 16f + 8f;
            float centerY = tileY * 16f + 8f;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                //玩家数组里可能有还没初始化的槽位，GetHeldAoeTool 内部只读 HeldItem，不会抛
                if (player is not { active: true } || GetHeldAoeTool(player) is null)
                {
                    continue;
                }
                float distance = Math.Abs(player.Center.X - centerX) + Math.Abs(player.Center.Y - centerY);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = player;
                }
            }
            return nearest;
        }
        /// <summary>交付“外部破坏方块时接住的掉落”，并自行判定责任人（见 <see cref="TryGetAoeMiningContext"/>）。</summary>
        public static void DeliverExternalDrops(List<Item> drops, int tileX, int tileY)
        {
            if (drops.Count <= 0)
            {
                return;
            }
            TryGetAoeMiningContext(tileX, tileY, out Player? player, out IAoeMiningTool? tool);
            DeliverExternalDrops(drops, tileX, tileY, player, tool);
        }
        /// <summary>
        /// 交付“外部破坏方块时接住的掉落”（责任人已知时用这个）。
        /// <para>单人 / 服务端交给 <paramref name="tool"/> 的收纳策略（没有工具则散落到世界）；
        /// 多人客户端直接吞掉，掉落以服务端为权威。</para>
        /// </summary>
        public static void DeliverExternalDrops(List<Item> drops, int tileX, int tileY,
            Player? player, IAoeMiningTool? tool)
        {
            if (drops.Count <= 0)
            {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                //掉落以服务端为权威：客户端这一份直接吞掉（服务端会生成并同步过来）
                drops.Clear();
                return;
            }
            Deliver(drops, new Vector2(tileX * 16f + 8f, tileY * 16f + 8f), player, tool);
        }
        /// <summary>
        /// 诊断开关（默认关闭）：打开后“漏网掉落”告警会附带调用栈，便于定位是哪条生成路径绕过了收集窗口。
        /// </summary>
        public static bool BreakDiagnostics { get; set; }
        /// <summary>诊断用：当前调用栈的前几帧——漏网的掉落是谁生成的，一眼就能看出来。</summary>
        public static string DescribeCaller(int frames = 8)
        {
            System.Diagnostics.StackTrace trace = new(2, false);
            List<string> names = [];
            for (int i = 0; i < trace.FrameCount && names.Count < frames; i++)
            {
                MethodBase? method = trace.GetFrame(i)?.GetMethod();
                if (method?.DeclaringType is { } declaring)
                {
                    names.Add(declaring.Name + "." + method.Name);
                }
            }
            return string.Join(" ← ", names);
        }
        /// <summary>
        /// 这一格的方块是否属于“破坏时现算随机奖励”的类型（见 <see cref="RandomLootTiles"/>）。
        /// <para>它们的奖励无视 <c>noItem</c> 与 <see cref="GlobalTile.CanDrop"/>，
        /// 必须整段放进收集窗口破坏（<see cref="BreakTileWithDrops"/>），否则会绕过屏蔽掉在世界上。</para>
        /// </summary>
        public static bool IsRandomLootTile(int type) => RandomLootTiles.Contains(type);
        /// <summary>
        /// 破坏物块并交给原版掉落表处理（树木、草等需要原版特殊逻辑的方块）。
        /// <para>多人下由服务端负责生成并同步掉落，本地只做破坏表现，
        /// 避免客户端与服务端各掉一份；单人则直接走原版路径。</para>
        /// </summary>
        public static void BreakTileWithVanillaDrops(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                BreakNet.RequestServerKillTile(x, y, noItem: false);
                WorldGen.KillTile(x, y, noItem: true);
                return;
            }
            WorldGen.KillTile(x, y);
        }
        /// <summary>生成掉落物，并在多人客户端上同步给其它玩家。</summary>
        public static void SpawnDrop(int x, int y, Item item)
        {
            if (item is not { stack: > 0 } || item.IsAir)
            {
                return;
            }
            int index = Item.NewItem(item.GetSource_DropAsItem(), new Vector2(x * 16f, y * 16f), item);
            if (index >= 0 && Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, index, 1f);
            }
        }
        // ---------------------------------------------------------------- 范围挖掘
        /// <summary>
        /// 范围挖掘“一次挥动”的结算状态：掉落、已结算的多格物块、需要服务端结算的坐标，
        /// 以及责任人（谁在挖、用哪件工具）——额外掉落与交付策略都取自这件工具实例。
        /// </summary>
        public sealed class AoeSwing
        {
            /// <summary>发起这次挥动的玩家（多人服务端结算时为发包玩家；可能为 null）。</summary>
            public Player? Player { get; }
            /// <summary>这次挥动使用的范围挖掘工具（玩家没握着这样的工具时为 null，掉落会散落到世界）。</summary>
            public IAoeMiningTool? Tool { get; }
            /// <summary>按玩家手持物品解析责任人。</summary>
            public AoeSwing(Player? player = null)
            {
                Player = player;
                Tool = GetHeldAoeTool(player);
            }
            /// <summary>本次挥动收集到的全部掉落（结束时交付给 <see cref="Tool"/> 的收纳策略）。</summary>
            public List<Item> Drops { get; } = [];
            /// <summary>已结算过的多格物块左上角：2x2 / 3x2 物块只能结算一次，否则每格掉一份。</summary>
            public HashSet<Point16> ProcessedOrigins { get; } = [];
            /// <summary>已结算过的箱子下标（同一个箱子会被多个相邻格子命中）。</summary>
            public HashSet<int> ProcessedChests { get; } = [];
            /// <summary>多人客户端：内容物保存在服务端上的模组方块，交给服务端结算（非多人客户端为 null）。</summary>
            public List<Point16>? ServerLootOrigins { get; } = Main.netMode == NetmodeID.MultiplayerClient ? [] : null;
            /// <summary>多人客户端：只破坏、不产出掉落的方块（软物块），一次批量发给服务端。</summary>
            public List<Point16>? ServerBreakOrigins { get; } = Main.netMode == NetmodeID.MultiplayerClient ? [] : null;
            /// <summary>
            /// 多人客户端：本次挥动要抹掉的墙体包围盒（tile 坐标，<see cref="Rectangle.Right"/> /
            /// <see cref="Rectangle.Bottom"/> 为开区间；没抹过墙时为 null）。
            /// </summary>
            public Rectangle? ServerWallArea { get; private set; }
            /// <summary>
            /// 抹掉一格墙体，并把非环境墙（玩家放置的墙）的掉落收进本次结算。
            /// <para>多人客户端只登记包围盒：墙体掉落客户端与服务端各算一份就会重复，
            /// 统一由服务端抹除并把结果交付回来。</para>
            /// </summary>
            public void KillWall(int x, int y)
            {
                if (!InBounds(x, y))
                {
                    return;
                }
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    Rectangle cell = new(x, y, 1, 1);
                    ServerWallArea = ServerWallArea is { } area ? Rectangle.Union(area, cell) : cell;
                    return;
                }
                //单人 / 服务端：把墙体掉落直接收进本次挥动的结算，而不是留在世界上
                KillWallWithDrops(x, y, Drops);
            }
            /// <summary>诊断用：本次挥动已经处理过的格子数。</summary>
            public int TilesProcessed { get; internal set; }
        }
        /// <summary>
        /// 把任意一格换算成它所属多格物块的左上角（普通方块就是它自己）。
        /// <para>多格物块的掉落与破坏只能在左上角做一次，否则每格产出一次。分两类：登记了
        /// <see cref="TileObjectData"/> 的走官方 <c>TopLeft</c>；只有 <see cref="TileID.Sets.IsMultitile"/>
        /// 标记而没有 <see cref="TileObjectData"/> 的（罐子、暗影珠…）按原版的两格宽摆帧换算。</para>
        /// </summary>
        public static Point16 GetAoeOrigin(Tile tile, int x, int y)
        {
            if (Main.tileFrameImportant[tile.TileType]
                && TileObjectData.GetTileData(tile) is { } data && (data.Width > 1 || data.Height > 1))
            {
                //官方算法：按多格物块的帧坐标换算左上角
                return TileObjectData.TopLeft(x, y);
            }
            //第二类：多格但没有 TileObjectData 的方块（罐子等），原版按两格宽摆帧
            //（frameX = 样式*36 + 列*18，frameY = 行*18）。不归一的话四格会被当成四个独立方块，
            //而罐子奖励是按被破坏那一格的帧坐标现算的，从子格进入会算出另一个样式。
            //目标格必须是同类型的左上角，校验不过就当普通方块处理（宁可少归一，也不能挪到隔壁）。
            if (TileID.Sets.IsMultitile[tile.TileType])
            {
                int originX = x - tile.TileFrameX % 36 / 18;
                int originY = y - tile.TileFrameY / 18;
                if (InBounds(originX, originY)
                    && Framing.GetTileSafely(originX, originY) is { HasTile: true } origin
                    && origin.TileType == tile.TileType
                    && origin.TileFrameX % 36 == 0 && origin.TileFrameY % 36 == 0)
                {
                    return new Point16(originX, originY);
                }
            }
            return new Point16(x, y);
        }
        /// <summary>
        /// 范围挖掘的通用结算：收集这一格（多格物块只在其左上角结算一次）的全部掉落并破坏它。
        /// </summary>
        public static void ProcessAoeTile(Tile tile, int x, int y, AoeSwing swing)
        {
            //单格出错（第三方 GetItemDrops 返回 null / 抛异常）不能让整次挥动中断：
            //异常冒出循环后 FinishAoeSwing 不会执行，已收集的掉落会全部消失。
            try
            {
                ProcessAoeTileInner(tile, x, y, swing);
            }
            catch (Exception exception)
            {
                Error($"ProcessAoeTile failed (type={tile.TileType} @({x},{y})): {exception.GetType().Name} {exception.Message}");
            }
        }
        private static void ProcessAoeTileInner(Tile tile, int x, int y, AoeSwing swing)
        {
            if (!InBounds(x, y) || !tile.HasTile)
            {
                return;
            }
            //“这一格是什么”必须在**破坏之前**记下来：<c>WorldGen.KillTile</c> 会把这一格的 type 清成 0
            //（<c>Tile.ClearTile</c>），破坏之后再读 <c>tile.TileType</c> 只会读到空气——
            //矿石倍率判定（<see cref="TileID.Sets.Ore"/>）与日志里的方块类型都会因此静默失效。
            int tileType = tile.TileType;
            bool isOre = TileID.Sets.Ore[tileType];
            bool isPot = IsRandomLootTile(tileType);
            Point16 origin = GetAoeOrigin(tile, x, y);
            if (!swing.ProcessedOrigins.Add(origin))
            {
                return;
            }
            int ox = origin.X;
            int oy = origin.Y;
            if (swing.ServerLootOrigins is not null)
            {
                //多人客户端：掉落一律交给服务端结算（客户端生成会同步给服务端，容易出两份）
                swing.ServerLootOrigins.Add(origin);
                return;
            }
            //箱子：内容物与箱子本体一起取，方块也在这里被破坏（内部按左上角去重）
            List<Item>? chestLoot = TakeChestLoot(tile, x, y, swing.ProcessedChests);
            if (chestLoot is not null)
            {
                foreach (Item item in chestLoot)
                {
                    if (item is { IsAir: false, stack: > 0 })
                    {
                        swing.Drops.Add(item);
                    }
                }
                swing.TilesProcessed++;
                return;
            }
            //“破坏时现算随机奖励”的方块：奖励不在掉落表里且无视 noItem / CanDrop，
            //整段放进收集窗口破坏才会进本次结算。
            if (isPot)
            {
                BreakTileWithDrops(ox, oy, swing.Drops);
                swing.TilesProcessed++;
                return;
            }
            //其它方块（含原版家具与模组方块）：三条路依次尝试，确保“方块没了却什么都不给”不会发生
            int before = swing.Drops.Count;
            //第 1 条路：原版掉落函数 / tModLoader 掉落生成器（普通方块、模组方块、箱子本体）
            GenerateTileDrops(ox, oy, swing.Drops);
            if (swing.Drops.Count == before)
            {
                //第 2 条路：让引擎自己破坏并掉落（原版家具的掉落只在这条路径里产生），收集窗口接住
                BreakTileWithDrops(ox, oy, swing.Drops);
            }
            else
            {
                //已经拿到掉落：屏蔽掉落破坏方块，避免第二份；窗口留着接住无视屏蔽的那类方块
                BreakTile(ox, oy, noItem: true, sink: swing.Drops);
            }
            if (swing.Drops.Count == before)
            {
                //第 3 条路：兜底预测（ModTile.GetItemDrops / 原版掉落表）；
                //额外掉落统一在下面套用一次，这里不要再传（否则会加倍两次）
                swing.Drops.AddRange(GetTileItemDrops(ox, oy));
                BreakTile(ox, oy, noItem: true, sink: swing.Drops);
            }
            if (swing.Drops.Count > before)
            {
                //额外掉落：由这件工具给出（“矿石 ×N”或任何其它物块的加成，库不认识矿石）
                ApplyExtraDrops(swing.Drops, before, tileType, swing.Tool?.ExtraDropModifier);
            }
            swing.TilesProcessed++;
        }
        /// <summary>
        /// 结束一次范围挖掘：把需要服务端结算的坐标发出去，并把本地收集到的掉落交付出去。
        /// </summary>
        public static void FinishAoeSwing(AoeSwing swing, Vector2 clusterPosition)
        {
            //记下“刚挥完”与发起者：之后延迟冒出来、没有窗口接住的掉落要靠它认出来并交付
            LastAoeSwingTick = Main.GameUpdateCount;
            _hasAoeSwung = true;
            _lastSwingPlayerIndex = swing.Player?.whoAmI ?? -1;
            //抹墙放在“结算方块”之前：服务端先抹墙并同步，之后的地形同步里墙已经是空的，
            //否则客户端会先收到一份“带墙”的地形，墙体闪一下再消失
            if (swing.ServerWallArea is { } wallArea)
            {
                BreakNet.RequestServerKillWalls(wallArea, clusterPosition);
            }
            if (swing.ServerLootOrigins is { Count: > 0 })
            {
                BreakNet.RequestLootTiles(swing.ServerLootOrigins, clusterPosition);
            }
            if (swing.ServerBreakOrigins is { Count: > 0 })
            {
                BreakNet.RequestServerKillTiles(swing.ServerBreakOrigins, noItem: true);
            }
            //服务端：主动同步整片挖掘区域的地形。服务端用 WorldGen.KillTile 破坏方块不会自动同步，
            //而让客户端发 SendTileSquare 会把客户端那份（可能过期的）地形报上来。
            if (Main.netMode == NetmodeID.Server)
            {
                int centerX = (int)(clusterPosition.X / 16f);
                int centerY = (int)(clusterPosition.Y / 16f);
                NetMessage.SendTileSquare(-1, centerX - 14, centerY - 14, 28, 28);
            }
            Deliver(swing.Drops, clusterPosition, swing.Player, swing.Tool);
        }
        /// <summary>
        /// 取出一个箱子的内容物（不含箱子本体）并清空箱子物品栏。
        /// <para>必须真正清空：<see cref="Chest.DestroyChest"/> 在箱内还有物品时返回 false，
        /// 而 <c>WorldGen.KillTile</c> 会据此判定“该格应当存活”——箱子将永远挖不掉，
        /// 且每次挥动都会把同一批物品再取一次。</para>
        /// </summary>
        private static List<Item> ExtractChestContents(Chest chest)
        {
            List<Item> loot = [];
            Item[] slots = chest.item;
            for (int i = 0; i < slots.Length; i++)
            {
                Item slot = slots[i];
                if (slot.IsAir || slot.stack <= 0)
                {
                    continue;
                }
                loot.Add(slot.Clone(clone => clone.maxStack));
                //真正清空物品栏：只做快照会让 DestroyChest 一直失败
                slots[i] = new Item();
            }
            return loot;
        }
        /// <summary>
        /// 取一格方块“本体”的掉落（模组方块走 <see cref="ModTile.GetItemDrops"/>，原版方块走原版掉落表，
        /// 箱子本体也在这里取到）。
        /// <para>必须在方块尚未被破坏时调用：既要读类型来套 <paramref name="extraDrops"/>，
        /// 原版掉落表也要读帧坐标。</para>
        /// </summary>
        /// <param name="extraDrops">额外掉落表（见 <see cref="IAoeMiningTool.ExtraDropModifier"/>）；null = 不加成</param>
        public static List<Item> GetTileItemDrops(int x, int y, IReadOnlyDictionary<bool[], Int32Modifier>? extraDrops = null)
        {
            List<Item> drops = [];
            if (!InBounds(x, y))
            {
                return drops;
            }
            Tile tile = Framing.GetTileSafely(x, y);
            if (!tile.HasTile)
            {
                return drops;
            }
            if (TileLoader.GetTile(tile.TileType) is { } modTile)
            {
                //ModTile.GetItemDrops 可能返回 null（多格物块的子格、或第三方直接 return null），
                //不判空会抛异常并中断整次挥动的结算（已收集的掉落会全部消失）
                try
                {
                    if (modTile.GetItemDrops(x, y) is { } modDrops)
                    {
                        foreach (Item item in modDrops)
                        {
                            if (item is { IsAir: false, stack: > 0 })
                            {
                                //用 CloneItem 而不是 Clone：Item.Clone 会按 SetDefaults 重置 maxStack
                                drops.Add(item.Clone(clone => clone.maxStack));
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    Error($"ModTile.GetItemDrops failed ({modTile.Name} @({x},{y})): {exception.GetType().Name} {exception.Message}");
                }
            }
            else
            {
                try
                {
                    if (TryGetDrop(x, y, tile, out int itemType, out int stack))
                    {
                        drops.Add(new Item(itemType, stack));
                    }
                }
                catch (Exception exception)
                {
                    Error($"Vanilla drop table failed (type={tile.TileType} @({x},{y})): {exception.GetType().Name} {exception.Message}");
                }
            }
            ApplyExtraDrops(drops, 0, tile.TileType, extraDrops);
            return drops;
        }
        /// <summary>
        /// 取出箱子（内容物 + 箱子本体）并彻底破坏它；该位置不是箱子时返回 null。
        /// <para>单人 / 服务端就地取内容并破坏，内容由调用方并入本次结算；多人客户端只发请求，
        /// 因为箱子内容物是服务端权威数据（没打开过的箱子在本地是空的）。</para>
        /// <para>同一个箱子会被相邻格子多次命中，用 <paramref name="processedChests"/> 在本次挖掘内去重。</para>
        /// </summary>
        public static List<Item>? TakeChestLoot(Tile tile, int x, int y, HashSet<int> processedChests)
        {
            //只有箱子类方块才走这条流程：FindChestByGuessing 会在附近有箱子时把任意格子猜成箱子，
            //一旦返回非 null，这一格自己的掉落就会被当成“箱子已处理”而跳过
            if (!InBounds(x, y) || !Main.tileContainer[tile.TileType])
            {
                return null;
            }
            int chestIndex = FindChestIndex(tile, x, y);
            if (chestIndex < 0 || chestIndex >= Main.chest.Length || Main.chest[chestIndex] is not { } chest)
            {
                return null;
            }
            if (!processedChests.Add(chestIndex))
            {
                //本次挖掘已处理过这个箱子：返回空列表表示“是箱子但不要再产出掉落”
                return [];
            }
            int chestX = chest.x;
            int chestY = chest.y;
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                BreakNet.RequestChestBreak(chestX, chestY);
                return [];
            }
            List<Item> loot = ExtractChestContents(chest);
            ClearAndBreakChest(chest, chestIndex, chestX, chestY, dropContents: false, lootSink: loot);
            return loot;
        }
        /// <summary>
        /// 服务端侧：取出箱子内容物并破坏箱子，再把内容交付出去
        /// （服务端生成的物品会由原版自动同步给所有客户端）。
        /// <para>多人下必须由服务端来完成“取内容 + 掉落”：客户端在从未打开过该箱子时
        /// 本地副本是空的，靠客户端取内容会造成“箱子被破坏但物品全部丢失”。</para>
        /// </summary>
        public static void LootAndBreakChestOnServer(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            Tile tile = Framing.GetTileSafely(x, y);
            int chestIndex = FindChestIndex(tile, x, y);
            if (chestIndex < 0 || chestIndex >= Main.chest.Length || Main.chest[chestIndex] is not { } chest)
            {
                return;
            }
            int chestX = chest.x;
            int chestY = chest.y;
            List<Item> loot = ExtractChestContents(chest);
            ClearAndBreakChest(chest, chestIndex, chestX, chestY, dropContents: false, lootSink: loot);
            Deliver(loot, new Vector2(chestX * 16f + 16f, chestY * 16f + 16f));
        }
        /// <summary>按帧坐标（多格以左上角为准）定位箱子下标；该格不是箱子类方块、或找不到时返回 -1。</summary>
        public static int FindChestIndex(Tile tile, int x, int y)
        {
            if (!Main.tileContainer[tile.TileType])
            {
                return -1;
            }
            Point16 origin = GetMultiTileOrigin(tile, x, y);
            int chestIndex = Chest.FindChest(origin.X, origin.Y);
            if (chestIndex < 0)
            {
                //兜底：帧坐标不是标准 2x2 时（例如别的模组的箱子）按邻近格子猜一次
                chestIndex = Chest.FindChestByGuessing(x, y);
            }
            return chestIndex;
        }
        /// <summary>本地清理：清空箱子副本并破坏方块（不产出掉落，内容物由服务端负责）。</summary>
        public static void ClearAndBreakChestAt(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return;
            }
            Tile tile = Framing.GetTileSafely(x, y);
            int chestIndex = FindChestIndex(tile, x, y);
            if (chestIndex < 0 || chestIndex >= Main.chest.Length || Main.chest[chestIndex] is not { } chest)
            {
                return;
            }
            ClearAndBreakChest(chest, chestIndex, chest.x, chest.y, dropContents: false);
        }

        /// <summary>
        /// 清空箱子物品栏并破坏箱子占用的多格物块。
        /// </summary>
        /// <param name="chest">箱子实例</param>
        /// <param name="chestIndex">箱子在 <see cref="Main.chest"/> 中的下标</param>
        /// <param name="dropContents">是否把内容物直接掉落到世界中（调用方不想自己处理掉落时用）</param>
        /// <param name="lootSink">
        /// 非 null 时用原版掉落函数把箱子本体的掉落收进这里（必须在清空数据之前算，函数要读帧坐标判断风格）；
        /// null 表示不需要这份掉落（例如多人客户端只清理本地副本）。
        /// </param>
        public static void ClearAndBreakChest(Chest chest, int chestIndex, int chestX, int chestY, bool dropContents, List<Item>? lootSink = null)
        {
            //先清空内容物：DestroyChest 在箱内还有物品时会失败，方块也就破坏不掉
            Item[] slots = chest.item;
            for (int i = 0; i < slots.Length; i++)
            {
                Item slot = slots[i];
                if (slot.IsAir || slot.stack <= 0)
                {
                    continue;
                }
                if (dropContents)
                {
                    SpawnDrop(chestX, chestY, slot.Clone());
                }
                slots[i] = new Item();
            }
            if (!Chest.DestroyChest(chestX, chestY) && chestIndex >= 0 && chestIndex < Main.chest.Length)
            {
                //兜底：直接清掉数据槽，避免箱子永远处于“不可破坏”状态
                Main.chest[chestIndex] = null;
            }
            if (lootSink is null)
            {
                BreakChestTiles(chestX, chestY);   // 没有 sink 时不需要箱子本体掉落
                return;
            }
            //箱子本体：先问原版掉落函数；拿不到就让引擎自己掉一份并用窗口接住（此时数据已清空，破坏得掉）
            int before = lootSink.Count;
            GenerateTileDrops(chestX, chestY, lootSink);
            if (lootSink.Count == before)
            {
                BreakChestTiles(chestX, chestY, lootSink);
                if (lootSink.Count == before)
                {
                    lootSink.AddRange(GetTileItemDrops(chestX, chestY));   // 最后兜底：预测一份
                }
                return;
            }
            BreakChestTiles(chestX, chestY);
        }
        /// <summary>
        /// 破坏箱子占用的多格物块（默认屏蔽掉落：本体掉落已由 <see cref="GenerateTileDrops"/> 结算）。
        /// <para>必须以左上角坐标调用；箱子数据清空后 <c>KillTile</c> 才会真正破坏它。</para>
        /// </summary>
        public static void BreakChestTiles(int originX, int originY) => BreakChestTiles(originX, originY, null);

        /// <summary>
        /// 破坏箱子占用的多格物块。
        /// </summary>
        /// <param name="sink">
        /// 非 null 时用“带掉落的破坏”把掉落收进它（原版掉落函数算不出箱子本体时的兜底）；
        /// null 则屏蔽掉落。
        /// </param>
        public static void BreakChestTiles(int originX, int originY, List<Item>? sink)
        {
            if (sink is null)
            {
                BreakTile(originX, originY);
            }
            else
            {
                BreakTileWithDrops(originX, originY, sink);
            }
            for (int dx = 0; dx < 2; dx++)
            {
                for (int dy = 0; dy < 2; dy++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }
                    int x = originX + dx;
                    int y = originY + dy;
                    if (!InBounds(x, y) || !Framing.GetTileSafely(x, y).HasTile)
                    {
                        continue;
                    }
                    if (sink is null)
                    {
                        BreakTile(x, y);
                    }
                    else
                    {
                        BreakTileWithDrops(x, y, sink);
                    }
                }
            }
        }
    }
}