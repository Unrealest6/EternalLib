namespace EternalLib
{
    /// <summary>
    /// 拖拽面板的统一驱动系统：更新、绘制、拖拽开始时清掉各宿主的点击缓存。
    /// <para>所有模组只需要这一个 <see cref="ModSystem"/>；要开一个新的拖拽面板，只要在自己的系统里
    /// 持有一个 <see cref="DragPanelHost"/> 并实现 <see cref="IDragUIPanelHost"/> 即可，不需要再写泛型基类。</para>
    /// </summary>
    public sealed class DragPanelSystem : ModSystem
    {
        public override void Load()
        {
            if (Main.dedServ)
            {
                return;
            }
            DragUISession.DragStarted += ClearPendingClicks;
        }
        public override void Unload()
        {
            DragUISession.DragStarted -= ClearPendingClicks;
            foreach (DragPanelHost host in DragPanelHost.Instances)
            {
                host.Reset();
            }
            DragPanelHost.ClearAll();
        }
        /// <summary>拖拽开始时清掉所有宿主的按下缓存：原版点击在松手时按“按下那一刻记录的目标”派发，不清掉会误触发拖拽前按下的按钮。</summary>
        private static void ClearPendingClicks()
        {
            foreach (DragPanelHost host in DragPanelHost.Instances)
            {
                host.DiscardPendingClicks();
            }
        }
        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.dedServ)
            {
                return;
            }
            foreach (DragPanelHost host in DragPanelHost.Instances)
            {
                host.Update(gameTime);
            }
        }
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            foreach (DragPanelHost host in DragPanelHost.Instances)
            {
                InsertLayer(layers, host);
            }
        }
        private static void InsertLayer(List<GameInterfaceLayer> layers, DragPanelHost host)
        {
            //锚点必须是真实存在的原版层名（host.Anchor）；写成 Key 会找不到插入位置，界面永远不会被绘制
            int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: " + host.Anchor));
            if (index == -1)
            {
                return;
            }
            layers.Insert(index, new LegacyGameInterfaceLayer(ModContent.GetInstance<DragPanelSystem>().Mod.Name + ": " + host.Key,
                delegate { host.Draw(Main.spriteBatch); return true; },
                InterfaceScaleType.UI));
        }
    }
}
