namespace EternalLib
{
    /// <summary>
    /// 面板位置（像素 + 百分比权重），可完整还原一个 <see cref="StyleDimension"/> 对。
    /// </summary>
    public readonly record struct UIPanelPosition(float LeftPixels, float TopPixels, float LeftPercent = 0f, float TopPercent = 0f)
    {
        /// <summary>从元素当前布局读取位置。</summary>
        public static UIPanelPosition From(UIElement element)
            => new(element.Left.Pixels, element.Top.Pixels, element.Left.Percent, element.Top.Percent);
        /// <summary>写回元素布局。</summary>
        public void ApplyTo(UIElement element)
        {
            element.Left.Set(LeftPixels, LeftPercent);
            element.Top.Set(TopPixels, TopPercent);
        }
        /// <summary>数值是否有效（防止存档被写坏后把面板扔到看不见的地方）。</summary>
        public bool IsValid
            => float.IsFinite(LeftPixels) && float.IsFinite(TopPixels)
               && float.IsFinite(LeftPercent) && float.IsFinite(TopPercent);
    }
    /// <summary>
    /// 把 UI 面板位置存进<b>角色存档</b>（<see cref="ModPlayer.SaveData"/>）。
    /// <para>适合“全局性”的客户端界面：位置跟着角色走，单人与多人都不需要服务器参与，
    /// 也不会污染世界存档。像工作台那样“绑定某个物块实体”的界面，仍应把位置写进对应的
    /// <c>TileEntity</c>（AvaritiaMod 现有做法）。</para>
    /// </summary>
    public sealed class UIPositionStore : ModPlayer
    {
        private const string KeyPrefix = "EternalLibUIPositions";
        private readonly Dictionary<string, UIPanelPosition> _positions = [];
        /// <summary>当前本地玩家的位置存储；玩家不可用时返回 null。</summary>
        public static UIPositionStore? Local
        {
            get
            {
                try
                {
                    Player? player = Main.LocalPlayer;
                    return player?.GetModPlayer<UIPositionStore>();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
        /// <summary>已记录的面板数量。</summary>
        public static int Count => Local?._positions.Count ?? 0;
        public static bool TryGet(string key, out UIPanelPosition position)
        {
            UIPositionStore? store = Local;
            if (store is not null && !string.IsNullOrEmpty(key))
            {
                return store._positions.TryGetValue(key, out position);
            }
            position = default;
            return false;
        }
        public static void Set(string key, UIPanelPosition position)
        {
            UIPositionStore? store = Local;
            if (store is null || string.IsNullOrEmpty(key) || !position.IsValid)
            {
                return;
            }
            store._positions[key] = position;
        }
        public static bool Remove(string key)
        {
            UIPositionStore? store = Local;
            return store is not null && !string.IsNullOrEmpty(key) && store._positions.Remove(key);
        }
        /// <summary>清空所有已记录的面板位置（例如提供“重置界面布局”按钮）。</summary>
        public static void Clear() => Local?._positions.Clear();
        public override void SaveData(TagCompound tag)
        {
            if (_positions.Count == 0)
            {
                return;
            }
            List<string> keys = [with(_positions.Count)];
            List<float> values = [with(_positions.Count * 4)];
            foreach ((string key, UIPanelPosition position) in _positions)
            {
                if (string.IsNullOrEmpty(key) || !position.IsValid)
                {
                    continue;
                }
                keys.Add(key);
                values.Add(position.LeftPixels);
                values.Add(position.TopPixels);
                values.Add(position.LeftPercent);
                values.Add(position.TopPercent);
            }
            tag[KeyPrefix + "Keys"] = keys;
            tag[KeyPrefix + "Values"] = values;
        }
        public override void LoadData(TagCompound tag)
        {
            _positions.Clear();
            try
            {
                if (!tag.ContainsKey(KeyPrefix + "Keys") || !tag.ContainsKey(KeyPrefix + "Values"))
                {
                    return;
                }
                IList<string> keys = tag.GetList<string>(KeyPrefix + "Keys");
                IList<float> values = tag.GetList<float>(KeyPrefix + "Values");
                for (int i = 0, v = 0; i < keys.Count && v + 3 < values.Count; i++, v += 4)
                {
                    UIPanelPosition position = new(values[v], values[v + 1], values[v + 2], values[v + 3]);
                    if (position.IsValid && !string.IsNullOrEmpty(keys[i]))
                    {
                        _positions[keys[i]] = position;
                    }
                }
            }
            catch (Exception ex)
            {
                EternalLog.Error($"读取 UI 面板位置失败，已丢弃：{ex.Message}");
                _positions.Clear();
            }
        }
    }
}