namespace EternalLib
{
    /// <summary>
    /// 负责清理 EternalLib 持有的全部跨模组静态状态。
    /// <para>静态注册表不会随程序集卸载而自动失效，若不在卸载时清空，
    /// 模组重载后会残留旧实例（旧 <c>Texture2D</c>、旧物品类型号），造成显存泄漏与 <c>ObjectDisposedException</c>。</para>
    /// </summary>
    public sealed class EternalLibSystem : ModSystem
    {
        public override void Unload()
        {
            ColorGradient.ClearAll();
            DragUISession.Reset();
            FrameItem.ClearTextureCache();
        }
    }
}