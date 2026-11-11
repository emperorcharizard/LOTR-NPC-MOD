using System;
using Vintagestory.API.Server;

namespace LotrNpcs
{
    /// <summary>Saved-game data this mod keeps under its own keys.</summary>
    public static class SavedData
    {
        /// <summary>
        /// A save that can't be read (0.0.113). The next world save writes over the key with whatever is loaded (an
        /// empty list), so the unreadable bytes are first copied to a key of their own, for an admin to recover.
        /// </summary>
        public static void KeepUnreadable(ICoreServerAPI sapi, string key, byte[] bytes, string what, Exception e)
        {
            string copy = key + "-unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            sapi.WorldManager.SaveGame.StoreData(copy, bytes);
            sapi.Logger.Error("[lotrnpcs] Could not read the " + what + " (" + e.Message + "). The unreadable data was kept under the save key " + copy + ".");
        }
    }
}
