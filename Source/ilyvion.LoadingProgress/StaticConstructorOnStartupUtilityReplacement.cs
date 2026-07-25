using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

internal sealed class StaticConstructorOnStartupUtilityReplacement
{
    internal static void Interject() =>
        Utilities.LongEventHandlerPrependQueue(() =>
        {
            LongEventHandler.QueueLongEvent(CallAllAndRest(), "LoadingProgress.CallAll");
            // When we're done, resume the original ExecuteToExecuteWhenFinished method to
            // process whatever toExecuteWhenFinished entries are left.
            LongEventHandler.QueueLongEvent(
                LongEventHandler_ExecuteToExecuteWhenFinished_Patches.ExecuteToExecuteWhenFinished(),
                "LoadingProgress.ExecuteToExecuteWhenFinished"
            );
        });

    internal static bool _callAllCalled;

    // Vanilla's PlayDataLoader.DoPlayLoad() bundles StaticConstructorOnStartupUtility.CallAll(),
    // FloatMenuMakerMap.Init(), GlobalTextureAtlasManager.BakeStaticAtlases(), cache clearing,
    // a forced GC.Collect() and Resources.UnloadUnusedAssets() into a single
    // ExecuteWhenFinished delegate. We can't patch DoPlayLoad itself (it's already run by the
    // time our mod is alive), so the whole closure arrives as one opaque, unyielded unit. We
    // intercept it (see StaticConstructorOnStartupCallAllFinder) and run every one of those
    // steps here ourselves instead, with a yield between each, so the loading screen gets a
    // chance to repaint between them instead of freezing for the combined duration of all of
    // them. The original closure entry is removed from toExecuteWhenFinished (see
    // LongEventHandler_ExecuteToExecuteWhenFinished_Patches) so it never runs a second time.
    private static IEnumerable CallAllAndRest()
    {
        _callAllCalled = true;
        DeepProfiler.Start("StaticConstructorOnStartupUtilityReplacement.CallAll()");
        var list = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartup>();
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];

            LoadingProgressWindow.SetCurrentLoadingActivityRaw(item.ToString());
            LoadingProgressWindow.StageProgress = (i + 1, list.Count);
            yield return null;

            var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(
                Utilities.FindModByAssembly(item.Assembly)
            );
            info?.Start("LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAll");

            try
            {
                var now = DateTime.Now;
                //LoadingProgressMod.Debug($"About to run static constructor for {item} @ {now:HH:mm:ss.fff}");
                RuntimeHelpers.RunClassConstructor(item.TypeHandle);
                //LoadingProgressMod.Debug($"Finished running static constructor for {item} @ {DateTime.Now:HH:mm:ss.fff}; took {DateTime.Now - now:mm\\:ss\\.fff}");
            }
            catch (Exception ex)
            {
                Log.Error("Error in static constructor of " + item?.ToString() + ": " + ex);
            }

            _ = info?.Stop(
                "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAll"
            );

            // RimWorld's UpdateCurrentEnumeratorEvent calls MoveNext() in a tight loop with
            // no rendering in between, only checking its 100ms time budget after each
            // MoveNext() returns. Without this yield, a slow constructor above blows through
            // that budget from inside a single MoveNext() call, and the very next thing that
            // happens before control returns is this loop setting the label for the *next*
            // item. That's what ends up on screen, so the stall gets misattributed to the
            // following mod. Yielding here ensures the label still showing when we finally
            // repaint is the one for the constructor that actually ran.
            yield return null;
        }
        DeepProfiler.End();
        StaticConstructorOnStartupUtility.coreStaticAssetsLoaded = true;

        // Run the real StaticConstructorOnStartupUtility.CallAll() too, purely so any
        // third-party Harmony prefixes/postfixes on it still fire like they normally would.
        // The constructors themselves are cheap the second time around (the CLR no-ops repeat
        // RunClassConstructor calls for a type that's already been initialized).
        LoadingProgressWindow.SetCurrentLoadingActivityRaw(string.Empty);
        yield return null;
        DeepProfiler.Start("Static constructor calls");
        try
        {
            StaticConstructorOnStartupUtility.CallAll();
            if (Prefs.DevMode)
            {
                StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes();
            }
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return null;

        FloatMenuMakerMap.Init();
        yield return null;

        DeepProfiler.Start("Atlas baking.");
        try
        {
            GlobalTextureAtlasManager.BakeStaticAtlases();
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return null;

        DeepProfiler.Start("Garbage Collection");
        try
        {
            RimWorld.IO.AbstractFilesystem.ClearAllCache();
            GC.Collect(int.MaxValue, GCCollectionMode.Forced);
            _ = Resources.UnloadUnusedAssets();
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return null;
    }
}

internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
    private static class StaticConstructorOnStartupCallAllFinder
    {
        private static readonly MethodInfo _method_StaticConstructorOnStartupUtility_CallAll =
            AccessTools.Method(
                typeof(StaticConstructorOnStartupUtility),
                nameof(StaticConstructorOnStartupUtility.CallAll)
            );

        private static readonly CodeMatch[] toMatch =
        [
            new(OpCodes.Call, _method_StaticConstructorOnStartupUtility_CallAll),
        ];

        public static IEnumerable<MethodInfo> FindMethodCalling()
        {
            // Find all possible candidates, both from the wrapping type and all nested types.
            var candidates = Utilities.FindInTypeAndInnerTypeMethods(typeof(PlayDataLoader));

            //check all candidates for the target instructions, return those that match.
            foreach (var method in candidates)
            {
                var instructions = PatchProcessor.GetCurrentInstructions(method);
                var matched = instructions.Matches(toMatch);
                if (matched)
                {
                    yield return method;
                }
            }
            yield break;
        }
    }
}
