using System.Globalization;
using System.Resources;

namespace NetWatcher.Localization;

/// <summary>
/// The UI strings. The neutral (English) resource is the fallback for every
/// culture; <c>Strings.*.resx</c> ships the translations. XAML uses these
/// properties through <c>x:Static</c>, so the shell is rebuilt when the
/// language changes.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("NetWatcher.Localization.Strings", typeof(Strings).Assembly);

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string ConnectionsPageTitle => Get(nameof(ConnectionsPageTitle));
    public static string DnsPageTitle => Get(nameof(DnsPageTitle));
    public static string SettingsPageTitle => Get(nameof(SettingsPageTitle));

    public static string VisibleCountFormat => Get(nameof(VisibleCountFormat));

    public static string FilterConnectionsPlaceholder => Get(nameof(FilterConnectionsPlaceholder));
    public static string FilterConnectionsName => Get(nameof(FilterConnectionsName));
    public static string FilterDnsPlaceholder => Get(nameof(FilterDnsPlaceholder));
    public static string FilterDnsName => Get(nameof(FilterDnsName));
    public static string FilterByProcess => Get(nameof(FilterByProcess));
    public static string ShowConnectionsForProcess => Get(nameof(ShowConnectionsForProcess));
    public static string AllProcesses => Get(nameof(AllProcesses));
    public static string UniqueIps => Get(nameof(UniqueIps));
    public static string UniqueIpsToolTip => Get(nameof(UniqueIpsToolTip));

    public static string Pause => Get(nameof(Pause));
    public static string Resume => Get(nameof(Resume));
    public static string PauseToolTip => Get(nameof(PauseToolTip));
    public static string Clear => Get(nameof(Clear));
    public static string ClearToolTip => Get(nameof(ClearToolTip));
    public static string Export => Get(nameof(Export));
    public static string ExportToolTip => Get(nameof(ExportToolTip));
    public static string CsvFileFilter => Get(nameof(CsvFileFilter));
    public static string MarkdownFileFilter => Get(nameof(MarkdownFileFilter));
    public static string JsonFileFilter => Get(nameof(JsonFileFilter));

    public static string EtwUnavailableTitle => Get(nameof(EtwUnavailableTitle));
    public static string EtwUnavailableMessage => Get(nameof(EtwUnavailableMessage));

    public static string ColumnTime => Get(nameof(ColumnTime));
    public static string ColumnProcess => Get(nameof(ColumnProcess));
    public static string ColumnPid => Get(nameof(ColumnPid));
    public static string ColumnDomain => Get(nameof(ColumnDomain));
    public static string ColumnIp => Get(nameof(ColumnIp));
    public static string ColumnPort => Get(nameof(ColumnPort));
    public static string ColumnProtocol => Get(nameof(ColumnProtocol));
    public static string ColumnState => Get(nameof(ColumnState));
    public static string ColumnSource => Get(nameof(ColumnSource));
    public static string ColumnLastSeen => Get(nameof(ColumnLastSeen));
    public static string ColumnAddresses => Get(nameof(ColumnAddresses));
    public static string ColumnStatus => Get(nameof(ColumnStatus));

    public static string CopyIp => Get(nameof(CopyIp));
    public static string CopyDomain => Get(nameof(CopyDomain));
    public static string CopyRow => Get(nameof(CopyRow));
    public static string Details => Get(nameof(Details));

    public static string NoConnectionsTitle => Get(nameof(NoConnectionsTitle));
    public static string NoConnectionsMessage => Get(nameof(NoConnectionsMessage));
    public static string NoMatchingConnectionsTitle => Get(nameof(NoMatchingConnectionsTitle));
    public static string NoMatchingConnectionsMessage => Get(nameof(NoMatchingConnectionsMessage));
    public static string NoDnsTitle => Get(nameof(NoDnsTitle));
    public static string NoDnsMessage => Get(nameof(NoDnsMessage));
    public static string NoMatchingDnsTitle => Get(nameof(NoMatchingDnsTitle));
    public static string NoMatchingDnsMessage => Get(nameof(NoMatchingDnsMessage));

    public static string StatusLineFormat => Get(nameof(StatusLineFormat));
    public static string Yes => Get(nameof(Yes));
    public static string No => Get(nameof(No));
    public static string EtwRunning => Get(nameof(EtwRunning));
    public static string EtwUnavailable => Get(nameof(EtwUnavailable));

    public static string DnsSummaryFormat => Get(nameof(DnsSummaryFormat));
    public static string DnsFooterFormat => Get(nameof(DnsFooterFormat));

    public static string ConnectionDetails => Get(nameof(ConnectionDetails));
    public static string Close => Get(nameof(Close));
    public static string DetailsProcess => Get(nameof(DetailsProcess));
    public static string DetailsDomain => Get(nameof(DetailsDomain));
    public static string DetailsRemote => Get(nameof(DetailsRemote));
    public static string DetailsLocalPort => Get(nameof(DetailsLocalPort));
    public static string DetailsProtocol => Get(nameof(DetailsProtocol));
    public static string DetailsSource => Get(nameof(DetailsSource));
    public static string DetailsFirstSeen => Get(nameof(DetailsFirstSeen));
    public static string DetailsLastSeen => Get(nameof(DetailsLastSeen));

    public static string OriginDns => Get(nameof(OriginDns));
    public static string OriginSharedCdn => Get(nameof(OriginSharedCdn));
    public static string OriginPtr => Get(nameof(OriginPtr));
    public static string OriginNone => Get(nameof(OriginNone));
    public static string OriginUnknown => Get(nameof(OriginUnknown));

    public static string SettingsTheme => Get(nameof(SettingsTheme));
    public static string SettingsThemeDescription => Get(nameof(SettingsThemeDescription));
    public static string ThemeSystem => Get(nameof(ThemeSystem));
    public static string ThemeLight => Get(nameof(ThemeLight));
    public static string ThemeDark => Get(nameof(ThemeDark));
    public static string SettingsLanguage => Get(nameof(SettingsLanguage));
    public static string SettingsLanguageDescription => Get(nameof(SettingsLanguageDescription));
    public static string LanguageSystem => Get(nameof(LanguageSystem));
}
