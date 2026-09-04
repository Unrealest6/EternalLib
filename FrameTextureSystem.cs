namespace EternalLib
{
    public sealed class FrameTextureSystem : ModSystem
    {
        internal static Dictionary<string, FrameTexture?> Animations { get; } = [];
        public static FrameTexture? Register(string name, string texturePath)
        {
            if (Animations.TryGetValue(name, out FrameTexture? existing))
            {
                return existing;
            }
            FrameTexture anim = new(name, texturePath);
            Animations[name] = anim;
            return anim;
        }
        public static FrameTexture? Register(string name, string texturePath, int totalFrames,
            FrameDef[]? timeline, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
        {
            if (Animations.TryGetValue(name, out FrameTexture? existing))
            {
                return existing;
            }
            FrameTexture anim = new(name, texturePath, totalFrames, timeline, frameDuration, mode);
            Animations[name] = anim;
            return anim;
        }
        public static FrameTexture? Register(string name, string texturePath, int totalFrames,
            int frameDuration = 1, FrameMode mode = FrameMode.Loop)
        {
            FrameDef[] timeline = new FrameDef[Math.Max(0, totalFrames)];
            for (int i = 0; i < timeline.Length; i++)
            {
                timeline[i] = new FrameDef(i, frameDuration);
            }
            return Register(name, texturePath, totalFrames, timeline, frameDuration, mode);
        }
        public static FrameTexture? RegisterGrid(string name, string texturePath, int rows, int columns,
            FrameDef[] timeline, int frameDuration = 1, FrameMode mode = FrameMode.Loop)
        {
            if (Animations.TryGetValue(name, out FrameTexture? existing))
            {
                return existing;
            }
            FrameTexture anim = new(name, texturePath, rows, columns, timeline, frameDuration, mode);
            Animations[name] = anim;
            return anim;
        }
        public static FrameTexture? RegisterGrid(string name, string texturePath, int rows, int columns,
            int frameDuration = 1, FrameMode mode = FrameMode.Loop)
        {
            int totalFrames = rows * columns;
            FrameDef[] timeline = new FrameDef[Math.Max(0, totalFrames)];
            for (int i = 0; i < timeline.Length; i++)
            {
                timeline[i] = new FrameDef(i, frameDuration);
            }
            return RegisterGrid(name, texturePath, rows, columns, timeline, frameDuration, mode);
        }
        private static void LoadAll()
        {
            foreach (FrameTexture? anim in Animations.Values)
            {
                try { anim?.Load(); }
                catch (Exception ex)
                {
                    ModContent.GetInstance<EternalLib>().Logger.Error(
                        $"Failed to load animation '{anim?.Name}': {ex.Message}");
                }
            }
        }
        public override void PostSetupContent()
        {
            if (Main.dedServ)
            {
                return;
            }
            Main.QueueMainThreadAction(() =>
            {
                LoadAll();
                ModContent.GetInstance<EternalLib>().Logger.Info($"{Name}:Loaded {Animations.Count} animations.");
            });
        }
        public override void PostUpdateEverything()
        {
            foreach (FrameTexture? anim in Animations.Values)
            {
                anim?.Update();
            }
        }
        public override void Unload()
        {
            foreach (FrameTexture? anim in Animations.Values)
            {
                anim?.Unload();
            }
            Animations.Clear();
        }
    }
}