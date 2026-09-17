global using Microsoft.Xna.Framework;
global using Microsoft.Xna.Framework.Graphics;
global using Microsoft.Xna.Framework.Input;
global using ReLogic.Content;
global using System;
global using System.Buffers;
global using System.Collections.Generic;
global using System.Text;
global using Terraria;
global using Terraria.Audio;
global using Terraria.DataStructures;
global using Terraria.GameContent;
global using Terraria.GameContent.UI.Elements;
global using Terraria.ID;
global using Terraria.ModLoader;
global using Terraria.ModLoader.IO;
global using Terraria.UI;

namespace EternalLib
{
    public class EternalLib : Mod
    {
        /// <summary>库的 API 版本，供依赖方进行特性探测。</summary>
        public const string ApiVersion = "0.23";
    }
}