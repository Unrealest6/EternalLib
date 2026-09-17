namespace EternalLib
{
    public sealed class EternalPlayer : ModPlayer
    {
        /// <summary>双击跳跃键的判定窗口（tick）。</summary>
        public const int DoubleTapWindow = 20;
        internal bool CanFly { get; set; }
        internal float FlySpeedX => Player.moveSpeed * 5f;
        internal float FlySpeedY => Player.jumpSpeedBoost + Player.jumpSpeed;
        /// <summary>是否正处于飞行状态。</summary>
        public bool IsFlying { get; private set; }
        private bool _jumpWasDown;
        private int _doubleTapTimer;
        public override void Initialize() => ResetFlyState();
        public override void OnRespawn() => ResetFlyState();
        public override void ResetEffects() => CanFly = false;
        public override void PreUpdateMovement()
        {
            if (!CanFly)
            {
                // 失去飞行能力（卸下装备/死亡）时不要保留旧的飞行状态，
                // 否则重新装备后无需双击就会立刻起飞。
                ResetFlyState();
                return;
            }
            UpdateFlySwitch();
            if (IsFlying)
            {
                ApplyFlyMovement();
            }
        }
        private void ResetFlyState()
        {
            IsFlying = false;
            _jumpWasDown = false;
            _doubleTapTimer = 0;
        }
        /// <summary>检测“按下”边沿并在窗口期内双击时切换飞行。</summary>
        private void UpdateFlySwitch()
        {
            bool jumpDown = Player.controlJump;
            if (jumpDown && !_jumpWasDown)
            {
                if (_doubleTapTimer > 0)
                {
                    IsFlying = !IsFlying;
                    _doubleTapTimer = 0;
                }
                else
                {
                    _doubleTapTimer = DoubleTapWindow;
                }
            }
            _jumpWasDown = jumpDown;
            if (_doubleTapTimer > 0)
            {
                _doubleTapTimer--;
            }
        }
        private void ApplyFlyMovement()
        {
            Player.gravity = 0f;
            Player.noFallDmg = true;
            if (Player.controlLeft)
            {
                Player.velocity.X--;
            }
            if (Player.controlRight)
            {
                Player.velocity.X++;
            }
            if (Player.controlUp || Player.controlJump)
            {
                Player.velocity.Y--;
            }
            else if (Player.controlDown)
            {
                Player.velocity.Y++;
            }
            else
            {
                Player.velocity.Y = 0f;
            }
            Player.ClampVelocity(new Vector2(FlySpeedX, FlySpeedY));
        }
    }
}