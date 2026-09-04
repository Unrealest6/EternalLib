namespace EternalLib
{
    public sealed class EternalPlayer : ModPlayer
    {
        internal bool CanFly { get; set; }
        internal float FlySpeedX => Player.moveSpeed * 5f;
        internal float FlySpeedY => Player.jumpSpeedBoost + Player.jumpSpeed;
        private bool IsFlyKeyDown { get; set; } = true;
        private bool Fly { get; set; }
        private int FlyTimer { get; set; }
        private int KeyDownTimer { get; set; }
        private void PreFly()
        {
            if (FlyTimer <= 0)
            {
                KeyDownTimer = 0;
            }
            if (Player.controlJump)
            {
                if (IsFlyKeyDown)
                {
                    KeyDownTimer++;
                    if (KeyDownTimer == 2)
                    {
                        Fly = !Fly;
                        KeyDownTimer = 0;
                    }
                    else
                    {
                        FlyTimer = 20;
                    }
                    IsFlyKeyDown = false;
                }
            }
            if (!Player.controlJump)
            {
                IsFlyKeyDown = true;
            }
            FlyTimer--;
        }
        private void Flying(Vector2 maxVelocity)
        {
            Player.gravity = 0;
            Player.noFallDmg = true;
            Player.ResetVelocity(maxVelocity);
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
            if (Player.controlDown)
            {
                Player.velocity.Y++;
            }
            if (!(Player.controlDown || Player.controlUp || Player.controlJump))
            {
                Player.velocity.Y = 0;
            }
        }
        public override void ResetEffects()
        {
            CanFly = false;
        }
        public override void PreUpdateMovement()
        {
            PreFly();
            if (CanFly && Fly)
            {
                Flying(new Vector2(FlySpeedX, FlySpeedY));
            }
        }
    }
}