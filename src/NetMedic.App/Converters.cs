using System.Globalization;
using System.Windows.Data;
using NetMedic.App.Resources;
using NetMedic.Core.Diagnostics;
using NetMedic.Core.Repairs;

namespace NetMedic.App;

/// <summary>AppPage 枚举转 Visibility。</summary>
public sealed class PageToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AppPage page && parameter is string targetStr &&
            Enum.TryParse<AppPage>(targetStr, out var target))
        {
            return page == target ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Finding 转标题文案。</summary>
public sealed class FindingToTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Finding f ? Strings.GetString(f.TitleKey) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Finding 转说明文案。</summary>
public sealed class FindingToExplanationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Finding f ? Strings.GetString(f.ExplanationKey) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>RepairActionDescriptor 转标题。</summary>
public sealed class RepairToTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RepairActionDescriptor a ? Strings.GetString(a.TitleKey) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>RepairActionDescriptor 转描述。</summary>
public sealed class RepairToDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RepairActionDescriptor a ? Strings.GetString(a.DescriptionKey) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>RepairActionDescriptor 转确认文案。</summary>
public sealed class RepairToConfirmConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RepairActionDescriptor a ? Strings.GetString(a.ConfirmationKey) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool 转 是/否。</summary>
public sealed class BoolToYesNoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? (b ? Strings.RepairConfirm_Yes : Strings.RepairConfirm_No) : Strings.RepairConfirm_No;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>null 转 Visibility（非 null 可见）。</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null or string { Length: 0 }
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>反转 bool 转 Visibility（false=可见，true=折叠）。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b
            ? (b ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible)
            : System.Windows.Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
