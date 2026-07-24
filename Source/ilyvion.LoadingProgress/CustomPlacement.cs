namespace ilyvion.LoadingProgress;

/// <summary>
/// Pure math backing the "Custom" <see cref="LoadingWindowPlacement"/>: a window position
/// stored as a 0..1 fraction of how far it's been dragged across its valid travel range, so it
/// stays proportionally in the same spot (and always fully on-screen) regardless of resolution.
/// </summary>
internal static class CustomPlacement
{
    public static Vector2 ClampRelative(Vector2 relativePosition) =>
        new(Math.Clamp(relativePosition.x, 0f, 1f), Math.Clamp(relativePosition.y, 0f, 1f));

    public static Vector2 GetPosition(
        Vector2 windowSize,
        Vector2 screenSize,
        Vector2 relativePosition
    )
    {
        var maxX = Math.Max(screenSize.x - windowSize.x, 0f);
        var maxY = Math.Max(screenSize.y - windowSize.y, 0f);
        var clamped = ClampRelative(relativePosition);
        return new Vector2(clamped.x * maxX, clamped.y * maxY);
    }
}
