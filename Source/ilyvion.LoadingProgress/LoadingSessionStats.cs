namespace ilyvion.LoadingProgress;

internal static class LoadingSessionStats
{
    internal static int? ModsLoaded { get; private set; }
    internal static int? DefsParsed { get; private set; }
    internal static int? PatchOperationsApplied { get; private set; }

    internal static void Reset()
    {
        ModsLoaded = null;
        DefsParsed = null;
        PatchOperationsApplied = null;
    }

    internal static void RecordStageCompletion(LoadingStage stage, float maxValue)
    {
        var value = (int)maxValue;
#pragma warning disable IDE0010
        switch (stage)
        {
            case LoadingStage.LoadModXml:
                ModsLoaded = value;
                break;
            case LoadingStage.LoadingDefs:
                DefsParsed = value;
                break;
            case LoadingStage.ApplyPatches:
                PatchOperationsApplied = value;
                break;
        }
#pragma warning restore IDE0010
    }
}
