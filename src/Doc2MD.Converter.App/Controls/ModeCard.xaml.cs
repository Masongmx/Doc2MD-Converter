using System.Windows;
using System.Windows.Controls;

namespace Doc2MD.Controls;

/// <summary>
/// U7: 模式卡片控件。消除 MainWindow 中三段约 40 行 × 3 的重复卡片 XAML，
/// 文案/图标通过依赖属性注入，选中态由 MainWindow 根据 SelectedModeIndex 调用 UpdateSelection 维护。
/// </summary>
public partial class ModeCard : UserControl
{
    public static readonly DependencyProperty ModeIndexProperty = DependencyProperty.Register(
        nameof(ModeIndex), typeof(int), typeof(ModeCard), new PropertyMetadata(0));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(ModeCard), new PropertyMetadata(false));

    public static readonly DependencyProperty ModeNameProperty = DependencyProperty.Register(
        nameof(ModeName), typeof(string), typeof(ModeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(ModeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubDescriptionProperty = DependencyProperty.Register(
        nameof(SubDescription), typeof(string), typeof(ModeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconTextProperty = DependencyProperty.Register(
        nameof(IconText), typeof(string), typeof(ModeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AutomationNameProperty = DependencyProperty.Register(
        nameof(AutomationName), typeof(string), typeof(ModeCard), new PropertyMetadata(string.Empty));

    /// <summary>卡片对应的 AppMode 索引（0=ToMarkdown, 1=MarkdownToDocx, 2=FormatDoc）。</summary>
    public int ModeIndex
    {
        get => (int)GetValue(ModeIndexProperty);
        set => SetValue(ModeIndexProperty, value);
    }

    /// <summary>当前是否为选中卡片（由外部按 SelectedModeIndex 同步）。</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string ModeName
    {
        get => (string)GetValue(ModeNameProperty);
        set => SetValue(ModeNameProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string SubDescription
    {
        get => (string)GetValue(SubDescriptionProperty);
        set => SetValue(SubDescriptionProperty, value);
    }

    public string IconText
    {
        get => (string)GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    public string AutomationName
    {
        get => (string)GetValue(AutomationNameProperty);
        set => SetValue(AutomationNameProperty, value);
    }

    /// <summary>点击卡片时触发（sender 为该 ModeCard，可读取 ModeIndex）。</summary>
    public event RoutedEventHandler? ModeClicked;

    public ModeCard()
    {
        InitializeComponent();
    }

    /// <summary>按全局选中模式索引同步本卡片的选中态。</summary>
    public void UpdateSelection(int selectedModeIndex)
    {
        IsSelected = selectedModeIndex == ModeIndex;
    }

    private void OnCardClick(object sender, RoutedEventArgs e)
    {
        ModeClicked?.Invoke(this, e);
    }
}
