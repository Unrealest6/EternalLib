namespace EternalLib
{
    public abstract class FrameItem : ModItem
    {
        public FrameTexture? FrameTexture => ModContent.GetFrameTexture(FullName);
        public byte Mode { get; protected set; }
        protected abstract int FrameCount { get; }
        protected abstract FrameDef[] FrameTimeline { get; }
        public virtual Dictionary<byte, FrameTexture?> FrameTextures => [];
        protected virtual int FrameDuration => 1;
        protected virtual Rectangle PlayerGetItemDrawFrame(On_Player.orig_GetItemDrawFrame orig, Player self, int type)
            => Mode != 0 && FrameTextures.TryGetValue(Mode, out FrameTexture? texture) && texture != null ? texture.GetFrameRect()
            : FrameTexture?.GetFrameRect() ?? orig.Invoke(self, type);
        protected virtual Texture2D? GetPlayerHeldTexture()
            => Mode != 0 && FrameTextures.TryGetValue(Mode, out FrameTexture? texture) && texture != null ? texture.GetCurrentFrame()
            : FrameTexture?.GetCurrentFrame();
        public override void SetStaticDefaults()
        {
            FrameTextureSystem.Register(FullName, Texture, FrameCount, FrameTimeline, FrameDuration);
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(FrameDuration, FrameCount));
        }
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            if (FrameTexture == null)
            {
                return false;
            }
            Texture2D currentFrame = FrameTexture.GetCurrentFrame();
            if (Mode != 0 && FrameTextures.TryGetValue(Mode, out FrameTexture? texture) && texture != null)
            {
                currentFrame = texture.GetCurrentFrame();
            }
            spriteBatch.Draw(currentFrame, position, null, drawColor, 0f, currentFrame.Size() / 2f, scale, SpriteEffects.None, 0f);
            return false;
        }
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            if (FrameTexture == null)
            {
                return false;
            }
            Texture2D currentFrame = FrameTexture.GetCurrentFrame();
            if (Mode != 0 && FrameTextures.TryGetValue(Mode, out FrameTexture? texture) && texture != null)
            {
                currentFrame = texture.GetCurrentFrame();
            }
            Vector2 vector = new(currentFrame.Width / 2f, currentFrame.Height / 2f);
            Vector2 vector3 = Item.position - Main.screenPosition + vector;
            spriteBatch.Draw(currentFrame, vector3, null, lightColor, rotation, vector, scale, SpriteEffects.None, 0f);
            return false;
        }
        internal static Rectangle PlayerGetItemDrawFrameHook(On_Player.orig_GetItemDrawFrame orig, Player self, int type)
            => ItemLoader.GetItem(type) is FrameItem item ? item.PlayerGetItemDrawFrame(orig, self, type) : orig.Invoke(self, type);
        internal static void DrawPlayerHeldItemHook(ILContext il)
        {
            ILCursor c = new(il);
            if (!c.TryGotoNext(i => i.MatchStloc(3)))
            {
                return;
            }
            c.Index++;
            ILLabel skipLabel = c.DefineLabel();
            ILLabel endLabel = c.DefineLabel();
            c.Emit(OpCodes.Ldloc, 0);
            c.Emit(OpCodes.Call, typeof(FrameItem).GetMethod(nameof(GetHeldTexture), BindingFlags.Static | BindingFlags.NonPublic)!);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Brfalse_S, skipLabel);
            c.Emit(OpCodes.Stloc, 3);
            c.Emit(OpCodes.Br_S, endLabel);
            c.MarkLabel(skipLabel);
            c.Emit(OpCodes.Pop);
            c.MarkLabel(endLabel);
        }
        private static Texture2D? GetHeldTexture(Item heldItem) => heldItem.ModItem is FrameItem item ? item.GetPlayerHeldTexture() : null;
    }
}