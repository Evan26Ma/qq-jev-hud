using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QQJevHud.Core;
using QQJevHud.Services;
// WPF types that also exist in the project's WinForms / System.Drawing usings.
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;

namespace QQJevHud.Views;

/// <summary>
/// The app's home: what this thing does, whether it is ready to work, and the one button that starts
/// it. Settings stay in their own window (<see cref="SettingsWindow"/>); this page is the introduction
/// and the readiness check.
/// </summary>
public partial class HomeWindow : Window
{
    private readonly HudController _controller;
    private readonly Action _openSettings;

    public HomeWindow(HudController controller, Action openSettings)
    {
        InitializeComponent();
        _controller = controller;
        _openSettings = openSettings;
        _controller.StatusChanged += OnStatusChanged;
        Closed += (_, _) => _controller.StatusChanged -= OnStatusChanged;
        Refresh();
    }

    /// <summary>Re-reads settings and keys, then redraws status, checklist and feature list.</summary>
    public void Refresh()
    {
        var settings = new SettingsStore().Load().Normalize();
        RenderChecklist(settings);
        RenderFeatures();
        StatusText.Text = _controller.IsEnabled ? "自动分析已开启" : "已暂停";
        ToggleButton.Content = _controller.IsEnabled ? "暂停分析" : "开始分析";
    }

    private void OnStatusChanged(string status) => Dispatcher.Invoke(() => StatusText.Text = status);

    /// <summary>The getting-started checklist: each row says what is done and what to do next.</summary>
    private void RenderChecklist(AppSettings settings)
    {
        ChecklistHost.Children.Clear();
        var hasJev = CredentialStore.HasKey(CredentialStore.TypeSafeTarget);
        var hasLlm = CredentialStore.HasKey(CredentialStore.OpenAiTarget);
        var qqRunning = System.Diagnostics.Process.GetProcessesByName("QQ").Length > 0;

        ChecklistHost.Children.Add(Row("① 配置判断服务",
            hasJev || settings.UsesMock,
            hasJev ? "Jev 已配置，可以给出真实判断" : settings.UsesMock ? "当前是离线 Mock 模式（本地假数据）" : "未配置 Jev API Key —— 判断需要它",
            hasJev || settings.UsesMock ? null : "去配置"));

        ChecklistHost.Children.Add(Row("② 配置候选回复（可选）",
            hasLlm,
            hasLlm ? "已配置 OpenAI 兼容 LLM，能生成候选回复" : "未配置 —— 仍可看判断，但没有候选回复",
            hasLlm ? null : "去配置"));

        ChecklistHost.Children.Add(Row("③ 打开 QQ 聊天窗口",
            qqRunning,
            qqRunning ? "QQ 正在运行" : "还没检测到 QQ —— 请先打开并进入一个聊天",
            null));

        ChecklistHost.Children.Add(Row("④ 开始分析",
            _controller.IsEnabled,
            _controller.IsEnabled ? "正在观察新消息，卡片会浮在 QQ 上" : "点击「开始分析」，然后等对方发来新消息",
            _controller.IsEnabled ? null : "开始"));
    }

    /// <summary>One checklist row; <paramref name="action"/> adds a button that fixes the step.</summary>
    private UIElement Row(string title, bool done, string detail, string? action)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var mark = new TextBlock
        {
            Text = done ? "✓" : "○",
            FontSize = 14,
            Width = 22,
            Foreground = done
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57))
                : new SolidColorBrush(Color.FromRgb(0x8B, 0x90, 0x98)),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(mark, 0);
        grid.Children.Add(mark);

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush")
        });
        text.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("SecondaryTextBrush")
        });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (action is not null)
        {
            var button = new Button { Content = action, VerticalAlignment = VerticalAlignment.Center };
            if (action == "开始") button.Click += async (_, _) => await _controller.StartCurrentChatAsync();
            else button.Click += (_, _) => _openSettings();
            Grid.SetColumn(button, 2);
            grid.Children.Add(button);
        }
        return grid;
    }

    /// <summary>What the app does, in the user's terms.</summary>
    private void RenderFeatures()
    {
        FeaturesHost.Children.Clear();
        FeaturesHost.Children.Add(Feature("只看不碰", "不注入、不修改 QQ，只读当前窗口；截图只在内存里，不落盘。"));
        FeaturesHost.Children.Add(Feature("看懂对方", "新消息一到，浮出一张卡：谁的哪句话、真实意图、情绪、潜台词，以及该怎么回。"));
        FeaturesHost.Children.Add(Feature("帮你想回复", "给出 4 个不同话术的候选，每条都标出 Jev 预测的「可能出现的结果」。"));
        FeaturesHost.Children.Add(Feature("点一下就填进去", "点任一候选即填入 QQ 输入框 —— 不会自动发送，确认后再点发送。"));
        FeaturesHost.Children.Add(Feature("越用越准", "在 notes/ 里给重要的人写一份背景卡（约定 / 雷区 / 近况），保存即生效。"));
    }

    private UIElement Feature(string title, string detail)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush")
        });
        stack.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("SecondaryTextBrush")
        });
        return stack;
    }

    private async void StartClick(object sender, RoutedEventArgs e)
    {
        await _controller.StartCurrentChatAsync();
        Refresh();
    }

    private void SettingsClick(object sender, RoutedEventArgs e) => _openSettings();

    private void PreviewCardClick(object sender, RoutedEventArgs e) => Preview.ShowCardPreview();

    private void PreviewChoicesClick(object sender, RoutedEventArgs e) => Preview.ShowChoicePreview();

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
