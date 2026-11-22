namespace EternalLib;

/// <summary>
/// 拖拽控制器：原先写在 <c>DragUIState&lt;T&gt;</c> 里的整套实现（拖拽、越界回弹、贴边、位置持久化）整体搬到这里，
/// 改成“持有宿主接口”的普通类。
/// <para>宿主只需在 <c>OnActivate</c> / <c>OnDeactivate</c> / <c>Update</c> 里各转调一次，
/// 配置从 <see cref="IDraggablePanel{T}"/> 的默认实现读取。</para>
/// </summary>
public sealed class DragPanelController<T> where T : UIElement
{
    private readonly IDraggablePanel<T> _owner;
    private bool _dragButtonHeld;
    private bool _escapeWasDown;
    private bool _sessionNotified;
    private bool _panelInputIgnored;
    private Vector2 _lastMouse;
    private bool _isRebounding;
    private Vector2 _reboundVelocity;
    /// <param name="owner">宿主面板（通常是 UI 自己：<c>new(this)</c>）。</param>
    public DragPanelController(IDraggablePanel<T> owner) => _owner = owner;
    /// <summary>显示标记。</summary>
    public bool Visible { get; set; }
    /// <summary>可拖拽的根元素。</summary>
    public T? Element { get; set; }
    /// <summary>当前是否正在拖拽。</summary>
    public bool IsDragging { get; private set; }
    /// <summary>界面请求关闭。</summary>
    public event Action? CloseRequested;
    /// <summary>请求关闭界面（由宿主系统执行真正的关闭）。</summary>
    public void RequestClose() => CloseRequested?.Invoke();
    /// <summary>UI 激活：按 <see cref="IDraggablePanel{T}.PositionKey"/> 恢复位置。</summary>
    public void OnActivate() => RestorePosition();
    /// <summary>UI 关闭：保存位置并取消拖拽与回弹。</summary>
    public void OnDeactivate()
    {
        SavePosition();
        CancelDrag();
    }
    /// <summary>每帧更新：接替原 <c>DragUIState.Update</c> 的全部逻辑（在宿主的 <c>base.Update</c> 之后调用）。</summary>
    public void Update()
    {
        T? element = ResolveRootElement();
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
        bool overHandle = _owner.DragHandle?.IsMouseHovering ?? false;
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
            // 面板本体起拖要求鼠标下不是子元素：StartDrag 会丢弃按下缓存（见 DragUISession.DiscardPendingClicks），
            // 否则这次点击不会被派发（如按住 Shift 点合成槽无反应）；把手不受此限制。
            bool canStart = overHandle || (overPanel && !IsOverChild(element)
                                                     && (!_owner.RequireModifierKey || Main.keyState.IsKeyDown(_owner.DragKey)));
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
    /// <summary>取消当前拖拽与回弹（关闭 UI 时调用，避免下次打开时残留状态）。</summary>
    public void CancelDrag()
    {
        ReleaseDragClaims();
        IsDragging = false;
        _dragButtonHeld = false;
        _isRebounding = false;
        _reboundVelocity = Vector2.Zero;
    }
    /// <summary>从 <see cref="UIPositionStore"/> 恢复位置（需要设置 <see cref="IDraggablePanel{T}.PositionKey"/>）。</summary>
    public void RestorePosition()
    {
        if (_owner.PositionKey is not { Length: > 0 } key || ResolveRootElement() is not { } element)
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
    /// <summary>把当前位置写入 <see cref="UIPositionStore"/>（需要设置 <see cref="IDraggablePanel{T}.PositionKey"/>）。</summary>
    public void SavePosition()
    {
        if (_owner.PositionKey is not { Length: > 0 } key || ResolveRootElement() is not { } element)
        {
            return;
        }
        UIPositionStore.Set(key, UIPanelPosition.From(element));
    }
    /// <summary>把面板立即拉回屏幕可见范围内。</summary>
    public void ClampIntoView()
    {
        if (ResolveRootElement() is not { } element)
        {
            return;
        }
        CalculatedStyle dims = element.GetDimensions();
        float margin = _owner.EdgeMargin;
        float maxX = Math.Max(margin, Main.screenWidth - dims.Width - margin);
        float maxY = Math.Max(margin, Main.screenHeight - dims.Height - margin);
        Vector2 target = new(Math.Clamp(dims.X, margin, maxX), Math.Clamp(dims.Y, margin, maxY));
        Vector2 delta = target - new Vector2(dims.X, dims.Y);
        if (delta == Vector2.Zero)
        {
            return;
        }
        element.Left.Pixels += delta.X;
        element.Top.Pixels += delta.Y;
        element.Recalculate();
    }
    /// <summary>当前可拖拽的根元素：未显式赋值时取第一个类型匹配的子元素。</summary>
    private T? ResolveRootElement()
    {
        if (Element is not null)
        {
            return Element;
        }
        foreach (UIElement child in _owner.State.Children)
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
        if (_owner.BlockChildInputWhileDragging && !element.IgnoresMouseInteraction)
        {
            element.IgnoresMouseInteraction = true;
            _panelInputIgnored = true;
        }
        _owner.OnDragStarted();
    }
    private void EndDrag(T element, bool snap)
    {
        if (snap)
        {
            SnapToEdge(element);
        }
        IsDragging = false;
        ReleaseDragClaims();
        _owner.OnDragEnded();
    }
    /// <summary>归还全局拖拽计数与子元素输入屏蔽。</summary>
    private void ReleaseDragClaims()
    {
        if (_sessionNotified)
        {
            _sessionNotified = false;
            DragUISession.NotifyDragEnded();
        }
        if (_panelInputIgnored && ResolveRootElement() is { } element)
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
        if (_owner.CloseOnEscape && escapeDown && !_escapeWasDown)
        {
            RequestClose();
        }
        _escapeWasDown = escapeDown;
    }
    /// <summary>松手时贴近屏幕边缘就贴边对齐。</summary>
    private void SnapToEdge(T element)
    {
        float snapDistance = _owner.SnapDistance;
        if (snapDistance <= 0f)
        {
            return;
        }
        float margin = _owner.EdgeMargin;
        CalculatedStyle dims = element.GetDimensions();
        float targetX = dims.X;
        float targetY = dims.Y;
        if (dims.X <= snapDistance)
        {
            targetX = margin;
        }
        else if (dims.X + dims.Width >= Main.screenWidth - snapDistance)
        {
            targetX = Main.screenWidth - dims.Width - margin;
        }
        if (dims.Y <= snapDistance)
        {
            targetY = margin;
        }
        else if (dims.Y + dims.Height >= Main.screenHeight - snapDistance)
        {
            targetY = Main.screenHeight - dims.Height - margin;
        }
        if (targetX.IsWithinTolerance(dims.X, 0.1f) && targetY.IsWithinTolerance(dims.Y, 0.1f))
        {
            return;
        }
        element.Left.Pixels += targetX - dims.X;
        element.Top.Pixels += targetY - dims.Y;
        element.Recalculate();
    }
    /// <summary>检查面板是否超出屏幕边缘，根据超出距离设置回弹初速度。</summary>
    private void CheckEdgeCollision(T element)
    {
        float margin = _owner.EdgeMargin;
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
        if (left < -margin)
        {
            initialVelocity.X = (Math.Abs(left) - margin) * _owner.SpringStrength;
            needRebound = true;
        }
        else if (right > screenW + margin)
        {
            initialVelocity.X = -(right - (screenW + margin)) * _owner.SpringStrength;
            needRebound = true;
        }
        if (top < -margin)
        {
            initialVelocity.Y = (Math.Abs(top) - margin) * _owner.SpringStrength;
            needRebound = true;
        }
        else if (bottom > screenH + margin)
        {
            initialVelocity.Y = -(bottom - (screenH + margin)) * _owner.SpringStrength;
            needRebound = true;
        }
        if (!needRebound)
        {
            return;
        }
        _isRebounding = true;
        _reboundVelocity = initialVelocity;
    }
    /// <summary>速度 Lerp 回弹：速度平滑衰减到 0，用速度更新位置。</summary>
    private void ApplyVelocityLerpRebound(T element)
    {
        _reboundVelocity = Vector2.Lerp(_reboundVelocity, Vector2.Zero, _owner.VelocityDecay);
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