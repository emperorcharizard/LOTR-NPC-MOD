using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>Finding somewhere to stand: for teleporting followers, placing formations, guard posts and patrols.</summary>
    public static class Ground
    {
        /// <summary>The height to stand at near (x, y, z): a free block on solid, harmless ground, searched up and down
        /// from y (tries: how many heights). Null if there is none.</summary>
        public static double? StandHeight(IBlockAccessor ba, double x, double y0, double z, int dimension, int tries)
        {
            BlockPos bpos = new BlockPos(dimension);
            int bx = (int)Math.Floor(x), bz = (int)Math.Floor(z);
            for (int j = 0; j < tries; j++)
            {
                int dy = (1 - (j % 2) * 2) * (int)Math.Ceiling(j / 2f);
                int y = (int)Math.Floor(y0) + dy;
                bpos.Set(bx, y, bz);
                if (!Free(ba, bpos)) continue;
                bpos.Up();
                if (!Free(ba, bpos)) continue;
                bpos.Set(bx, y - 1, bz);
                Block below = ba.GetBlock(bpos);
                Cuboidf[] boxes = below.GetCollisionBoxes(ba, bpos);
                if (boxes == null || boxes.Length == 0 || below.Attributes?["insideDamage"].AsInt(0) > 0) continue;
                float top = 0;
                foreach (Cuboidf box in boxes) top = Math.Max(top, box.Y2);
                return y - 1 + top;
            }
            return null;
        }

        static bool Free(IBlockAccessor ba, BlockPos pos)
        {
            Block block = ba.GetBlock(pos);
            Cuboidf[] boxes = block.GetCollisionBoxes(ba, pos);
            return (boxes == null || boxes.Length == 0) && !(block.Attributes?["insideDamage"].AsInt(0) > 0) && !block.IsLiquid();
        }
    }
}
