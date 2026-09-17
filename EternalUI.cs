namespace EternalLib
{
    /// <summary>
    /// UI 元素工厂，收敛各模组里重复的面板 / 标题 / 关闭按钮样板代码。
    /// </summary>
    public static class EternalUI
    {
        /// <summary>默认面板背景色。</summary>
        public static readonly Color DefaultPanelColor = new Color(63, 82, 151) * 0.8f;
        /// <summary>
        /// 创建一个带内边距与背景色的主面板。
        /// </summary>
        public static UIPanel CreatePanel(Vector2 size, Color? background = null, float padding = 5f)
        {
            UIPanel panel = new()
            {
                BackgroundColor = background ?? DefaultPanelColor
            };
            panel.SetPadding(padding);
            panel.Width.Set(size.X, 0f);
            panel.Height.Set(size.Y, 0f);
            return panel;
        }
        /// <summary>
        /// 创建面板标题文本（默认放在面板上方居中位置）。
        /// </summary>
        public static UIText CreateTitle(string text, float top = -30f, float scale = 1f, bool large = false)
        {
            UIText title = new(text, scale, large)
            {
                HAlign = 0.5f
            };
            title.Top.Set(top, 0f);
            return title;
        }
        /// <summary>
        /// 创建右上角关闭按钮：点击时播放原版关闭音效并执行 <paramref name="onClose"/>。
        /// </summary>
        public static UITextPanel<string> CreateCloseButton(string text, Action onClose, float width = 100f, float height = 40f)
        {
            UITextPanel<string> button = new(text)
            {
                HAlign = 0.99f,
                VAlign = 0.01f
            };
            button.Width.Set(width, 0f);
            button.Height.Set(height, 0f);
            button.OnLeftClick += (_, _) =>
            {
                SoundEngine.PlaySound(SoundID.MenuClose);
                onClose?.Invoke();
            };
            return button;
        }
        /// <summary>
        /// 创建固定尺寸的滚动条（不含挂载）。
        /// </summary>
        public static UIScrollbar CreateScrollbar(float height, float viewSize, float maxViewSize, float hAlign = 0.61f)
        {
            UIScrollbar scrollbar = new()
            {
                HAlign = hAlign,
                VAlign = 0.5f
            };
            scrollbar.Height.Set(height, 0f);
            scrollbar.SetView(viewSize, maxViewSize);
            return scrollbar;
        }
        /// <summary>
        /// 创建一个已挂好滚动条的 <see cref="UIList"/>（原版列表自带滚轮支持）。
        /// </summary>
        public static UIList CreateList(Vector2 size, float hAlign, out UIScrollbar scrollbar,
            float viewSize = 66f, float maxViewSize = 333f)
        {
            UIList list = new()
            {
                HAlign = hAlign,
                VAlign = 0.5f
            };
            list.Width.Set(size.X, 0f);
            list.Height.Set(size.Y, 0f);
            scrollbar = CreateScrollbar(size.Y, viewSize, maxViewSize, hAlign + 0.09f);
            list.SetScrollbar(scrollbar);
            return list;
        }
        /// <summary>鼠标当前是否被 UI 占用（该帧不要再处理物品使用等输入）。</summary>
        public static bool IsMouseOverUI => Main.LocalPlayer is { mouseInterface: true };
    }
}
