global using Microsoft.Xna.Framework;
global using Microsoft.Xna.Framework.Graphics;
global using Microsoft.Xna.Framework.Input;
global using Mono.Cecil.Cil;
global using MonoMod.Cil;
global using ReLogic.Content;
global using System;
global using System.Buffers;
global using System.Collections.Generic;
global using System.Reflection;
global using System.Text;
global using Terraria;
global using Terraria.Audio;
global using Terraria.DataStructures;
global using Terraria.GameContent;
global using Terraria.GameContent.UI.Elements;
global using Terraria.ID;
global using Terraria.ModLoader;
global using Terraria.UI;

namespace EternalLib
{
    public class EternalLib : Mod
    {
        public override void Load()
        {
            On_Player.GetItemDrawFrame += FrameItem.PlayerGetItemDrawFrameHook;
            IL_PlayerDrawLayers.DrawPlayer_27_HeldItem += FrameItem.DrawPlayerHeldItemHook;
        }
        public override void Unload()
        {
            On_Player.GetItemDrawFrame -= FrameItem.PlayerGetItemDrawFrameHook;
            IL_PlayerDrawLayers.DrawPlayer_27_HeldItem -= FrameItem.DrawPlayerHeldItemHook;
        }
    }
}