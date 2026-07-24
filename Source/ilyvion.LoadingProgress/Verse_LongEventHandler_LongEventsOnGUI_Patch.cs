using System.Reflection.Emit;
using ilyvion.LoadingProgress.FasterGameLoading;

namespace ilyvion.LoadingProgress;

// Shared by both patches below so the FasterGameLoading window's actual drawn side and the
// status box's layout math can't drift apart from each other.
internal static class FasterGameLoadingWindowLayout
{
    public static bool GoesAboveMainWindow(
        Vector2 mainWindowPosition,
        Vector2 mainWindowSize,
        Vector2 fasterGameLoadingWindowSize
    )
    {
        if (fasterGameLoadingWindowSize.y <= 0f)
        {
            return false;
        }

        var mainWindowBottom = mainWindowPosition.y + mainWindowSize.y;
        var fitsBelow = mainWindowBottom + 10f + fasterGameLoadingWindowSize.y <= UI.screenHeight;

        return !fitsBelow && mainWindowPosition.y - 10f - fasterGameLoadingWindowSize.y >= 0f;
    }
}

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.DrawLongEventWindowContents))]
internal sealed class Verse_LongEventHandler_DrawLongEventWindowContents_Patch
{
    private static void Postfix()
    {
        if (LoadingProgressWindow.CurrentStage == LoadingStage.Finished)
        {
            return;
        }

        var loadingProgressWindowSize = LoadingProgressWindow.WindowSize;
        var fasterGameLoadingProgressWindowSize = FasterGameLoadingProgressWindow.WindowSize;
        var loadingProgressWindowCenteredX = (UI.screenWidth - loadingProgressWindowSize.x) / 2f;
        var loadingProgressWindowPosition = LoadingProgressMod
            .Settings
            .LoadingWindowPlacement switch
        {
            LoadingWindowPlacement.Top => new(
                loadingProgressWindowCenteredX,
                10f + LongEventHandler.StatusRectSize.y + 10f
            ),
            LoadingWindowPlacement.Middle => new(
                loadingProgressWindowCenteredX,
                (
                    UI.screenHeight
                    - loadingProgressWindowSize.y
                    - fasterGameLoadingProgressWindowSize.y
                ) / 2f
            ),
            LoadingWindowPlacement.Bottom => new(
                loadingProgressWindowCenteredX,
                UI.screenHeight
                    - loadingProgressWindowSize.y
                    - fasterGameLoadingProgressWindowSize.y
                    - 10f
                    - (fasterGameLoadingProgressWindowSize.y > 0 ? 10f : 0f)
            ),
            LoadingWindowPlacement.Custom => CustomPlacement.GetPosition(
                loadingProgressWindowSize,
                new Vector2(UI.screenWidth, UI.screenHeight),
                LoadingProgressMod.Settings.CustomPlacementRelativePosition
            ),
            _ => Vector2.zero,
        };

        Rect rect = new(
            loadingProgressWindowPosition.x,
            loadingProgressWindowPosition.y,
            loadingProgressWindowSize.x,
            loadingProgressWindowSize.y
        );

        var useStandardWindow = LongEventHandler.currentEvent.UseStandardWindow;
        if (!useStandardWindow || Find.UIRoot == null || Find.WindowStack == null)
        {
            Widgets.DrawShadowAround(rect);
            Widgets.DrawWindowBackground(rect);
            LoadingProgressWindow.DrawContents(rect);
        }
        else
        {
            LoadingProgressWindow.DrawWindow(rect);
        }

        var fasterGameLoadingGoesAbove = FasterGameLoadingWindowLayout.GoesAboveMainWindow(
            rect.position,
            rect.size,
            fasterGameLoadingProgressWindowSize
        );
        Vector2 fasterGameLoadingProgressWindowPosition = new(
            rect.x + ((rect.width - fasterGameLoadingProgressWindowSize.x) / 2f),
            fasterGameLoadingGoesAbove
                ? rect.y - 10f - fasterGameLoadingProgressWindowSize.y
                : rect.yMax + 10f
        );
        rect = new(
            fasterGameLoadingProgressWindowPosition.x,
            fasterGameLoadingProgressWindowPosition.y,
            fasterGameLoadingProgressWindowSize.x,
            fasterGameLoadingProgressWindowSize.y
        );
        if (!useStandardWindow || Find.UIRoot == null || Find.WindowStack == null)
        {
            Widgets.DrawShadowAround(rect);
            Widgets.DrawWindowBackground(rect);
            FasterGameLoadingProgressWindow.DrawContents(rect);
        }
        else
        {
            FasterGameLoadingProgressWindow.DrawWindow(rect);
        }
    }
}

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsOnGUI))]
internal sealed class Verse_LongEventHandler_LongEventsOnGUI_Patch
{
    private static readonly MethodInfo _method_GenUI_Rounded = AccessTools.Method(
        typeof(GenUI),
        nameof(GenUI.Rounded),
        [typeof(Rect)]
    );
    private static readonly MethodInfo _methodAdjustStatusWindowRect = AccessTools.Method(
        typeof(Verse_LongEventHandler_LongEventsOnGUI_Patch),
        nameof(AdjustStatusWindowRect)
    );

