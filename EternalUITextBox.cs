namespace EternalLib
{
    /// <summary>
    /// 带占位符与文本变化事件的输入框。
    /// <para>原版 <see cref="UITextBox"/> 在 1.4.4.9 中没有暴露任何文本变化事件
    /// （<c>Text</c> 只是继承自 <c>UITextPanel&lt;string&gt;</c> 的属性），
    /// 这里用逐帧比对的方式提供 <see cref="TextChanged"/>，用于列表过滤等场景。</para>
    /// </summary>
    public class EternalUITextBox : UITextBox
    {
        /// <summary>文本发生变化时触发（参数为最新文本）。</summary>
        public event Action<string>? TextChanged;
        /// <summary>文本为空且未悬停时显示的占位提示。</summary>
        public string PlaceholderText { get; set; } = string.Empty;
        /// <summary>占位提示颜色。</summary>
        public Color PlaceholderColor { get; set; } = Color.Gray * 0.8f;
        /// <summary>占位提示左右的额外内缩（像素）。</summary>
        public Vector2 PlaceholderOffset { get; set; } = new(8f, 2f);
        private string _lastText;
        public EternalUITextBox(string text = "", float textScale = 1f, bool large = false, int maxLength = 64)
            : base(text, textScale, large)
        {
            _lastText = text;
            SetTextMaxLength(maxLength);
        }
        /// <summary>当前文本。</summary>
        public string CurrentText => Text ?? string.Empty;
        /// <summary>清空文本并立即触发一次变化事件。</summary>
        public void ClearText() => SetText(string.Empty, TextScale, IsLarge);
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            string current = CurrentText;
            if (current == _lastText)
            {
                return;
            }
            _lastText = current;
            TextChanged?.Invoke(current);
        }
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            if (!string.IsNullOrEmpty(CurrentText) || string.IsNullOrEmpty(PlaceholderText))
            {
                return;
            }
            CalculatedStyle inner = GetInnerDimensions();
            Utils.DrawBorderString(spriteBatch, PlaceholderText,
                inner.Position() + PlaceholderOffset, PlaceholderColor, TextScale);
        }
    }
}