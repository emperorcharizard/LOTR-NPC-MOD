Compile-time reference DLLs for the lotrnpcs mod (since 0.0.29). They replace the hand-written stubs in
tools/stubs, tools/stubs-ess and tools/stubs-harmony, which are no longer used.

How they were made (2026-11-02):
- VintagestoryAPI.dll and VSEssentials.dll: built from the open-source anegostudios vsapi and vsessentialsmod
  at tag 1.22.7, targeting net10.0, with AssemblyVersion forced to 1.0.0.0 so the mod binds to the game's own
  DLLs at runtime (the game's are 1.0.0.0; the source says 1.22.7.0).
  VSEssentials leaves out Loading/JsonPatchLoader.cs and Systems/WorldGen/Standard/WgenCommands.cs, because
  the game's Tavis.JsonPatch fork (0.5.3-vs.2) isn't on NuGet; 0.5.2-beta is used for the rest.
- cairo-sharp.dll: GtkSharp CairoSharp sources plus its Shared folder, with compile-only members VS adds.
  Only needed so VintagestoryAPI's public types resolve.
- 0Harmony.dll: Lib.Harmony 2.4.2 from NuGet (net10.0), the same version the game ships.
- Newtonsoft.Json, protobuf-net, OpenTK.Mathematics, SkiaSharp: from NuGet, as vsapi 1.22.7 references them.

None of these are copied into the mod zip; the game supplies the real DLLs.

Build:  export DOTNET_ROOT=/usr/lib/dotnet PATH="$PATH:/usr/lib/dotnet"
        cd src && dotnet build -c Release -o out          (writes out/lotrnpcs.dll)
Tests:  cd tools/tests/spawnzones && dotnet build -c Release -o out && cp ../../refs/*.dll out/
        dotnet out/zonetest.dll <generated assets>/assets/lotrnpcs/entities