    private static Rect AdjustStatusWindowRect(Rect r)
    {
        if (LoadingProgressWindow.CurrentStage == LoadingStage.Finished)
        {
            return r;
        }

        var statusRectSize = LongEventHandler.StatusRectSize;
        var loadingProgressWindowSize = LoadingProgressWindow.WindowSize;
        var fasterGameLoadingProgressWindowSize = FasterGameLoadingProgressWindow.WindowSize;

        float statusRectTop = 0;
        switch (LoadingProgressMod.Settings.LoadingWindowPlacement)
        {
            case LoadingWindowPlacement.Top:
                statusRectTop = 10f;
                break;
            case LoadingWindowPlacement.Middle:
                statusRectTop =
                    (
                        (
                            UI.screenHeight
                            - loadingProgressWindowSize.y
                            - fasterGameLoadingProgressWindowSize.y
                        ) / 2f
                    )
                    - statusRectSize.y
                    - 10f;
                break;
            case LoadingWindowPlacement.Bottom:
                statusRectTop =
                    UI.screenHeight
                    - loadingProgressWindowSize.y
                    - fasterGameLoadingProgressWindowSize.y
                    - 10f
                    - (fasterGameLoadingProgressWindowSize.y > 0 ? 10f : 0f)
                    - statusRectSize.y
                    - 10f;
                break;
            case LoadingWindowPlacement.Custom:
                var customPosition = CustomPlacement.GetPosition(
                    loadingProgressWindowSize,
                    new Vector2(UI.screenWidth, UI.screenHeight),
                    LoadingProgressMod.Settings.CustomPlacementRelativePosition
                );
                // The loading window can be dragged anywhere, including flush against the top or
                // bottom of the screen, so there isn't always room to put the status box on its
                // usual side, or for the FasterGameLoading window to stay below it; work out where
                // the FasterGameLoading window actually ends up (same logic used to draw it) and
                // put the status box on whichever side of the combined block still has room.
                var fasterGameLoadingGoesAbove = FasterGameLoadingWindowLayout.GoesAboveMainWindow(
                    customPosition,
                    loadingProgressWindowSize,
                    fasterGameLoadingProgressWindowSize
                );
                var blockTop = fasterGameLoadingGoesAbove
                    ? customPosition.y - 10f - fasterGameLoadingProgressWindowSize.y
                    : customPosition.y;
                var blockBottom = fasterGameLoadingGoesAbove
                    ? customPosition.y + loadingProgressWindowSize.y
                    : customPosition.y
                        + loadingProgressWindowSize.y
                        + (
                            fasterGameLoadingProgressWindowSize.y > 0
                                ? 10f + fasterGameLoadingProgressWindowSize.y
                                : 0f
                        );
                statusRectTop =
                    blockTop >= statusRectSize.y + 20f
                        ? blockTop - statusRectSize.y - 10f
                        : blockBottom + 10f;
                // r.width is the box's actual current width, which can be wider than
                // LongEventHandler.StatusRectSize.x when the status text is long; centering on
                // the static field's width instead of the real one is what threw this off.
                r.x = Math.Clamp(
                    customPosition.x + ((loadingProgressWindowSize.x - r.width) / 2f),
                    0f,
                    UI.screenWidth - r.width
                );
                break;
            default:
                break;
        }
        r.y = Math.Clamp(statusRectTop, 0f, UI.screenHeight - statusRectSize.y);
        return r;
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _method_GenUI_Rounded
        );
        if (!codeMatcher.IsValid)
        {
            LoadingProgressMod.Error(
                $"Could not patch LongEventHandler.LongEventsOnGUI, IL does not match expectations ([call GenUI.Rounded])"
            );
            return originalInstructionList;
        }

        _ = codeMatcher.Advance(1).Insert([new(OpCodes.Call, _methodAdjustStatusWindowRect)]);

        return codeMatcher.Instructions();
    }
}
