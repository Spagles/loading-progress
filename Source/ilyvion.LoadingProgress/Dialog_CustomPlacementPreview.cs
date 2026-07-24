namespace ilyvion.LoadingProgress;

internal sealed class Dialog_CustomPlacementPreview : Window
{
    public override Vector2 InitialSize => LoadingProgressWindow.WindowSize;

    public Dialog_CustomPlacementPreview()
    {
        doCloseX = true;
        draggable = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    protected override void SetInitialSizeAndPosition() => SnapToRelativePosition();

    public override void Notify_ResolutionChanged() => SnapToRelativePosition();

    private void SnapToRelativePosition()
    {
        var size = InitialSize;
        var position = CustomPlacement.GetPosition(
            size,
            new Vector2(UI.screenWidth, UI.screenHeight),
            LoadingProgressMod.Settings.CustomPlacementRelativePosition
        );
        windowRect = new Rect(position.x, position.y, size.x, size.y).Rounded();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(
            inRect.ContractedBy(10f),
            "LoadingProgress.CustomPlacementPreview.Tip".Translate()
        );
        Text.Anchor = TextAnchor.UpperLeft;

        // Dragging (handled by the base Window via `draggable`) can pull windowRect anywhere,
        // including off-screen, so re-clamp it every frame and mirror the clamped position back
        // into the setting as a fraction of the drag range instead of only doing so on close.
        var screenSize = new Vector2(UI.screenWidth, UI.screenHeight);
        var maxX = Math.Max(screenSize.x - windowRect.width, 0f);
        var maxY = Math.Max(screenSize.y - windowRect.height, 0f);
        windowRect.x = Math.Clamp(windowRect.x, 0f, maxX);
        windowRect.y = Math.Clamp(windowRect.y, 0f, maxY);

        LoadingProgressMod.Settings.CustomPlacementRelativePosition = new Vector2(
            maxX > 0f ? windowRect.x / maxX : 0f,
            maxY > 0f ? windowRect.y / maxY : 0f
        );
    }
}
