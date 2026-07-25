namespace ilyvion.LoadingProgress.StartupImpact;

[HotSwappable]
internal sealed class UiTable(int rowCount, float rowHeight, float[] columnWidthsTemplate)
{
    private readonly float _rowHeight = rowHeight;
    private readonly float[] _columnWidthsTemplate = columnWidthsTemplate;
    private readonly float[] _columnOffsets = new float[columnWidthsTemplate.Length];
    private readonly float[] _columnWidths = new float[columnWidthsTemplate.Length];
    private float _columnLayoutWidth = -1f;
    private Rect _uiRect;
    private Rect _viewRect;
    private Rect _userRect;
    private Vector2 _scrollPosition = Vector2.zero;

    public int RowCount
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            _viewRect.height = field * _rowHeight;
        }
    } = rowCount;

    public void StartTable(float x, float y, float width, float height)
    {
        RecalculateColumns(width - 16f);

        if (_uiRect.x != x || _uiRect.y != y || _uiRect.width != width || _uiRect.height != height)
        {
            _uiRect.x = x;
            _uiRect.y = y;
            _uiRect.width = width;
            _uiRect.height = height;

            _viewRect.x = 0;
            _viewRect.y = 0;
            _viewRect.width = width - 16f;
            _viewRect.height = RowCount * _rowHeight;
        }

        Widgets.BeginScrollView(_uiRect, ref _scrollPosition, _viewRect);
    }

    private void RecalculateColumns(float availableWidth)
    {
        if (_columnLayoutWidth == availableWidth)
        {
            return;
        }
        _columnLayoutWidth = availableWidth;

        float totalNeededWidth = 0;
        var totalAvailableWidth = availableWidth;
        foreach (var cw in _columnWidthsTemplate)
        {
            if (cw > 0)
            {
                totalNeededWidth += cw;
            }
            else
            {
                totalAvailableWidth += cw;
            }
        }

        float xoff = 0;
        var n = 0;
        foreach (var cw in _columnWidthsTemplate)
        {
            var calculatedWidth = cw > 0 ? cw * totalAvailableWidth / totalNeededWidth : -cw;
            _columnOffsets[n] = xoff;
            _columnWidths[n] = calculatedWidth;

            xoff += calculatedWidth;
            n++;
        }
    }

    /// <summary>
    /// Draws a header row above the scrollable table, aligned to the same column
    /// layout, and lets the caller render each column's header cell (e.g. a
    /// clickable sort button).
    /// </summary>
    public void Header(float x, float y, float width, float height, Action<int, Rect> drawColumn)
    {
        RecalculateColumns(width - 16f);
        for (var column = 0; column < _columnWidths.Length; column++)
        {
            drawColumn(
                column,
                new Rect(x + _columnOffsets[column], y, _columnWidths[column], height)
            );
        }
    }

    public bool IsRowVisible(int row)
    {
        // visible area
        Rect viewRect = new(0f, _scrollPosition.y, _uiRect.width, _uiRect.height);

        return Cell(0, row).Overlaps(viewRect);
    }

    public Rect Cell(int column, int row)
    {
        if (column < 0 || column >= _columnWidths.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(column),
                $"Bad column coordinate: {column}"
            );
        }
        if (row < 0 || row >= RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row), $"Bad row coordinate: {row}");
        }

        _userRect.x = _columnOffsets[column];
        _userRect.y = row * _rowHeight;
        _userRect.width = _columnWidths[column];
        _userRect.height = _rowHeight;

        return _userRect;
    }

    public void TruncatedLabel(int column, int row, string text)
    {
        var rect = Cell(column, row);
        Widgets.Label(rect, text.Truncate(rect.width, null));
    }

#pragma warning disable CA1822 // Mark members as static
    public void EndTable() => Widgets.EndScrollView();
#pragma warning restore CA1822 // Mark members as static
}
