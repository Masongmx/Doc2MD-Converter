using System.Globalization;
using System.Windows.Data;
using Doc2MD.Constants;

namespace Doc2MD.Converters;

/// <summary>
/// 双向转换器：磅值(double) ↔ 中文字号名(string)。
/// 用于 ComboBox 绑定，UI 显示"二号"/"三号"等中文字号，内部仍存储磅值。
/// </summary>
public class FontSizeToChineseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double pt)
            return ChineseFontSize.GetName(pt);
        return ChineseFontSize.GetName(16.0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name)
        {
            var pt = ChineseFontSize.TryGetPt(name);
            if (pt.HasValue) return pt.Value;
        }
        return 16.0; // 回退默认值
    }
}
