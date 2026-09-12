namespace EternalLib
{
    public abstract class DragUIState<T> : UIState where T : UIElement
    {
        public bool Visible { get; set; }
        protected T? Element { get; set; }
        protected virtual Keys Key => Keys.LeftShift;
        protected virtual float VelocityDecay => 0.16f;
        protected virtual float EdgeMargin => 10f;
        protected virtual float SpringStrength => 0.2f;
        public bool IsDragging { get; private set; }
        private bool IsKeyDown { get; set; }
        private float _dragOffsetX, _dragOffsetY;
        private bool _isRebounding;
        private Vector2 _reboundVelocity;
        private Vector2 _targetVelocity;
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Element is null)
            {
                return;
            }
            if (_isRebounding)
            {
                ApplyVelocityLerpRebound();
            }
            if (Element.IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.keyState.IsKeyDown(Key))
                {
                    if (!IsKeyDown)
                    {
                        _dragOffsetX = Main.MouseScreen.X - Element.GetDimensions().X;
                        _dragOffsetY = Main.MouseScreen.Y - Element.GetDimensions().Y;
                        _isRebounding = false;
                        _reboundVelocity = Vector2.Zero;
                        IsDragging = true;
                    }
                    IsKeyDown = true;
                }
            }
            if (Main.mouseLeft && IsKeyDown)
            {
                float newX = Element.Left.Pixels + Main.MouseScreen.X - Element.GetDimensions().X - _dragOffsetX;
                float newY = Element.Top.Pixels + Main.MouseScreen.Y - Element.GetDimensions().Y - _dragOffsetY;
                Element.Left.Set(newX, 0f);
                Element.Top.Set(newY, 0f);
                Element.Recalculate();
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
            if (Element is null)
            {
                return;
            }
            CalculatedStyle dims = Element.GetDimensions();
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
            if (Element is null)
            {
                return;
            }
            _reboundVelocity = Vector2.Lerp(_reboundVelocity, _targetVelocity, VelocityDecay);
            Element.Left.Set(Element.Left.Pixels + _reboundVelocity.X, 0f);
            Element.Top.Set(Element.Top.Pixels + _reboundVelocity.Y, 0f);
            Element.Recalculate();
            if (_reboundVelocity.Length() >= 0.3f)
            {
                return;
            }
            _reboundVelocity = Vector2.Zero;
            _isRebounding = false;
        }
    }
}
