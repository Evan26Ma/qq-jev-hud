using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QQJevHud.Core;
using QQJevHud.Services;
// WPF types that also exist in the project's WinForms / System.Drawing usings.
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;

namespace QQJevHud.Views;

/// <summary>
/// "话我帮你想，发送你来定" — shows candidate replies for the newest incoming message, grouped by
/// tone, each with 复制 / 填入 QQ actions. It is a normal interactive window (unlike the
/// click-through overlay). Sending is always left to the user.
/// </summary>
public partial class ReplyWindow : Window
{
    private ChatMessage? _current;

    public ReplyWindow()
    {
        InitializeComponent();
    }

    /// <summary>A request to regenerate replies for the current message (the controller decides how).</summary>
    public event Action<ChatMessage>? RegenerateRequested;

    public void ShowDraft(ReplyDraft draft)
    {
        _current = new ChatMessage(draft.MessageId, "current", MessageDirection.Incoming, draft.MessageText, 1, new ScreenRect());
        MessageText.Text = string.IsNullOrWhiteSpace(draft.MessageText) ? "（无文本）" : draft.MessageText;
        CandidatesHost.Children.Clear();

        if (draft.Groups.Count == 0)
        {
            CandidatesHost.Children.Add(Hint("暂时没有生成候选。可能是未配置 OpenAI 兼容 API Key，或本次生成失败。"));
            StatusLabel.Text = string.Empty;
            return;
        }

        foreach (var group in draft.Groups) CandidatesHost.Children.Add(BuildToneCard(group));
        StatusLabel.Text = $"已生成 {draft.All.Count()} 条候选 · 发送由你";
    }

    private Border BuildToneCard(ReplyGroup group)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = group.ToneName,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        });
        foreach (var candidate in group.Candidates) panel.Children.Add(BuildCandidateRow(candidate));

        return new Border
        {
            Style = (Style)FindResource("ToneCard"),
            Child = panel
        };
    }

    private UIElement BuildCandidateRow(ReplyCandidate candidate)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrWhiteSpace(candidate.Style))
        {
            stack.Children.Add(new TextBlock
            {
                Text = candidate.Style,
                FontSize = 11,
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            });
        }
        stack.Children.Add(new TextBlock
        {
            Text = candidate.Text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("PrimaryTextBrush"),
            Margin = new Thickness(0, 1, 8, 0)
        });
        Grid.SetColumn(stack, 0);
        row.Children.Add(stack);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var copy = new Button { Content = "复制", Tag = candidate.Text, Margin = new Thickness(0, 0, 6, 0) };
        copy.Click += Copy_Click;
        var fill = new Button { Content = "填入 QQ", Tag = candidate.Text };
        fill.Click += Fill_Click;
        buttons.Children.Add(copy);
        buttons.Children.Add(fill);
        Grid.SetColumn(buttons, 1);
        row.Children.Add(buttons);

        return row;
    }

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush")
    };

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string text) return;
        ReplyInserter.Copy(text);
        StatusLabel.Text = "已复制到剪贴板。";
    }

    private void Fill_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string text) return;
        StatusLabel.Text = ReplyInserter.FillIntoQq(text)
            ? "已填入 QQ 输入框 —— 确认后再点发送。"
            : "QQ 未能聚焦，已复制到剪贴板；请在 QQ 里 Ctrl+V。";
    }

    private void RegenerateClick(object sender, RoutedEventArgs e)
    {
        if (_current is null) { StatusLabel.Text = "还没有可重新生成的消息。"; return; }
        StatusLabel.Text = "正在重新生成候选…";
        RegenerateRequested?.Invoke(_current);
    }
}
