namespace EternalLib
{
    /// <summary>
    /// 可拖拽面板的契约（原 <c>DragUIState&lt;T&gt;</c> 抽象类改为“接口 + 组合”）。
    /// <para>继承 <see cref="UIState"/> 是 tModLoader 的硬性要求、无法去掉，所以这里把<b>拖拽能力</b>从继承链上摘下来：
    /// UI 只实现本接口，并把行为委托给组合进来的 <see cref="DragPanel"/>（<see cref="DragPanelController{T}"/>）。</para>
    /// <para>除 <see cref="DragPanel"/> 与 <see cref="State"/> 外，其余成员都带默认实现，不需要自定义就一行都不用写。</para>
    /// </summary>
    public interface IDraggablePanel<T> where T : UIElement
    {
        /// <summary>拖拽控制器（组合：由 UI 在构造函数里 <c>new(...this)</c> 创建）。</summary>
        DragPanelController<T> DragPanel { get; }
        /// <summary>返回 <c>this</c>：控制器需要访问 <see cref="UIState.Children"/> 这类框架成员。</summary>
        UIState State { get; }
        /// <summary>显示标记，由 <see cref="DragPanelHost.Show"/> 打开时置为 true。</summary>
        bool Visible { get => DragPanel.Visible; set => DragPanel.Visible = value; }
        /// <summary>可拖拽的根元素。</summary>
        T? Element { get => DragPanel.Element; set => DragPanel.Element = value; }
        /// <summary>当前是否正在拖拽（子元素可用于屏蔽点击）。</summary>
        bool IsDragging => DragPanel.IsDragging;
        /// <summary>界面请求关闭（Esc 或 <see cref="RequestClose"/>）；由 <see cref="DragPanelHost"/> 接管。</summary>
        event Action? CloseRequested
        {
            add => DragPanel.CloseRequested += value;
            remove => DragPanel.CloseRequested -= value;
        }
        /// <summary>请求关闭界面（由宿主系统执行真正的关闭）。</summary>
        void RequestClose() => DragPanel.RequestClose();
        /// <summary>取消当前拖拽与回弹。</summary>
        void CancelDrag() => DragPanel.CancelDrag();
        /// <summary>把面板立即拉回屏幕可见范围内。</summary>
        void ClampIntoView() => DragPanel.ClampIntoView();
        /// <summary>从 <see cref="UIPositionStore"/> 恢复位置（需要设置 <see cref="PositionKey"/>）。</summary>
        void RestorePosition() => DragPanel.RestorePosition();
        /// <summary>把当前位置写入 <see cref="UIPositionStore"/>（需要设置 <see cref="PositionKey"/>）。</summary>
        void SavePosition() => DragPanel.SavePosition();
        /// <summary>在面板本体上拖拽所需要按住的修饰键。</summary>
        Keys DragKey => Keys.LeftShift;
        /// <summary>回弹速度的衰减系数（0~1，越小停得越快）。</summary>
        float VelocityDecay => 0.16f;
        /// <summary>允许超出屏幕的边距。</summary>
        float EdgeMargin => 10f;
        /// <summary>回弹初速度系数。</summary>
        float SpringStrength => 0.2f;
        /// <summary>在面板本体（非 <see cref="DragHandle"/>）上拖拽时是否必须按住 <see cref="DragKey"/>。</summary>
        bool RequireModifierKey => true;
        /// <summary>拖拽把手（通常是标题栏）；设置后在把手上按下即可直接拖动。</summary>
        UIElement? DragHandle => null;
        /// <summary>边缘吸附距离（0 = 不吸附）。</summary>
        float SnapDistance => 0f;
        /// <summary>是否允许 Esc 关闭（默认关闭，避免与宿主自己的 Esc 处理重复触发）。</summary>
        bool CloseOnEscape => false;
        /// <summary>位置持久化键；设置后打开 / 关闭界面时自动存取面板位置。</summary>
        string? PositionKey => null;
        /// <summary>拖拽期间是否让面板及其全部子元素忽略鼠标，避免拖面板时误触内部按钮或物品格。</summary>
        bool BlockChildInputWhileDragging => true;
        /// <summary>拖拽开始（UI 可据此暂停自身逻辑）。</summary>
        void OnDragStarted() { }
        /// <summary>拖拽结束。</summary>
        void OnDragEnded() { }
    }
}