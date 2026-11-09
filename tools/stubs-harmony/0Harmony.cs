// Compile-only reference stub for Harmony 2 (namespace HarmonyLib), which the game ships as Lib/0Harmony.dll.
// Signatures match what Player Model Lib 1.24.0 calls on the game's 0Harmony 2.4.2. Never shipped: at runtime
// the game's own 0Harmony.dll is used. The version is kept low so it binds to whatever 2.x the game has.
using System.Reflection;

namespace HarmonyLib
{
    public class Harmony
    {
        public Harmony(string id) => throw null;
        public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null) => throw null;
        public void UnpatchAll(string harmonyID = null) => throw null;
    }

    public class HarmonyMethod
    {
        public HarmonyMethod(MethodInfo method) => throw null;
    }
}
