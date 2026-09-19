global using Microsoft.Xna.Framework;
global using Microsoft.Xna.Framework.Graphics;
global using Microsoft.Xna.Framework.Input;
global using ReLogic.Content;
global using System;
global using System.Buffers;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Linq.Expressions;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Text;
global using Terraria;
global using Terraria.Audio;
global using Terraria.DataStructures;
global using Terraria.GameContent;
global using Terraria.GameContent.UI.Elements;
global using Terraria.ID;
global using Terraria.ModLoader;
global using Terraria.ModLoader.IO;
global using Terraria.ObjectData;
global using Terraria.UI;
global using static EternalLib.EternalLog;

namespace EternalLib
{
    public class EternalLib : Mod
    {
        /// <summary>库的 API 版本，供依赖方进行特性探测。</summary>
        public const string ApiVersion = "0.33";
        /// <summary>
        /// 库内消息入口。依赖方不需要知道库的包格式，只需要实现自己的 <c>HandlePacket</c>。
        /// </summary>
        public override void HandlePacket(BinaryReader reader, int whoAmI) => EternalNet.Handle(reader, whoAmI);
        public override void Load()
        {
            //先解绑再绑定，保证热重载时不会重复订阅；注册表也在这里还原成默认值
            Unload();
            On_WorldGen.SpawnThingsFromPot += SpawnThingsFromPotHook;
        }
        public override void Unload()
        {
            On_WorldGen.SpawnThingsFromPot -= SpawnThingsFromPotHook;
            BreakHelper.ResetRegistries();
        }
        /// <summary>
        /// 这类方块奖励的唯一生成点：原版在 <c>WorldGen.CheckPot</c> 里现算奖励时调它，完全不经过
        /// <c>KillTile</c>，所以只在 KillTile 上挂钩接不住（支撑方块被拆后的延迟级联就是走这里）。
        /// <para>没有收集窗口、且确定属于我们的范围挖掘上下文时（见
        /// <see cref="BreakHelper.TryGetAoeMiningContext"/>），临时开窗口接住奖励：
        /// 单人 / 服务端交给责任人那件工具的收纳策略，多人客户端只吞掉（掉落以服务端为权威）。</para>
        /// </summary>
        private void SpawnThingsFromPotHook(On_WorldGen.orig_SpawnThingsFromPot orig, int i, int j, int x2, int y2, int style)
        {
            if (BreakHelper.ActiveDropSink is not null
                || !BreakHelper.TryGetAoeMiningContext(i, j, out Player? player, out IAoeMiningTool? tool))
            {
                orig(i, j, x2, y2, style);
                return;
            }
            List<Item> captured = [];
            List<Item>? previousSink = BreakHelper.BeginExternalCapture(captured);
            try
            {
                orig(i, j, x2, y2, style);
            }
            finally
            {
                BreakHelper.EndExternalCapture(previousSink);
            }
            BreakHelper.DeliverExternalDrops(captured, i, j, player, tool);
        }
    }
}