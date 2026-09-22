using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using QQJevHud.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace QQJevHud.Views;

public sealed class OverlayCardViewModel : INotifyPropertyChanged
{
    /// <summary>Extra height one choice row adds when the card is expanded.</summary>
    private const double ChoiceRowHeight = 58;

    private bool _isExpanded;

    public required DecisionCard Card { get; init; }
    public string MessageId => Card.MessageId;
    public double Left { get; init; }
    public double Top { get; init; }
    public double Width { get; init; }

    /// <summary>Height the layout engine reserved; grows while the choices are expanded.</summary>
    public double Height => IsExpanded && HasChoices ? BaseHeight + ChoiceRows.Count * ChoiceRowHeight + 24 : BaseHeight;
    public double BaseHeight { get; init; }

    public bool IsMarker { get; init; }
    public bool ShowOptions { get; init; } = true;
    public bool ShowRisk { get; init; } = true;
    public bool ShowAdvice { get; init; } = true;

    /// <summary>The message this card judges, so it can be matched to the chat after a scroll.</summary>
    public string MessageText { get; init; } = string.Empty;

    /// <summary>Speaker in a group chat, or the contact in a 1:1 chat.</summary>
    public string? Sender { get; init; }

    /// <summary>The replies offered for this message, each with Jev's predicted outcome.</summary>
    public ObservableCollection<ChoiceRowViewModel> ChoiceRows { get; } = new();

    public bool HasChoices => ChoiceRows.Count > 0;

    /// <summary>True once the choices have been requested but have not arrived yet.</summary>
    public bool IsWaiting { get; private set; }

    /// <summary>Transient feedback after clicking a reply (filled into QQ, or copied instead).</summary>
    public string? StatusHint { get; private set; }

    public string ExpandHint => !HasChoices
        ? (IsWaiting ? "正在想怎么回…" : "没有候选回复")
        : IsExpanded ? "收起 ▴" : "怎么回 ▾";

    public void SetStatus(string? hint)
    {
        StatusHint = hint;
        Raise(nameof(StatusHint));
        Raise(nameof(HasStatusHint));
    }

    public bool HasStatusHint => !string.IsNullOrWhiteSpace(StatusHint);

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Raise();
            Raise(nameof(ExpandHint));
            Raise(nameof(ShowChoices));
            Raise(nameof(Height));
        }
    }

    public bool ShowChoices => IsExpanded && HasChoices;

    public string SenderPrefix => string.IsNullOrWhiteSpace(Sender) ? string.Empty : $"{Sender}：";
    public string QuoteText => Truncate(MessageText, 52);
    public bool HasQuote => !string.IsNullOrWhiteSpace(MessageText);

    public string RiskText => Card.RiskLevel == 0 ? Card.RiskLabel : $"{Card.RiskLabel} · {Card.RiskLevel} / 10";
    public Brush RiskBrush => Card.RiskLevel >= 8
        ? Brushes.IndianRed
        : Card.RiskLevel >= 5 ? Brushes.DarkGoldenrod : Brushes.SeaGreen;

    public string StateText => Card.State switch
    {
        AnalysisState.Loading => "Jev 正在判断…",
        AnalysisState.LowConfidence => "上下文不足",
        AnalysisState.Failed => "本次判断失败",
        AnalysisState.Unavailable => "服务未启用",
        _ => $"置信度 {Card.Confidence}%"
    };

    /// <summary>Rich dimensions, trimmed to two options each so the card stays compact.</summary>
    public IReadOnlyList<DimensionRowViewModel> Dimensions => Card.Dimensions.Count == 0
        ? Array.Empty<DimensionRowViewModel>()
        : Card.VisibleDimensions(CardMetrics.MaxDimensions)
            .Select(dimension => DimensionRowViewModel.From(dimension with { Options = dimension.Options.Take(2).ToArray() }))
            .ToArray();

    public bool HasDimensions => Card.Dimensions.Count > 0;
    public bool ShowDimensions => ShowOptions && HasDimensions;
    public bool ShowPlainOptions => ShowOptions && !HasDimensions;

    /// <summary>Shows the choices that arrived for this message, in the order Jev ranked them.</summary>
    public void ApplyChoices(ChoiceSet set)
    {
        ChoiceRows.Clear();
        foreach (var choice in set.Ordered.Take(4)) ChoiceRows.Add(ChoiceRowViewModel.From(choice));
        IsWaiting = false;
        Raise(nameof(HasChoices));
        Raise(nameof(IsWaiting));
        Raise(nameof(ExpandHint));
        Raise(nameof(ShowChoices));
        Raise(nameof(Height));
    }

    /// <summary>Marks the card as waiting for its choices, so the hint stays honest.</summary>
    public void MarkWaiting()
    {
        if (IsWaiting) return;
        IsWaiting = true;
        Raise(nameof(IsWaiting));
        Raise(nameof(ExpandHint));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string Truncate(string text, int max) =>
        string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";
}

/// <summary>One selectable reply inside an expanded card, with the outcome Jev predicts for it.</summary>
public sealed class ChoiceRowViewModel
{
    public required int Index { get; init; }
    public required string Text { get; init; }
    public required string ToneName { get; init; }
    public required Brush AccentBrush { get; init; }
    public string OutcomeText { get; init; } = string.Empty;
    public string RiskText { get; init; } = string.Empty;

    public static ChoiceRowViewModel From(ChoiceItem choice)
    {
        var risk = choice.Outcome?.RiskLevel ?? 0;
        return new ChoiceRowViewModel
        {
            Index = choice.Index,
            Text = choice.Text,
            ToneName = choice.ToneName,
            AccentBrush = risk >= 8 ? Brushes.IndianRed : risk >= 5 ? Brushes.DarkGoldenrod : Brushes.SeaGreen,
            OutcomeText = choice.Outcome is { } outcome
                ? $"→ {outcome.Reaction}" + (outcome.ReactionProbability > 0 ? $" {outcome.ReactionProbability}%" : string.Empty)
                : string.Empty,
            RiskText = choice.Outcome is { } o ? o.RiskLabel : string.Empty
        };
    }
}
