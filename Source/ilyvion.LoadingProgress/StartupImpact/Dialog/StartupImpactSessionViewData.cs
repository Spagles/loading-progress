namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

internal sealed class StartupImpactSessionViewData
{
    public static readonly string[] CategoriesTotal =
    [
        "LoadingProgress.StartupImpact.Total.Mods",
        "LoadingProgress.StartupImpact.Total.ModsHidden",
        "LoadingProgress.StartupImpact.Total.BaseGame",
        "LoadingProgress.StartupImpact.Total.Others",
    ];

    private readonly StartupImpactSessionData sessionData;
    private readonly List<StartupImpactSessionModViewData> modViewData;
    private float hiddenModsLoadingTime;

    private readonly List<string> categories = [];
    private readonly List<string> categoriesNonMods = [];
    private readonly List<float> metricsNonMods = [];
    private readonly List<float> metricsTotal = [];
    private readonly Dictionary<string, Color> categoryColorsNonMods = [];
    private readonly List<string> categoriesMods = [];
    private readonly List<float> metricsMods = [];
    private readonly Dictionary<string, Color> categoryColorsMods = [];

    internal IReadOnlyList<StartupImpactSessionModViewData> ModViewData => modViewData.AsReadOnly();

    public float BasegameLoadingTime { get; private set; }
    public float ModsLoadingTime { get; private set; }
    public float MaxImpact { get; private set; }

    public IReadOnlyList<string> Categories => categories.AsReadOnly();
    public IReadOnlyList<string> CategoriesNonMods => categoriesNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsNonMods => metricsNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsTotal => metricsTotal.AsReadOnly();
    public IReadOnlyDictionary<string, Color> CategoryColorsNonMods =>
        categoryColorsNonMods.AsReadOnly();
    public IReadOnlyList<string> CategoriesMods => categoriesMods.AsReadOnly();
    public IReadOnlyList<float> MetricsMods => metricsMods.AsReadOnly();
    public IReadOnlyDictionary<string, Color> CategoryColorsMods => categoryColorsMods.AsReadOnly();

    public StartupImpactSessionViewData(StartupImpactSessionData sessionData)
    {
        this.sessionData = sessionData;
        modViewData = [.. sessionData.Mods.Select(mod => new StartupImpactSessionModViewData(mod))];

        CalculateBaseGameStats();
        CalculateModStats();

        foreach (var modView in modViewData)
        {
            modView.Initialize(this);
        }
    }

    public void CalculateModStats()
    {
        ModsLoadingTime = 0;
        hiddenModsLoadingTime = 0;
        MaxImpact = 0;

        HashSet<string> categorySet = [];
        foreach (var modView in modViewData)
        {
            if (modView.HideInUi)
            {
                hiddenModsLoadingTime += modView.ModData.TotalImpact;
            }
            else
            {
                if (MaxImpact < modView.ModData.TotalImpact)
                {
                    MaxImpact = modView.ModData.TotalImpact;
                }

                ModsLoadingTime += modView.ModData.TotalImpact;
            }

            foreach (var entry in modView.ModData.Metrics)
            {
                _ = categorySet.Add(entry.Key);
            }

            foreach (var entry in modView.ModData.OffThreadMetrics)
            {
                _ = categorySet.Add(entry.Key);
            }
        }

        categories.Clear();
        categories.AddRange(categorySet.OrderBy(category => category));

        var totalLoadingTime = ModsLoadingTime + hiddenModsLoadingTime + BasegameLoadingTime;
        if (sessionData.LoadingTime == 0)
        {
            sessionData.OverrideLoadingTime(totalLoadingTime);
        }
        else if (totalLoadingTime > sessionData.LoadingTime)
        {
            sessionData.OverrideLoadingTime(totalLoadingTime);
        }

        metricsTotal.Clear();
        metricsTotal.AddRange([
            ModsLoadingTime,
            hiddenModsLoadingTime,
            BasegameLoadingTime,
            Math.Max(0, sessionData.LoadingTime - totalLoadingTime),
        ]);

        categoriesMods.Clear();
        metricsMods.Clear();
        foreach (
            var modView in modViewData
                .Where(m => !m.HideInUi && m.ModData.TotalImpact > 0)
                .OrderByDescending(m => m.ModData.TotalImpact)
        )
        {
            var name = modView.ModData.ModName;
            categoryColorsMods[name] = StartupImpactProfilerUtil.HashColor(
                modView.ModData.ModPackageId
            );
            categoriesMods.Add(name);
            metricsMods.Add(modView.ModData.TotalImpact);
        }
    }

    public void CalculateBaseGameStats()
    {
        categoriesNonMods.Clear();
        metricsNonMods.Clear();
        BasegameLoadingTime = 0;

        foreach (var entry in sessionData.Metrics)
        {
            var cat = entry.Key;

            categoryColorsNonMods[cat] = StartupImpactProfilerUtil.HashColor(cat);
            categoriesNonMods.Add(cat);
            metricsNonMods.Add(entry.Value);
            BasegameLoadingTime += entry.Value;
        }
    }
}
