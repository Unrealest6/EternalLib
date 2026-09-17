namespace EternalLib
{
    /// <summary>
    /// 为 <see cref="FrameItem"/> 的背包绘制提供“当前正在绘制的物品实例”。
    /// <para><c>ModItem.PreDrawInInventory</c> 的签名里没有 Item 参数，无法知道正在画的是哪一个实例；
    /// 而 <see cref="GlobalItem"/> 的同名钩子会先收到 Item（tModLoader 的调用顺序是
    /// 先跑 GlobalItem 钩子，再跑 ModItem 钩子）。这里在 Global 侧压栈、PostDraw 侧弹栈，
    /// 于是“形态按物品实例绑定”的物品在背包里也能画出各自正确的贴图。</para>
    /// </summary>
    public sealed class FrameItemDrawContext : GlobalItem
    {
        private static readonly Stack<Item> DrawStack = new();
        /// <summary>当前正在绘制背包图标的物品实例；不在绘制中时为 null。</summary>
        public static Item? CurrentInventoryItem => DrawStack.Count > 0 ? DrawStack.Peek() : null;
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.ModItem is FrameItem;
        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            DrawStack.Push(item);
            return true;
        }
        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            if (DrawStack.Count > 0 && ReferenceEquals(DrawStack.Peek(), item))
            {
                DrawStack.Pop();
            }
        }
    }
}