namespace EternalLib
{
    public abstract class DragUISystem<T, TState> : ModSystem where T : DragUISystem<T, TState> where TState : DragUIState<UIPanel>
    {
        public static TState? CurrentUI { get; protected set; }
        protected static UserInterface? UserInterface { get; private set; }
        public virtual void ShowUI()
        {
            CurrentUI?.Activate();
            UserInterface?.SetState(CurrentUI);
        }
        public virtual void HideUI()
        {
            CurrentUI?.Visible = false;
            UserInterface?.SetState(null);
            CurrentUI = null;
            SoundEngine.PlaySound(SoundID.MenuClose);
        }
        protected virtual string Vanilla => "Mouse Text";
        public override void Load() => UserInterface = new UserInterface();
        public override void Unload()
        {
            UserInterface = null;
            CurrentUI = null;
        }
        public override void UpdateUI(GameTime gameTime) => UserInterface?.Update(gameTime);
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(l => l.Name.Equals("Vanilla: " + Vanilla));
            if (index != -1)
            {
                layers.Insert(index, new LegacyGameInterfaceLayer(Mod.Name + ": " + FullName,
                    delegate { UserInterface?.Draw(Main.spriteBatch, new GameTime()); return true; },
                    InterfaceScaleType.UI));
            }
        }
    }
}