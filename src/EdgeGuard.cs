using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// Keeps NPCs from walking off drops deeper than MaxSafeDrop (0.0.88, owner, 2026-11-04: "when navigating paths over
    /// deep pits, the npcs have no regard for safety and will fling themselves off the edge").
    ///
    /// The game's pathfinder plans no fall deeper than about 4 blocks, but several moves skip the plan: when no path is
    /// found, the follow task and vanilla seek's repath walk straight at their goal; vanilla seek (and our battle places)
    /// point the last stretch of a path straight at a moving target every tick; archers step back in a straight line.
    /// So instead of fixing each, this checks every step: on the server, right after the AI has set where the NPC walks
    /// this tick (the behavior is listed after "taskai"), it looks EdgeLookAhead blocks ahead along that walk; if the
    /// ground there is more than MaxSafeDrop blocks down (or lava), the walk stops: the path is dropped and the walk and
    /// outward motion are zeroed. The task then ends and picks again; standing at the edge is the safe outcome. Water
    /// below counts as a safe landing; a wall or step ahead is not a drop. Not while airborne, swimming or mounted.
    /// </summary>
    public class EntityBehaviorEdgeGuard : EntityBehavior
    {
        /// <summary>The deepest drop an NPC walks off: the pathfinder's own limit for walking NPCs.</summary>
        public const int MaxSafeDrop = 4;
        /// <summary>How far ahead of its middle the ground is checked: half the NPC's width and a step.</summary>
        public const double EdgeLookAhead = 0.6;
        /// <summary>0.0.105 (owner, 2026-11-09: archers "end up jumping off walls in order to enter melee combat"): archers
        /// step down at most this far while fighting, so they stay up on walls and hills (out of a fight they follow
        /// their leader down like anyone).</summary>
        public const int ArcherMaxDrop = 1;
        int archer = -1;
        EntityBehaviorFactionMember member;

        public EntityBehaviorEdgeGuard(Entity entity) : base(entity) { }

        public override string PropertyName() => "lotrnpcs:edgeguard";

        /// <summary>What a block cell holds, for the drop check.</summary>
        public enum Cell { Air, Solid, Water, Lava }

        /// <summary>
        /// Is stepping into column (x, z) at feet level y unsafe? A solid block at feet level is a wall or a step (not a
        /// drop); otherwise the first solid block or water below must be at most maxDrop blocks down; lava, or nothing
        /// within reach, is unsafe.
        /// </summary>
        public static bool Unsafe(System.Func<int, int, int, Cell> cell, int x, int y, int z, int maxDrop)
        {
            if (cell(x, y, z) == Cell.Solid) return false;
            for (int fall = 0; fall <= maxDrop; fall++)
            {
                switch (cell(x, y - 1 - fall, z))
                {
                    case Cell.Solid: case Cell.Water: return false;
                    case Cell.Lava: return true;
                }
            }
            return true;
        }

        public static Cell CellAt(IBlockAccessor blocks, BlockPos pos)
        {
            Block fluid = blocks.GetBlock(pos, BlockLayersAccess.Fluid);
            if (fluid != null && fluid.IsLiquid()) return fluid.LiquidCode == "lava" || fluid.Code?.Path.Contains("lava") == true ? Cell.Lava : Cell.Water;
            Cuboidf[] boxes = blocks.GetBlock(pos, BlockLayersAccess.Solid).GetCollisionBoxes(blocks, pos);
            return boxes != null && boxes.Length > 0 ? Cell.Solid : Cell.Air;
        }

        readonly BlockPos tmp = new BlockPos(0);
        System.Func<int, int, int, Cell> cellFn;

        public override void OnGameTick(float deltaTime)
        {
            if (entity.Api.Side != EnumAppSide.Server || !entity.Alive || !(entity is EntityAgent agent)) return;
            if (!entity.OnGround || entity.Swimming || entity.FeetInLiquid || agent.MountedOn != null) return;
            Vec3d walk = agent.Controls.WalkVector;
            double wx = walk.X, wz = walk.Z;
            double len = Math.Sqrt(wx * wx + wz * wz);
            if (len < 1e-5) return;
            double ax = entity.Pos.X + wx / len * EdgeLookAhead, az = entity.Pos.Z + wz / len * EdgeLookAhead;
            int bx = (int)Math.Floor(ax), bz = (int)Math.Floor(az), by = (int)Math.Floor(entity.Pos.InternalY + 0.01);
            // Still on its own block: nothing new underfoot.
            if (bx == (int)Math.Floor(entity.Pos.X) && bz == (int)Math.Floor(entity.Pos.Z)) return;
            IBlockAccessor blocks = entity.World.BlockAccessor;
            tmp.dimension = entity.Pos.Dimension;
            cellFn ??= (x, y, z) => { tmp.Set(x, y, z); return CellAt(entity.World.BlockAccessor, tmp); };
            if (archer < 0) { archer = Recruit.IsArcher(entity) ? 1 : 0; member = entity.GetBehavior<EntityBehaviorFactionMember>(); }
            int maxDrop = archer == 1 && member != null && member.Fighting(entity.World.ElapsedMilliseconds) ? ArcherMaxDrop : MaxSafeDrop;
            if (!Unsafe(cellFn, bx, by, bz, maxDrop)) return;

            entity.GetBehavior<EntityBehaviorTaskAI>()?.PathTraverser?.Stop();
            agent.Controls.WalkVector.Set(0, 0, 0);
            agent.Controls.Forward = false;
            agent.ServerControls.Forward = false;
            // Momentum toward the edge goes too; motion along it or away stays.
            double ux = wx / len, uz = wz / len;
            double toward = entity.Pos.Motion.X * ux + entity.Pos.Motion.Z * uz;
            if (toward > 0)
            {
                entity.Pos.Motion.X -= ux * toward;
                entity.Pos.Motion.Z -= uz * toward;
            }
        }
    }
}
