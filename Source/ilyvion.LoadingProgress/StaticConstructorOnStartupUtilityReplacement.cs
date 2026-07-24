using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

internal sealed class StaticConstructorOnStartupUtilityReplacement
{
    internal static void Interject() =>
        Utilities.LongEventHandlerPrependQueue(() =>
        {
            LongEventHandler.QueueLongEvent(CallAll(), "LoadingProgress.CallAll");
            // When we're done, resume the original ExecuteToExecuteWhenFinished method.
            LongEventHandler.QueueLongEvent(
                LongEventHandler_ExecuteToExecuteWhenFinished_Patches.ExecuteToExecuteWhenFinished(),
                "LoadingProgress.ExecuteToExecuteWhenFinished"
            );
        });

    internal static bool _callAllCalled;

    private static IEnumerable CallAll()
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
