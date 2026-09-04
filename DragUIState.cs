namespace EternalLib
{
    public abstract class DragUIState<T, TPanel> : UIState where T : DragUIState<T, TPanel> where TPanel : UIElement, new()
    {
        protected TPanel panel = new();
        protected virtual Keys Key => Keys.LeftShift;
        protected virtual float VelocityDecay => 0.16f;
        protected virtual float EdgeMargin => 10f;
        protected virtual float SpringStrength => 0.2f;
        public static bool IsDragging { get; private set; }
        private static bool IsKeyDown { get; set; }
        private float _dragOffsetX, _dragOffsetY;
        private bool _isRebounding;
        private Vector2 _reboundVelocity;
        private Vector2 _targetVelocity;
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (_isRebounding)
            {
                ApplyVelocityLerpRebound();
            }
            if (panel.IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.keyState.IsKeyDown(Key))
                {
                    if (!IsKeyDown)
                    {
                        _dragOffsetX = Main.MouseScreen.X - panel.GetDimensions().X;
                        _dragOffsetY = Main.MouseScreen.Y - panel.GetDimensions().Y;
                        _isRebounding = false;
                        _reboundVelocity = Vector2.Zero;
                        IsDragging = true;
                    }
                    IsKeyDown = true;
                }
            }
            if (Main.mouseLeft && IsKeyDown)
            {
                float newX = panel.Left.Pixels + Main.MouseScreen.X - panel.GetDimensions().X - _dragOffsetX;
                float newY = panel.Top.Pixels + Main.MouseScreen.Y - panel.GetDimensions().Y - _dragOffsetY;
                panel.Left.Set(newX, 0f);
                panel.Top.Set(newY, 0f);
                panel.Recalculate();
            }
            else
            {
                IsKeyDown = false;
                if (IsDragging)
                {
                    IsDragging = false;
                }
                if (!_isRebounding)
                {
                    CheckEdgeCollision();
                }
            }
        }
        /// <summary>
        /// 检查面板是否超出屏幕边缘，根据超出距离设置回弹初速度
        /// </summary>
        private void CheckEdgeCollision()
        {
            CalculatedStyle dims = panel.GetDimensions();
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
            if (left < -EdgeMargin)
            {
                float overflow = Math.Abs(left) - EdgeMargin;
                initialVelocity.X = overflow * SpringStrength;
                needRebound = true;
            }
            else if (right > screenW + EdgeMargin)
            {
                float overflow = right - (screenW + EdgeMargin);
                initialVelocity.X = -overflow * SpringStrength;
                needRebound = true;
            }
            if (top < -EdgeMargin)
            {
                float overflow = Math.Abs(top) - EdgeMargin;
                initialVelocity.Y = overflow * SpringStrength;
                needRebound = true;
            }
            else if (bottom > screenH + EdgeMargin)
            {
                float overflow = bottom - (screenH + EdgeMargin);
                initialVelocity.Y = -overflow * SpringStrength;
                needRebound = true;
            }
            if (!needRebound)
            {
                return;
            }
            _isRebounding = true;
            _reboundVelocity = initialVelocity;
            _targetVelocity = Vector2.Zero;
        }
        /// <summary>
        /// 速度 Lerp 回弹，速度平滑衰减到0，用速度更新位置
        /// </summary>
        private void ApplyVelocityLerpRebound()
        {
            _reboundVelocity = Vector2.Lerp(_reboundVelocity, _targetVelocity, VelocityDecay);
            panel.Left.Set(panel.Left.Pixels + _reboundVelocity.X, 0f);
            panel.Top.Set(panel.Top.Pixels + _reboundVelocity.Y, 0f);
            panel.Recalculate();
            if (_reboundVelocity.Length() >= 0.3f)
            {
                return;
            }
            _reboundVelocity = Vector2.Zero;
            _isRebounding = false;
        }
    }
}
