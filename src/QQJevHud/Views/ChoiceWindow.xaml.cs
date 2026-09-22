using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QQJevHud.Core;
using QQJevHud.Services;
// WPF types that also exist in the project's WinForms / System.Drawing usings.
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace QQJevHud.Views;

/// <summary>
/// The one surface that matters: Jev's read of the message *and* the handful of replies you can send,
/// each previewing what it would lead to — the way a visual novel shows where each option leads.
/// Clicking a choice fills it into QQ. Sending is still the user's call.
/// </summary>
public partial class ChoiceWindow : Window
{
    private ChatMessage? _current;

    public ChoiceWindow()
    {
        InitializeComponent();
    }

    /// <summary>Asks the controller to draft a fresh set for the current message.</summary>
    public event Action<ChatMessage>? RegenerateRequested;

    /// <summary>Redraws the whole panel for a new message.</summary>
    public void ShowChoiceSet(ChoiceSet set)
    {
        _current = new ChatMessage(set.MessageId, "current", MessageDirection.Incoming, set.MessageText, 1, new ScreenRect(), set.Sender);
        SpeakerLine.Text = string.IsNullOrWhiteSpace(set.Sender) ? string.Empty : set.Sender!;
        SpeakerLine.Visibility = string.IsNullOrWhiteSpace(set.Sender) ? Visibility.Collapsed : Visibility.Visible;
        MessageText.Text = string.IsNullOrWhiteSpace(set.MessageText) ? "（无文本）" : set.MessageText;

        RenderJudgment(set.Card);
        RenderChoices(set.Ordered, set.Card);

        StatusLabel.Text = set.Choices.Count == 0
            ? "暂时没有候选。可能是未配置 OpenAI 兼容 API Key，或本次生成失败。"
            : $"已给出 {set.Choices.Count} 个选择 · 点击即填入 QQ，发送由你决定";
        if (!IsVisible) Show();
    }

    private void RenderJudgment(DecisionCard card)
    {
        JudgmentHost.Children.Clear();

        // Headline: the two starred judgments read as the "scene state".
        foreach (var dimension in card.VisibleDimensions(2))
            JudgmentHost.Children.Add(BuildDimensionLine(dimension, prominent: true));

        // The rest feed the "建议" line rather than crowding the panel.
        var advice = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        advice.Children.Add(new TextBlock
        {
            Text = "建议 ",
            FontSize = 11,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        advice.Children.Add(new TextBlock
        {
            Text = card.RecommendedAction,
            FontSize = 12,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        });
        advice.Children.Add(new Border
        {
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(6, 1, 6, 1),
            CornerRadius = new CornerRadius(3),
            Background = RiskFill(card.RiskLevel),
            Child = new TextBlock
            {
                Text = $"{card.RiskLabel} · {card.RiskLevel}/10",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = RiskInk(card.RiskLevel)
            }
        });
        JudgmentHost.Children.Add(advice);
    }

    private UIElement BuildDimensionLine(JudgmentDimension dimension, bool prominent)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
        row.Children.Add(new TextBlock
        {
            Text = "★ " + dimension.Label,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        foreach (var option in dimension.Options.Take(2))
        {
            row.Children.Add(new TextBlock
            {
                Text = $"{option.Label} {option.Probability}%",
                FontSize = 12,
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = prominent && option == dimension.Options[0]
                    ? (Brush)FindResource("PrimaryTextBrush")
                    : (Brush)FindResource("SecondaryTextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        return row;
    }

    private void RenderChoices(IReadOnlyList<ChoiceItem> choices, DecisionCard card)
    {
        ChoicesHost.Children.Clear();
        if (choices.Count == 0)
        {
            ChoicesHost.Children.Add(new TextBlock
            {
                Text = "点「重新生成」再试一次，或在设置里配置 OpenAI 兼容 API Key。",
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }
        foreach (var choice in choices) ChoicesHost.Children.Add(BuildChoiceCard(choice));
    }

    /// <summary>One selectable reply: the text, its tone, and where it would lead.</summary>
    private UIElement BuildChoiceCard(ChoiceItem choice)
    {
        var content = new StackPanel();

        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)FindResource("BrandBrush"),
            Child = new TextBlock
            {
                Text = choice.Index.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        head.Children.Add(new TextBlock
        {
            Text = choice.ToneName,
            FontSize = 11,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(head);

        content.Children.Add(new TextBlock
        {
            Text = choice.Text,
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("PrimaryTextBrush")
        });

        // The consequence: what Jev expects to happen if this is what you send.
        if (choice.Outcome is { } outcome)
        {
            var prediction = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            prediction.Children.Add(new TextBlock
            {
                Text = "→ ",
                FontSize = 12,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
            prediction.Children.Add(new TextBlock
            {
                Text = outcome.ReactionProbability > 0 ? $"{outcome.Reaction} {outcome.ReactionProbability}%" : outcome.Reaction,
                FontSize = 12,
                Foreground = RiskInk(outcome.RiskLevel),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            prediction.Children.Add(new TextBlock
            {
                Text = $"　{outcome.RiskLabel}",
                FontSize = 11,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
            content.Children.Add(prediction);
        }

        var card = new Border
        {
            Style = (Style)FindResource("ChoiceCard"),
            Child = content,
            Tag = choice.Text,
            Cursor = Cursors.Hand,
            ToolTip = "点击填入 QQ 输入框（不会自动发送）"
        };
        card.MouseLeftButtonUp += ChoiceClicked;
        return card;
    }

    private void ChoiceClicked(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string text) return;
        var filled = ReplyInserter.FillIntoQq(text);
        StatusLabel.Text = filled
            ? "已填入 QQ 输入框 —— 确认后再点发送。"
            : "QQ 未能聚焦，已复制到剪贴板；请在 QQ 里 Ctrl+V。";
    }

    private void RegenerateClick(object sender, RoutedEventArgs e)
    {
        if (_current is null)
        {
            StatusLabel.Text = "还没有可重新生成的消息。";
            return;
        }
        StatusLabel.Text = "正在重新生成候选…";
        RegenerateRequested?.Invoke(_current);
    }

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        var text = ChoicesHost.Children.OfType<Border>().FirstOrDefault()?.Tag as string;
        if (string.IsNullOrEmpty(text)) return;
        ReplyInserter.Copy(text);
        StatusLabel.Text = "已复制第 1 条到剪贴板。";
    }

    private static Brush RiskFill(int riskLevel) => riskLevel >= 8
        ? new SolidColorBrush(Color.FromRgb(0xFD, 0xE7, 0xE7))
        : riskLevel >= 5 ? new SolidColorBrush(Color.FromRgb(0xFD, 0xF3, 0xDF)) : new SolidColorBrush(Color.FromRgb(0xE7, 0xF5, 0xEC));

    private static Brush RiskInk(int riskLevel) => riskLevel >= 8
        ? new SolidColorBrush(Color.FromRgb(0xC9, 0x3C, 0x37))
        : riskLevel >= 5 ? new SolidColorBrush(Color.FromRgb(0xB7, 0x79, 0x1F)) : new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
}
