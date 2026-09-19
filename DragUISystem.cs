namespace EternalLib
{
    /// <summary>
    /// 拖拽 UI 的宿主系统：负责界面层级、状态切换与每帧更新。
    /// <para>泛型参数 <typeparamref name="T"/> 为自身类型（CRTP），使每个 UI 拥有独立的静态状态。</para>
    /// </summary>
    public abstract class DragUISystem<T, TState> : ModSystem
        where T : DragUISystem<T, TState> where TState : DragUIState<UIPanel>
    {
        /// <summary>绘制阶段复用的 <see cref="GameTime"/>，避免每帧分配。</summary>
        private static readonly GameTime UIGameTime = new();
        /// <summary>当前打开的 UI 状态。</summary>
        public static TState? CurrentUI { get; protected set; }
        /// <summary>当前 UI 是否处于打开状态。</summary>
        public static bool IsUIOpen => CurrentUI is not null;
        protected static UserInterface? UserInterface { get; private set; }
        /// <summary>打开指定界面（会替换当前已打开的界面）。</summary>
        public virtual void ShowUI(TState state)
        {
            // 走公开的 HideUI()，保证派生类重写的清理逻辑（例如解绑物块实体）一定被执行。
            HideUI();
            CurrentUI = state;
            ShowUI();
        }
        /// <summary>显示 <see cref="CurrentUI"/>（需先由派生类创建实例）。</summary>
        public virtual void ShowUI()
        {
            if (CurrentUI is null)
            {
                return;
            }
            CurrentUI.Visible = true;
            // 让 Esc / 关闭按钮统一回到本系统，派生类不必再各自实现。
            CurrentUI.CloseRequested -= HideUI;
            CurrentUI.CloseRequested += HideUI;
            CurrentUI.Activate();
            UserInterface?.SetState(CurrentUI);
        }
        /// <summary>关闭当前 UI。派生类可重写以保存面板位置等状态。</summary>
        public virtual void HideUI() => HideUI(playSound: true);
        private void HideUI(bool playSound)
        {
            if (CurrentUI is not null)
            {
                CurrentUI.Visible = false;
                CurrentUI.CloseRequested -= HideUI;
                CurrentUI.CancelDrag();
            }
            UserInterface?.SetState(null);
            CurrentUI = null;
            if (playSound)
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
            }
        }
        /// <summary>插入界面的目标原版层级名。</summary>
        protected virtual string Vanilla => "Mouse Text";
        public override void Load()
        {
            if (Main.dedServ)
            {
                return;
            }
            UserInterface = new UserInterface();
            // 拖拽开始时清掉本界面的按下缓存：原版点击在松手时按“按下那一刻记录的目标”派发，不清掉就会误触发拖拽前按下的按钮。
            DragUISession.DragStarted += ClearPendingClicks;
        }
        public override void Unload()
        {
            DragUISession.DragStarted -= ClearPendingClicks;
            UserInterface = null;
            CurrentUI = null;
        }
        /// <summary>清掉本界面的鼠标按下缓存（详见 <see cref="DragUISession.DiscardPendingClicks"/>）。</summary>
        private static void ClearPendingClicks() => DragUISession.DiscardPendingClicks(UserInterface);
        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.dedServ)
            {
                return;
            }
            UserInterface?.Update(gameTime);
        }
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(l => l.Name.Equals("Vanilla: " + Vanilla));
            if (index == -1)
            {
                return;
            }
            layers.Insert(index, new LegacyGameInterfaceLayer(Mod.Name + ": " + FullName,
                delegate { UserInterface?.Draw(Main.spriteBatch, UIGameTime); return true; },
                InterfaceScaleType.UI));
        }
    }
}
