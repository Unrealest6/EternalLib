namespace EternalLib
{
    /// <summary>
    /// 单个面板宿主的全部状态与行为：界面层级、显示 / 隐藏、每帧更新、绘制、拖拽收尾。
    /// <para>原 <c>DragUISystem&lt;T, TState&gt;</c> 用两个泛型参数（其中一个是 CRTP 自约束）来让“每个 UI 各有一份静态状态”；
    /// 现在改成普通类实例：一个宿主一份状态，由 <see cref="DragPanelSystem"/> 统一驱动，不再需要任何泛型。</para>
    /// </summary>
    public sealed class DragPanelHost
    {
        private static readonly List<DragPanelHost> All = [];
        private readonly GameTime _uiGameTime = new();
        /// <param name="key">唯一标识，用作插入的界面层名字（各宿主不要重复）。</param>
        public DragPanelHost(string key)
        {
            Key = key;
            Interface = new UserInterface();
            All.Add(this);
        }
        /// <summary>唯一标识，用作插入的界面层名字。</summary>
        public string Key { get; }
        /// <summary>
        /// 插入到哪一层原版界面之前（默认 <c>"Mouse Text"</c>）。
        /// <para>注意与 <see cref="Key"/> 区分：这里是<b>原版层名</b>，写错就找不到锚点，界面不会被绘制。</para>
        /// </summary>
        public string Anchor { get; init; } = "Mouse Text";
        /// <summary>本宿主独占的 <see cref="UserInterface"/>。</summary>
        public UserInterface Interface { get; }
        /// <summary>当前显示的面板（没有则为 null）。</summary>
        public UIState? Current { get; private set; }
        /// <summary>是否有面板正在显示。</summary>
        public bool IsOpen => Current is not null;
        /// <summary>当前面板（按接口形态访问；非拖拽面板时为 null）。</summary>
        public IDraggablePanel<UIPanel>? CurrentPanel => Current as IDraggablePanel<UIPanel>;
        /// <summary>显示面板（会先关掉上一个）。</summary>
        public void Show(UIState panel)
        {
            if (Current is not null && !ReferenceEquals(Current, panel))
            {
                Hide(playSound: false);
            }
            Current = panel;
            if (CurrentPanel is { } draggable)
            {
                draggable.Visible = true;
                //让 Esc / 关闭按钮统一回到这里，面板自己不必再各自实现
                draggable.CloseRequested -= HideFromRequest;
                draggable.CloseRequested += HideFromRequest;
            }
            panel.Activate();
            Interface.SetState(panel);
        }
        /// <summary>隐藏当前面板。</summary>
        public void Hide(bool playSound = true)
        {
            if (Current is not null)
            {
                if (CurrentPanel is { } draggable)
                {
                    draggable.Visible = false;
                    draggable.CloseRequested -= HideFromRequest;
                    draggable.CancelDrag();
                }
                Current = null;
            }
            Interface.SetState(null);
            if (playSound)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
            }
        }
        private void HideFromRequest() => Hide();
        internal void Update(GameTime gameTime) => Interface.Update(gameTime);
        internal void Draw(SpriteBatch spriteBatch) => Interface.Draw(spriteBatch, _uiGameTime);
        internal void DiscardPendingClicks() => DragUISession.DiscardPendingClicks(Interface);
        /// <summary>卸载时清理：取消拖拽并断开界面状态。</summary>
        internal void Reset()
        {
            CurrentPanel?.CancelDrag();
            Interface.SetState(null);
            Current = null;
        }
        internal static IReadOnlyList<DragPanelHost> Instances => All;
        internal static void ClearAll() => All.Clear();
    }
}
