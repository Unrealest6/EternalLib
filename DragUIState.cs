namespace EternalLib
{
    /// <summary>
    /// 可拖拽 UI 状态基类：派生类在 <c>OnInitialize</c> 中创建根元素并赋给 <see cref="Element"/>，拖拽、越界回弹与松手贴边由基类处理。
    /// 可选能力见 <see cref="DragHandle"/>、<see cref="RequireModifierKey"/>、<see cref="SnapDistance"/>、<see cref="CloseOnEscape"/>、<see cref="PositionKey"/>、<see cref="BlockChildInputWhileDragging"/>。
    /// </summary>
    public abstract class DragUIState<T> : UIState where T : UIElement
    {
        /// <summary>显示标记，由 <see cref="DragUISystem{T,TState}.ShowUI()"/> 打开时置为 true。</summary>
        public bool Visible { get; set; }
        /// <summary>可拖拽的根元素。</summary>
        protected T? Element { get; set; }
        /// <summary>在面板本体上拖拽所需要按住的修饰键。</summary>
        protected virtual Keys Key => Keys.LeftShift;
        /// <summary>回弹速度的衰减系数（0~1，越小停得越快）。</summary>
        protected virtual float VelocityDecay => 0.16f;
        /// <summary>允许超出屏幕的边距。</summary>
        protected virtual float EdgeMargin => 10f;
        /// <summary>回弹初速度系数。</summary>
        protected virtual float SpringStrength => 0.2f;
        /// <summary>
        /// 在面板本体（非 <see cref="DragHandle"/>）上拖拽时是否必须按住 <see cref="Key"/>。
        /// <para>设为 <c>false</c> 时必须同时设置 <see cref="DragHandle"/>，否则面板内任意点击都会被当成拖拽起点；落在子元素上的按下永不起拖（见 <see cref="IsOverChild"/>）。</para>
        /// </summary>
        protected virtual bool RequireModifierKey => true;
        /// <summary>
        /// 拖拽把手（通常是标题栏）。设置后，在把手上按下即可直接拖动，不需要修饰键。
        /// </summary>
        protected virtual UIElement? DragHandle => null;
        /// <summary>
        /// 边缘吸附距离：大于 0 时，松手位置距离屏幕边缘不超过该值就会贴边对齐。
        /// </summary>
        protected virtual float SnapDistance => 0f;
        /// <summary>是否允许 Esc 关闭。默认关闭，避免与宿主自己实现的 Esc 处理重复触发。</summary>
        protected virtual bool CloseOnEscape => false;
        /// <summary>
        /// 位置持久化键。设置后，界面打开时自动从 <see cref="UIPositionStore"/> 恢复位置，关闭时自动保存。
        /// <para>派生类重写 <c>OnActivate</c>/<c>OnDeactivate</c> 时必须调用 base，否则本项失效。</para>
        /// </summary>
        protected virtual string? PositionKey => null;
        /// <summary>
        /// 拖拽期间是否让面板及其全部子元素完全忽略鼠标，避免拖面板时误触内部按钮或物品格。
        /// <para>tModLoader 的 <c>UIElement.GetElementAt</c> 会跳过带 <c>IgnoresMouseInteraction</c> 的元素及其整棵子树，故只需置位根元素。</para>
        /// </summary>
        protected virtual bool BlockChildInputWhileDragging => true;
        /// <summary>界面请求关闭（Esc 或调用 <see cref="RequestClose"/>）。由 <see cref="DragUISystem{T,TState}"/> 接管。</summary>
        public event Action? CloseRequested;
        /// <summary>当前是否正在拖拽（派生类可用于屏蔽点击）。</summary>
        public bool IsDragging { get; private set; }
        private bool _dragButtonHeld;
        private bool _escapeWasDown;
        private bool _sessionNotified;
        private bool _panelInputIgnored;
        private Vector2 _lastMouse;
        private bool _isRebounding;
        private Vector2 _reboundVelocity;
        /// <summary>
        /// 当前可拖拽的根元素。未显式赋值 <see cref="Element"/> 时自动取第一个类型匹配的子元素。
        /// </summary>
        protected T? RootElement
        {
            get
            {
                if (Element is not null)
                {
                    return Element;
                }
                foreach (UIElement child in Children)
                {
                    if (child is not T typed)
                    {
                        continue;
                    }
                    Element = typed;
                    return typed;
                }
                return null;
            }
        }
        public override void OnActivate() => RestorePosition();
        public override void OnDeactivate()
        {
            SavePosition();
            CancelDrag();
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            T? element = RootElement;
            if (element is null)
            {
                CancelDrag();
                return;
            }
            UpdateCloseRequest();
            Vector2 mouse = Main.MouseScreen;
            if (_isRebounding)
            {
                ApplyVelocityLerpRebound(element);
            }
            bool overHandle = DragHandle?.IsMouseHovering ?? false;
            bool overPanel = element.IsMouseHovering;
            if (overHandle || overPanel || IsDragging)
            {
                // 鼠标在面板上（或正在拖拽）时交还 UI 占用标记，否则点击面板会顺手用掉手里的物品。
                Main.LocalPlayer.mouseInterface = true;
            }
            bool buttonDown = Main.mouseLeft;
            if (IsDragging)
            {
                // 拖拽一旦开始就只取决于按键是否按住：鼠标即使移出面板、甚至面板已忽略鼠标，也不会中断拖拽。
                if (buttonDown)
                {
                    MoveElement(element, mouse);
                }
                else
                {
                    EndDrag(element, snap: true);
                }
            }
            else if (buttonDown && !_dragButtonHeld)
            {
                // 面板本体起拖要求鼠标下不是子元素：StartDrag 会丢弃按下缓存（见 DragUISession.DiscardPendingClicks），否则这次点击不会被派发（如按住 Shift 点合成槽无反应）；把手不受此限制。
                bool canStart = overHandle || (overPanel && !IsOverChild(element) && (!RequireModifierKey || Main.keyState.IsKeyDown(Key)));
                if (canStart)
                {
                    StartDrag(element, mouse);
                }
                _dragButtonHeld = true;
            }
            else if (!buttonDown)
            {
                _dragButtonHeld = false;
                if (!_isRebounding)
                {
                    CheckEdgeCollision(element);
                }
            }
        }
        /// <summary>请求关闭界面（由宿主系统执行真正的关闭）。</summary>
        public void RequestClose() => CloseRequested?.Invoke();
        /// <summary>取消当前拖拽与回弹（关闭 UI 时调用，避免下次打开时残留状态）。</summary>
        public void CancelDrag()
        {
            ReleaseDragClaims();
            IsDragging = false;
            _dragButtonHeld = false;
            _isRebounding = false;
            _reboundVelocity = Vector2.Zero;
        }
        /// <summary>从 <see cref="UIPositionStore"/> 恢复位置（需要设置 <see cref="PositionKey"/>）。</summary>
        public void RestorePosition()
        {
            if (PositionKey is not { Length: > 0 } key || RootElement is not { } element)
            {
                return;
            }
            if (!UIPositionStore.TryGet(key, out UIPanelPosition position))
            {
                return;
            }
            position.ApplyTo(element);
            element.Recalculate();
        }
        /// <summary>把当前位置写入 <see cref="UIPositionStore"/>（需要设置 <see cref="PositionKey"/>）。</summary>
        public void SavePosition()
        {
            if (PositionKey is not { Length: > 0 } key || RootElement is not { } element)
            {
                return;
            }
            UIPositionStore.Set(key, UIPanelPosition.From(element));
        }
        /// <summary>拖拽开始时调用（派生类可重写以暂停自身逻辑）。</summary>
        protected virtual void OnDragStarted() { }
        /// <summary>拖拽结束时调用。</summary>
        protected virtual void OnDragEnded() { }
        /// <summary>把面板立即拉回屏幕可见范围内。</summary>
        public void ClampIntoView()
        {
            T? element = RootElement;
            if (element is null)
            {
                return;
            }
            CalculatedStyle dims = element.GetDimensions();
            float maxX = Math.Max(EdgeMargin, Main.screenWidth - dims.Width - EdgeMargin);
            float maxY = Math.Max(EdgeMargin, Main.screenHeight - dims.Height - EdgeMargin);
            Vector2 target = new(Math.Clamp(dims.X, EdgeMargin, maxX), Math.Clamp(dims.Y, EdgeMargin, maxY));
            Vector2 delta = target - new Vector2(dims.X, dims.Y);
            if (delta == Vector2.Zero)
            {
                return;
            }
            element.Left.Pixels += delta.X;
            element.Top.Pixels += delta.Y;
            element.Recalculate();
        }
        /// <summary>
        /// 鼠标是否停在面板内的某个直接子元素上。
        /// <para><c>IsMouseHovering</c> 会沿父链向上传递，鼠标落在更深层元素上时包含它的每层都会为 <c>true</c>，故只查直接子元素即可。</para>
        /// </summary>
        private static bool IsOverChild(UIElement element)
        {
            foreach (UIElement child in element.Children)
            {
                if (!child.IgnoresMouseInteraction && child.IsMouseHovering)
                {
                    return true;
                }
            }
            return false;
        }
        private void StartDrag(T element, Vector2 mouse)
        {
            _isRebounding = false;
            _reboundVelocity = Vector2.Zero;
            IsDragging = true;
            _dragButtonHeld = true;
            _lastMouse = mouse;
            if (!_sessionNotified)
            {
                _sessionNotified = true;
                DragUISession.NotifyDragStarted();
            }
            if (BlockChildInputWhileDragging && !element.IgnoresMouseInteraction)
            {
                element.IgnoresMouseInteraction = true;
                _panelInputIgnored = true;
            }
            OnDragStarted();
        }
        private void EndDrag(T element, bool snap)
        {
            if (snap)
            {
                SnapToEdge(element);
            }
            IsDragging = false;
            ReleaseDragClaims();
            OnDragEnded();
        }
        /// <summary>归还全局拖拽计数与子元素输入屏蔽。</summary>
        private void ReleaseDragClaims()
        {
            if (_sessionNotified)
            {
                _sessionNotified = false;
                DragUISession.NotifyDragEnded();
            }
            if (_panelInputIgnored && RootElement is { } element)
            {
                element.IgnoresMouseInteraction = false;
            }
            _panelInputIgnored = false;
        }
        private void MoveElement(T element, Vector2 mouse)
        {
            Vector2 delta = mouse - _lastMouse;
            _lastMouse = mouse;
            element.Left.Pixels += delta.X;
            element.Top.Pixels += delta.Y;
            element.Recalculate();
        }
        /// <summary>Esc 按下边沿（避免长按期间反复请求关闭）。</summary>
        private void UpdateCloseRequest()
        {
            bool escapeDown = Main.keyState.IsKeyDown(Keys.Escape);
            if (CloseOnEscape && escapeDown && !_escapeWasDown)
            {
                RequestClose();
            }
            _escapeWasDown = escapeDown;
        }
        /// <summary>松手时贴近屏幕边缘就贴边对齐。</summary>
        private void SnapToEdge(T element)
        {
            if (SnapDistance <= 0f)
            {
                return;
            }
            CalculatedStyle dims = element.GetDimensions();
            float targetX = dims.X;
            float targetY = dims.Y;
            if (dims.X <= SnapDistance)
            {
                targetX = EdgeMargin;
            }
            else if (dims.X + dims.Width >= Main.screenWidth - SnapDistance)
            {
                targetX = Main.screenWidth - dims.Width - EdgeMargin;
            }
            if (dims.Y <= SnapDistance)
            {
                targetY = EdgeMargin;
            }
            else if (dims.Y + dims.Height >= Main.screenHeight - SnapDistance)
            {
                targetY = Main.screenHeight - dims.Height - EdgeMargin;
            }
            if (targetX.IsWithinTolerance(dims.X, 0.1f) && targetY.IsWithinTolerance(dims.Y, 0.1f))
            {
                return;
            }
            element.Left.Pixels += targetX - dims.X;
            element.Top.Pixels += targetY - dims.Y;
            element.Recalculate();
        }
        /// <summary>
        /// 检查面板是否超出屏幕边缘，根据超出距离设置回弹初速度。
        /// </summary>
        private void CheckEdgeCollision(T element)
        {
            CalculatedStyle dims = element.GetDimensions();
            float panelW = dims.Width;
            float panelH = dims.Height;
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            float left = dims.X;
            float right = left + panelW;
            float top = dims.Y;
            float bottom = top + panelH;
            Vector2 initialVelocity = Vector2.Zero;
            bool needRebound = false;
            if (left < -EdgeMargin)
            {
                float overflow = Math.Abs(left) - EdgeMargin;
                initialVelocity.X = overflow * SpringStrength;
                needRebound = true;
            }
            else if (right > screenW + EdgeMargin)
            {
                float overflow = right - (screenW + EdgeMargin);
                initialVelocity.X = -overflow * SpringStrength;
                needRebound = true;
            }
            if (top < -EdgeMargin)
            {
                float overflow = Math.Abs(top) - EdgeMargin;
                initialVelocity.Y = overflow * SpringStrength;
                needRebound = true;
            }
            else if (bottom > screenH + EdgeMargin)
            {
                float overflow = bottom - (screenH + EdgeMargin);
                initialVelocity.Y = -overflow * SpringStrength;
                needRebound = true;
            }
            if (!needRebound)
            {
                return;
            }
            _isRebounding = true;
            _reboundVelocity = initialVelocity;
        }
        /// <summary>
        /// 速度 Lerp 回弹：速度平滑衰减到 0，用速度更新位置。
        /// </summary>
        private void ApplyVelocityLerpRebound(T element)
        {
            _reboundVelocity = Vector2.Lerp(_reboundVelocity, Vector2.Zero, VelocityDecay);
            element.Left.Pixels += _reboundVelocity.X;
            element.Top.Pixels += _reboundVelocity.Y;
            element.Recalculate();
            if (_reboundVelocity.Length() >= 0.3f)
            {
                return;
            }
            _reboundVelocity = Vector2.Zero;
            _isRebounding = false;
            ClampIntoView();
        }
    }
}