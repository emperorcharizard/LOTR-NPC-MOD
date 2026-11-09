using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// Spawn item icons as head-and-shoulders portraits, like vanilla's villager and trader items (user, 2026-11-03):
    /// the NPC's race model with its face, hair, ears and beard, wearing the armor its rank starts in. The item's
    /// "lotrnpcsPortrait" attribute names the Player Model Lib model, the skin, the preferred facial expression and
    /// the armor (the same "sets" and "items" as lotrnpcs:startinggear). Built once per item on the client, ahead of
    /// time by PortraitWarmUp or else on first render; if anything fails, the item keeps its plain full-body mesh.
    /// </summary>
    public class CollectibleBehaviorNpcPortrait : CollectibleBehavior
    {
        public CollectibleBehaviorNpcPortrait(CollectibleObject collObj) : base(collObj) { }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            MultiTextureMeshRef meshref = NpcPortrait.Cached(capi, itemstack.Collectible);
            if (meshref != null) renderinfo.ModelRef = meshref;
        }

        public override void OnUnloaded(ICoreAPI api) => NpcPortrait.DisposeAll(api);
    }

    /// <summary>
    /// Builds the spawn items' portraits a few at a time once the player is in creative mode, where the LOTR NPCs tab
    /// shows them. Until 0.0.43 all of them (about fifty) were built on the tab's first frame, which stalled the game.
    /// Survival players never see the items, so they never pay for them. Anything not built yet when it's first
    /// drawn is built then, as before.
    /// </summary>
    public static class PortraitWarmUp
    {
        // How often to look for creative mode, how often to build, and for how long each time (at least one portrait).
        const int CheckMs = 2000, StepMs = 100;
        const double BudgetMs = 4;

        public static void Start(ICoreClientAPI capi)
        {
            long waiting = 0;
            waiting = capi.Event.RegisterGameTickListener(_ =>
            {
                if (capi.World.Player?.WorldData?.CurrentGameMode != EnumGameMode.Creative) return;
                capi.Event.UnregisterGameTickListener(waiting);
                BuildAll(capi);
            }, CheckMs);
        }

        static void BuildAll(ICoreClientAPI capi)
        {
            List<CollectibleObject> todo = new List<CollectibleObject>();
            foreach (Item item in capi.World.Items)
            {
                if (item?.Code != null && item.HasBehavior<CollectibleBehaviorNpcPortrait>()) todo.Add(item);
            }
            int next = 0;
            long building = 0;
            Stopwatch watch = new Stopwatch();
            building = capi.Event.RegisterGameTickListener(_ =>
            {
                watch.Restart();
                do
                {
                    if (next >= todo.Count)
                    {
                        capi.Event.UnregisterGameTickListener(building);
                        return;
                    }
                    NpcPortrait.Cached(capi, todo[next++]);
                }
                while (watch.Elapsed.TotalMilliseconds < BudgetMs);
            }, StepMs);
        }
    }

    /// <summary>Puts a portrait mesh together the way Player Model Lib and vanilla gear rendering build the NPC.</summary>
    public static class NpcPortrait
    {
        const string CacheKey = "lotrnpcs-portraits", ModelCacheKey = "lotrnpcs-portraitmodels";

        // The bust: everything from the upper torso up, without the forearms.
        const string BustRoot = "UpperTorso";
        static readonly string[] BustDrops = { "LowerArmL", "LowerArmR" };

        /// <summary>The item's portrait, built the first time it's asked for. Null when it has none or building failed.</summary>
        public static MultiTextureMeshRef Cached(ICoreClientAPI capi, CollectibleObject coll)
        {
            Dictionary<AssetLocation, MultiTextureMeshRef> cache = ObjectCacheUtil.GetOrCreate(capi, CacheKey, () => new Dictionary<AssetLocation, MultiTextureMeshRef>());
            if (cache.TryGetValue(coll.Code, out MultiTextureMeshRef meshref)) return meshref;
            try
            {
                meshref = Build(capi, coll);
            }
            catch (Exception e)
            {
                capi.Logger.Error("[lotrnpcs] Could not build the portrait of {0}, it keeps the plain icon: {1}", coll.Code, e);
            }
            // Null too, so a failed portrait isn't tried again every frame.
            cache[coll.Code] = meshref;
            return meshref;
        }

        /// <summary>Frees every portrait when the world unloads.</summary>
        public static void DisposeAll(ICoreAPI api)
        {
            Dictionary<AssetLocation, MultiTextureMeshRef> cache = ObjectCacheUtil.TryGet<Dictionary<AssetLocation, MultiTextureMeshRef>>(api, CacheKey);
            if (cache == null) return;
            foreach (MultiTextureMeshRef meshref in cache.Values) meshref?.Dispose();
            cache.Clear();
        }

        public static MultiTextureMeshRef Build(ICoreClientAPI capi, CollectibleObject coll)
        {
            JsonObject spec = coll.Attributes?["lotrnpcsPortrait"];
            if (spec == null || !spec.Exists) return null;
            JObject model = Model(capi, new AssetLocation(spec["model"].AsString()));
            if (model == null) return null;

            // Framing comes from the bare body, so every rank of a race is framed alike whatever it wears.
            PortraitTextures bodyTextures = new PortraitTextures(capi);
            Shape body = Compose(capi, model, spec, bodyTextures, false);
            if (body == null) return null;
            float[] frame = Bounds(Tesselate(capi, body, bodyTextures));
            if (frame == null) return null;

            PortraitTextures textures = new PortraitTextures(capi);
            Shape dressed = Compose(capi, model, spec, textures, true);
            MeshData mesh = Tesselate(capi, dressed, textures);
            if (mesh == null || mesh.VerticesCount == 0) return null;
            ModelTransform gui = coll.GuiTransform;
            Frame(mesh, frame, gui == null ? 1 : gui.ScaleXYZ.Y, gui == null ? 0 : gui.Rotation.Y);
            return capi.Render.UploadMultiTextureMesh(mesh);
        }

        /// <summary>A Player Model Lib model's definition, read from its customplayermodels file once per world (each
        /// race's three ranks share one). Only read, never changed.</summary>
        static JObject Model(ICoreClientAPI capi, AssetLocation modelCode)
        {
            Dictionary<AssetLocation, JObject> models = ObjectCacheUtil.GetOrCreate(capi, ModelCacheKey, () => new Dictionary<AssetLocation, JObject>());
            if (models.TryGetValue(modelCode, out JObject model)) return model;
            IAsset asset = capi.Assets.TryGet(new AssetLocation(modelCode.Domain, "config/customplayermodels/" + modelCode.Path + ".json"));
            model = asset == null ? null : JObject.Parse(asset.ToText())[modelCode.Path] as JObject;
            models[modelCode] = model;
            return model;
        }

        static Shape Compose(ICoreClientAPI capi, JObject model, JsonObject spec, PortraitTextures textures, bool withGear)
        {
            AssetLocation shapeLoc = new AssetLocation((string)model["ShapePath"] ?? "game:entity/humanoid/seraph-faceless").WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape shape = Shape.TryGet(capi, shapeLoc);
            if (shape == null) return null;
            string log = shapeLoc.ToShortString();
            shape.ResolveReferences(capi.Logger, log);

            string main = (string)model["MainTextureCode"] ?? "seraph";
            AssetLocation skin = new AssetLocation(spec["skin"].AsString());
            textures.Set(main, skin);
            textures.Set("skinpart-" + main, skin);
            JArray parts = model["SkinnableParts"] as JArray ?? new JArray();

            // Texture parts on other texture codes (eyes, brows, hair colour, face) come first, so they win over the
            // part shapes' own textures, as in game. Parts on the main texture (skin, underwear) are covered above.
            foreach (JObject part in parts.OfType<JObject>().Where(p => (string)p["type"] == "texture"))
            {
                string target = (string)part["textureTarget"];
                if (target == null || target == main || !(Pick(part, spec, skin) is JObject variant) || (string)variant["texture"] == null) continue;
                textures.Set("skinpart-" + target, new AssetLocation((string)variant["texture"]));
            }

            foreach (JObject part in parts.OfType<JObject>().Where(p => (string)p["type"] == "shape"))
            {
                if (!(Pick(part, spec, skin) is JObject variant)) continue;
                string path = (string)variant["shape"]?["base"]
                    ?? ((string)part["shapeTemplate"]?["base"])?.Replace("{code}", (string)variant["code"]);
                if (path == null) continue;
                AssetLocation partLoc = new AssetLocation(path).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                // Missing shapes are normal here: "none" variants have no file.
                if (!capi.Assets.Exists(partLoc)) continue;
                Shape partShape = Shape.TryGet(capi, partLoc);
                if (partShape == null) continue;
                if (part["disableElements"] is JArray disable) shape.RemoveElements(disable.Select(t => (string)t).ToArray());
                partShape.SubclassForStepParenting("skinpart-");
                shape.StepParentShape(partShape, partLoc.ToShortString(), log, capi.Logger, (texcode, loc) => textures.SetIfMissing("skinpart-" + texcode, loc));
            }

            if (withGear)
            {
                string[] willDelete = Array.Empty<string>();
                foreach (Item item in Gear(capi, spec))
                {
                    ItemStack stack = new ItemStack(item);
                    IAttachableToEntity iatta = IAttachableToEntity.FromCollectible(item);
                    if (iatta == null) continue;
                    shape = EntityBehaviorContainer.addGearToShape(capi, null, capi.ItemTextureAtlas, shape, stack, iatta, "default", log, ref willDelete, textures.Gear);
                }
                shape.RemoveElements(willDelete);
            }

            KeepBust(shape);
            return shape;
        }

        /// <summary>The variant of a skin part the portrait shows: the one using the NPC's skin, else the preferred
        /// facial expression or hair, else the first.</summary>
        public static JObject Pick(JObject part, JsonObject spec, AssetLocation skin)
        {
            JArray variants = part["variants"] as JArray;
            if (variants == null || variants.Count == 0) return null;
            List<JObject> list = variants.OfType<JObject>().ToList();
            JObject bySkin = list.FirstOrDefault(v => (string)v["texture"] is string t && new AssetLocation(t).Equals(skin));
            if (bySkin != null) return bySkin;
            string code = (string)part["code"] ?? "";
            string wanted = spec["parts"][code].AsString(null);
            JObject chosen = list.FirstOrDefault(v => (string)v["code"] == wanted);
            if (chosen != null) return chosen;
            // A bald head with the model's default pale features looked faceless (user, 2026-11-03), so hair comes
            // first unless the race has none, as orcs.
            if (code == "hairbase") return list.FirstOrDefault(v => !Bald.Contains((string)v["code"] ?? "")) ?? list[0];
            return list[0];
        }

        static readonly HashSet<string> Bald = new HashSet<string> { "bald", "balding", "indigenousbalding", "none" };

        /// <summary>The first armor set whose items all exist, else the fallback items that exist, as the NPC is dressed.</summary>
        static List<Item> Gear(ICoreClientAPI capi, JsonObject spec)
        {
            foreach (JsonObject set in spec["sets"].AsArray() ?? Array.Empty<JsonObject>())
            {
                List<Item> items = set.AsArray<string>(Array.Empty<string>(), "game").Select(c => GearCode.Item(capi.World, c)).ToList();
                if (items.Count > 0 && items.All(i => i != null)) return items;
            }
            return spec["items"].AsArray<string>(Array.Empty<string>(), "game").Select(c => GearCode.Item(capi.World, c)).Where(i => i != null).ToList();
        }

        /// <summary>Drops everything below the chest (legs, hips and what hangs from them) and the forearms. The
        /// elements above the upper torso stay as invisible joints so it keeps its place.</summary>
        static void KeepBust(Shape shape)
        {
            if (shape.GetElementByName(BustRoot) == null) return;
            shape.Elements = Prune(shape.Elements);
            foreach (string name in BustDrops) shape.RemoveElementByName(name);
        }

        static ShapeElement[] Prune(ShapeElement[] elements)
        {
            if (elements == null) return null;
            List<ShapeElement> kept = new List<ShapeElement>();
            foreach (ShapeElement el in elements)
            {
                if (el.Name != null && el.Name.Equals(BustRoot, StringComparison.InvariantCultureIgnoreCase)) { kept.Add(el); continue; }
                if (!Contains(el, BustRoot)) continue;
                foreach (ShapeElementFace face in el.FacesResolved ?? Array.Empty<ShapeElementFace>())
                {
                    if (face != null) face.Enabled = false;
                }
                el.Children = Prune(el.Children);
                kept.Add(el);
            }
            return kept.ToArray();
        }

        static bool Contains(ShapeElement el, string name) =>
            el.Children != null && el.Children.Any(c => (c.Name != null && c.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) || Contains(c, name));

        static MeshData Tesselate(ICoreClientAPI capi, Shape shape, PortraitTextures textures)
        {
            TesselationMetaData meta = new TesselationMetaData
            {
                TexSource = textures,
                WithJointIds = false,
                WithDamageEffect = false,
                TypeForLogging = "item",
                Rotation = new Vec3f()
            };
            capi.Tesselator.TesselateShape(meta, shape, out MeshData mesh);
            return mesh;
        }

        // minX, minY, minZ, maxX, maxY, maxZ of the visible faces.
        static float[] Bounds(MeshData mesh)
        {
            if (mesh == null || mesh.VerticesCount == 0) return null;
            float[] b = { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < mesh.IndicesCount; i++)
            {
                int v = mesh.Indices[i] * 3;
                for (int a = 0; a < 3; a++)
                {
                    b[a] = Math.Min(b[a], mesh.xyz[v + a]);
                    b[a + 3] = Math.Max(b[a + 3], mesh.xyz[v + a]);
                }
            }
            return b[0] > b[3] ? null : b;
        }

        // How much of the bare bust, from the top, the portrait shows: the head and the top of the shoulders (user,
        // 2026-11-03: "the head, some shoulder, and the armor"). 0.0.38 showed it down to the elbows.
        public const float CropShare = 0.68f;
        // Item units across a GUI slot at guiTransform scale 1: slots are 48 wide and items are drawn 25.6 wide
        // (GuiElementPassiveItemSlot.unscaledSlotSize and unscaledItemSize), and each slot clips what sticks out.
        public const float SlotUnits = 48f / 25.6f;
        // Where the portrait sits in the slot, as shares of the slot's size. 0.0.39 and 0.0.40 centred the bare bust
        // and filled about three quarters of the slot, so helmets, crests and pauldrons were clipped at the slot's
        // edge and the cut chest floated above its bottom (user, 2026-11-03: "the npcs are still cropped weirdly").
        // Now the cut sits on the bottom edge, the bare bust fills BustHeight of the slot, and the portrait shrinks
        // when its gear would reach past TopLimit or HalfWidth from the middle.
        public const float BottomEdge = -0.02f, BustHeight = 0.8f, TopLimit = 0.96f, HalfWidth = 0.47f;

        /// <summary>Crops the bust to the head and shoulders, then scales and places it in the item's space so the
        /// slot shows all of it, standing on the slot's bottom edge. frame is the bare bust's bounds, so every rank of a
        /// race is framed alike unless its gear needs more room. guiScale and guiYaw are the item's guiTransform
        /// scale and turn about the vertical axis.</summary>
        public static void Frame(MeshData mesh, float[] frame, float guiScale, float guiYaw)
        {
            float top = frame[4];
            float bottom = top - (frame[4] - frame[1]) * CropShare;
            Clip(mesh, bottom);
            float cx = (frame[0] + frame[3]) / 2, cz = (frame[2] + frame[5]) / 2;
            float k = Scale(mesh, frame, bottom, guiScale, guiYaw);

            float units = SlotUnits / Math.Max(guiScale, 0.01f);
            float baseY = 0.5f + (BottomEdge - 0.5f) * units;
            for (int v = 0; v < mesh.VerticesCount; v++)
            {
                mesh.xyz[v * 3] = (mesh.xyz[v * 3] - cx) * k + 0.5f;
                mesh.xyz[v * 3 + 1] = (mesh.xyz[v * 3 + 1] - bottom) * k + baseY;
                mesh.xyz[v * 3 + 2] = (mesh.xyz[v * 3 + 2] - cz) * k + 0.5f;
            }
        }

        /// <summary>How much Frame scales the cropped bust: the bare bust BustHeight of the slot high, or less if the
        /// gear would reach past TopLimit or HalfWidth.</summary>
        public static float Scale(MeshData mesh, float[] frame, float bottom, float guiScale, float guiYaw)
        {
            float units = SlotUnits / Math.Max(guiScale, 0.01f);
            float cx = (frame[0] + frame[3]) / 2, cz = (frame[2] + frame[5]) / 2;
            double yaw = guiYaw * Math.PI / 180;
            float cos = (float)Math.Abs(Math.Cos(yaw)), sin = (float)Math.Abs(Math.Sin(yaw));
            float maxY = bottom, radius = 0;
            for (int i = 0; i < mesh.IndicesCount; i++)
            {
                int v = mesh.Indices[i] * 3;
                float y = mesh.xyz[v + 1];
                // Faces Clip removed point at a corner below the cut; those don't show.
                if (y < bottom - 1e-4f) continue;
                float dx = mesh.xyz[v] - cx, dz = mesh.xyz[v + 2] - cz;
                maxY = Math.Max(maxY, y);
                // Across the screen after the GUI's turn, whichever way it turns.
                radius = Math.Max(radius, Math.Max(Math.Abs(dx * cos + dz * sin), Math.Abs(dx * cos - dz * sin)));
            }
            float k = BustHeight * units / Math.Max(frame[4] - bottom, 0.01f);
            if (maxY > bottom) k = Math.Min(k, (TopLimit - BottomEdge) * units / (maxY - bottom));
            if (radius > 0) k = Math.Min(k, HalfWidth * units / radius);
            return k;
        }

        /// <summary>Cuts the mesh off at height y: faces wholly below go, and faces crossing it are shortened, their
        /// corners below slid up their edges with the texture, so the cut looks clean instead of squashed.</summary>
        public static void Clip(MeshData mesh, float y)
        {
            int per = mesh.IndicesPerFace > 0 ? mesh.IndicesPerFace : MeshData.StandardIndicesPerFace;
            HashSet<int> moved = new HashSet<int>();
            int[] quad = new int[4];
            bool[] below = new bool[4];
            for (int f = 0; f + per <= mesh.IndicesCount; f += per)
            {
                int[] idx = mesh.Indices;
                // Tesselated faces are quads drawn as the triangles a b c and a c d.
                bool isQuad = per == 6 && idx[f] == idx[f + 3] && idx[f + 2] == idx[f + 4];
                if (!isQuad)
                {
                    bool all = true;
                    for (int i = f; i < f + per && all; i++) all = mesh.xyz[idx[i] * 3 + 1] < y;
                    if (all) Collapse(idx, f, per);
                    continue;
                }
                quad[0] = idx[f]; quad[1] = idx[f + 1]; quad[2] = idx[f + 2]; quad[3] = idx[f + 5];
                int count = 0;
                for (int i = 0; i < 4; i++)
                {
                    below[i] = mesh.xyz[quad[i] * 3 + 1] < y;
                    if (below[i]) count++;
                }
                if (count == 0) continue;
                if (count == 4)
                {
                    Collapse(idx, f, per);
                    continue;
                }
                for (int i = 0; i < 4; i++)
                {
                    if (!below[i]) continue;
                    int next = (i + 1) % 4, prev = (i + 3) % 4;
                    int to = !below[next] ? next : !below[prev] ? prev : (i + 2) % 4;
                    if (moved.Add(quad[i])) Slide(mesh, quad[i], quad[to], y);
                }
            }
        }

        static void Collapse(int[] idx, int f, int per)
        {
            for (int i = f + 1; i < f + per; i++) idx[i] = idx[f];
        }

        // Moves vertex v along the edge towards vertex to (which is above y) until it is at height y.
        static void Slide(MeshData mesh, int v, int to, float y)
        {
            float yv = mesh.xyz[v * 3 + 1], yt = mesh.xyz[to * 3 + 1];
            if (yt - yv < 1e-6f) return;
            float t = (y - yv) / (yt - yv);
            for (int a = 0; a < 3; a++) mesh.xyz[v * 3 + a] += t * (mesh.xyz[to * 3 + a] - mesh.xyz[v * 3 + a]);
            if (mesh.Uv != null && mesh.Uv.Length >= (Math.Max(v, to) + 1) * 2)
            {
                for (int a = 0; a < 2; a++) mesh.Uv[v * 2 + a] += t * (mesh.Uv[to * 2 + a] - mesh.Uv[v * 2 + a]);
            }
        }
    }

    /// <summary>Texture codes of a portrait, placed in the item texture atlas.</summary>
    public class PortraitTextures : ITexPositionSource
    {
        readonly ICoreClientAPI capi;
        readonly Dictionary<string, AssetLocation> locations = new Dictionary<string, AssetLocation>();
        readonly Dictionary<string, TextureAtlasPosition> placed = new Dictionary<string, TextureAtlasPosition>();
        /// <summary>Armor textures, filled by vanilla's gear code (already in the atlas).</summary>
        public readonly Dictionary<string, CompositeTexture> Gear = new Dictionary<string, CompositeTexture>();

        public PortraitTextures(ICoreClientAPI capi) { this.capi = capi; }

        public void Set(string code, AssetLocation loc) => locations[code] = loc;
        public void SetIfMissing(string code, AssetLocation loc) { if (!locations.ContainsKey(code)) locations[code] = loc; }

        public Size2i AtlasSize => capi.ItemTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (placed.TryGetValue(textureCode, out TextureAtlasPosition pos)) return pos;
                pos = null;
                // The body's own textures first, as in game, where Player Model Lib's texture source answers before
                // the worn gear's. Vanilla clothing and armor add their textures without a prefix, so one named like
                // the skin ("seraph") replaced it, darkening the tormented elf captain's and the Haradrim's heads
                // (user, 2026-11-03: "some texture issues").
                if (locations.TryGetValue(textureCode, out AssetLocation loc))
                {
                    CompositeTexture ct = new CompositeTexture(loc);
                    ct.Bake(capi.Assets);
                    capi.ItemTextureAtlas.GetOrInsertTexture(ct, out _, out pos);
                }
                else if (Gear.TryGetValue(textureCode, out CompositeTexture gear) && gear.Baked != null)
                {
                    pos = capi.ItemTextureAtlas.Positions[gear.Baked.TextureSubId];
                }
                pos ??= capi.ItemTextureAtlas.UnknownTexturePosition;
                placed[textureCode] = pos;
                return pos;
            }
        }
    }
}
