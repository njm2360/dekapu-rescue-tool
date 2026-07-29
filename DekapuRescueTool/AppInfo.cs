using System.Reflection;

namespace DekapuRescueTool;

public static class AppInfo
{
    public const string DisplayName = "でかプ データ復旧ツール";

    private static readonly Assembly Self = typeof(AppInfo).Assembly;

    public static string Name { get; } = Self.GetName().Name ?? "DekapuRescueTool";

    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var informational = Self
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
            return Self.GetName().Version?.ToString(3) ?? "0.0.0";

        // '+'以降はgitハッシュ等のビルドメタデータなので除く
        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
