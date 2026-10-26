using System.Collections.Generic;
using UnityEngine;

namespace GungeonEID
{
    /// <summary>Looks up where a pickup's tk2d sprite lives in its texture atlas.</summary>
    public static class SpriteIcons
    {
        public class Icon
        {
            public Texture Texture;
            /// <summary>Atlas uvs of the sprite's corners, in tk2d order: bottom-left, bottom-right, top-left, top-right.</summary>
            public Vector2[] Uvs;
            /// <summary>Size in game pixels: the sprite's world-space bounds at Gungeon's 16 pixels per unit.</summary>
            public Vector2 PixelSize;
        }

        private static readonly Dictionary<int, Icon> Cache = new Dictionary<int, Icon>();

        /// <summary>Size of the pickup's sprite in game pixels, or zero if it has no sprite.</summary>
        public static Vector2 PixelSize(int pickupId)
        {
            var icon = Get(pickupId);
            return icon == null ? Vector2.zero : icon.PixelSize;
        }

        /// <summary>The pickup's sprite, or null if it has none.</summary>
        public static Icon Get(int pickupId)
        {
            if (Cache.TryGetValue(pickupId, out var icon)) return icon;
            icon = Load(pickupId);
            Cache[pickupId] = icon;
            return icon;
        }

        private static Icon Load(int pickupId)
        {
            try
            {
                var pickup = PickupObjectDatabase.GetById(pickupId);
                if (pickup == null || pickup.sprite == null) return null;
                var sprite = pickup.sprite;
                tk2dSpriteDefinition def = null;
                var coll = sprite.Collection;
                if (coll != null && coll.spriteDefinitions != null && sprite.spriteId >= 0 && sprite.spriteId < coll.spriteDefinitions.Length)
                    def = coll.spriteDefinitions[sprite.spriteId];
                if (def == null) def = sprite.GetCurrentSpriteDef();
                if (def == null || def.uvs == null || def.uvs.Length < 4) return null;

                var mat = def.materialInst != null ? def.materialInst : def.material;
                if (mat == null || mat.mainTexture == null) return null;
                var tex = mat.mainTexture;

                // Size from the tk2d bounds (world units, 16 game pixels each), or from the atlas region if
                // the bounds are missing.
                Vector2 e1 = def.uvs[1] - def.uvs[0], e2 = def.uvs[2] - def.uvs[0];
                var px = new Vector2(Mathf.Round(def.boundsDataExtents.x * 16f), Mathf.Round(def.boundsDataExtents.y * 16f));
                if (px.x < 1 || px.y < 1)
                    px = new Vector2(
                        Mathf.Round(new Vector2(e1.x * tex.width, e1.y * tex.height).magnitude),
                        Mathf.Round(new Vector2(e2.x * tex.width, e2.y * tex.height).magnitude));
                if (px.x < 1 || px.y < 1) return null;

                return new Icon
                {
                    Texture = tex,
                    Uvs = new[] { def.uvs[0], def.uvs[1], def.uvs[2], def.uvs[3] },
                    PixelSize = px,
                };
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogDebug($"No icon for pickup {pickupId}: {e.Message}");
                return null;
            }
        }
    }
}
