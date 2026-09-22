using System.Diagnostics;
using System.IO;
using System.Windows;
using QQJevHud.Core;
using QQJevHud.Services;

namespace QQJevHud.Views;

/// <summary>
/// Control panel + settings. Edits non-sensitive <see cref="AppSettings"/> (persisted to settings.json)
/// and API keys (persisted only to the Windows Credential Manager). Decision-judgment cards are the
/// same regardless of engine; keys never touch this window's backing store.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly HudController _controller;

    public SettingsWindow(HudController controller, AppSettings settings)
    {
        InitializeComponent();
        _controller = controller;
        for (var i = 1; i <= 8; i++) MaxCards.Items.Add(i);
        LoadFrom(settings);
        UpdateProviderPanels();
        UpdateKeyStatuses();
        _controller.StatusChanged += OnStatusChanged;
        Closed += (_, _) => _controller.StatusChanged -= OnStatusChanged;
    }

    private void LoadFrom(AppSettings s)
    {
        ProviderTypeSafe.IsChecked = !s.UsesOpenAi && !s.UsesMock;
        ProviderOpenAi.IsChecked = s.UsesOpenAi;
        ProviderMock.IsChecked = s.UsesMock;
        OpenAiBaseUrl.Text = s.BaseUrl;
        OpenAiModel.Text = s.Model;
        ProfileGeneral.IsChecked = !JudgmentProfiles.IsRelationship(s.Profile);
        ProfileRelationship.IsChecked = JudgmentProfiles.IsRelationship(s.Profile);
        GenerateRepliesToggle.IsChecked = s.GenerateReplies;
        TonesInput.Text = s.SelectedTones;
        CustomTonesInput.Text = s.CustomTones;
        ShowIntent.IsChecked = s.ShowIntent;
        ShowRisk.IsChecked = s.ShowRisk;
        ShowAdvice.IsChecked = s.ShowAdvice;
        CardOpacity.Value = s.CardOpacity;
        MaxCards.SelectedIndex = Math.Clamp(s.MaxCards, 1, 8) - 1;
        RedactSensitive.IsChecked = s.RedactSensitive;
        AutoStart.IsChecked = s.AutoStart;
        UpdateOpacityLabel();
    }

    private AppSettings Collect() => new AppSettings
    {
        Provider = ProviderOpenAi.IsChecked == true ? "openai" : ProviderMock.IsChecked == true ? "mock" : "typesafe",
        BaseUrl = OpenAiBaseUrl.Text?.Trim() ?? AppSettings.DefaultBaseUrl,
        Model = OpenAiModel.Text?.Trim() ?? AppSettings.DefaultModel,
        Profile = ProfileRelationship.IsChecked == true ? JudgmentProfiles.Relationship : JudgmentProfiles.General,
        GenerateReplies = GenerateRepliesToggle.IsChecked == true,
        SelectedTones = TonesInput.Text?.Trim() ?? AppSettings.DefaultSelectedTones,
        CustomTones = CustomTonesInput.Text?.Trim() ?? string.Empty,
        ShowIntent = ShowIntent.IsChecked == true,
        ShowRisk = ShowRisk.IsChecked == true,
        ShowAdvice = ShowAdvice.IsChecked == true,
        CardOpacity = CardOpacity.Value,
        MaxCards = MaxCards.SelectedIndex >= 0 ? MaxCards.SelectedIndex + 1 : 4,
        RedactSensitive = RedactSensitive.IsChecked == true,
        AutoStart = AutoStart.IsChecked == true
    }.Normalize();

    private void OnStatusChanged(string status) => Dispatcher.Invoke(() => StatusText.Text = status);

    private void UpdateProviderPanels()
    {
        if (JevPanel is null || OpenAiPanel is null) return;   // may fire while XAML is still loading
        JevPanel.Visibility = ProviderTypeSafe.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        OpenAiPanel.Visibility = ProviderOpenAi.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateKeyStatuses()
    {
        JevKeyStatus.Text = CredentialStore.HasKey(CredentialStore.TypeSafeTarget) ? "已配置（Key 在凭据管理器中）" : "未配置";
        OpenAiKeyStatus.Text = CredentialStore.HasKey(CredentialStore.OpenAiTarget) ? "已配置（Key 在凭据管理器中）" : "未配置";
    }

    private void UpdateOpacityLabel()
    {
        if (CardOpacityLabel is null) return;                  // may fire while XAML is still loading
        CardOpacityLabel.Text = $"{CardOpacity.Value * 100:0}%";
    }

    private void ProviderChanged(object sender, RoutedEventArgs e) => UpdateProviderPanels();

    private void CardOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateOpacityLabel();

    private void SaveJevKeyClick(object sender, RoutedEventArgs e)
    {
        var key = JevKey.Password?.Trim();
        if (string.IsNullOrEmpty(key)) { JevKeyStatus.Text = "Key 为空，未保存。"; return; }
        try { CredentialStore.SaveTypeSafeKey(key); JevKey.Clear(); UpdateKeyStatuses(); JevKeyStatus.Text = "已保存到 Windows 凭据管理器。"; }
        catch (Exception ex) { JevKeyStatus.Text = "保存失败：" + ex.Message; }
    }

    private void SaveOpenAiKeyClick(object sender, RoutedEventArgs e)
    {
        var key = OpenAiKey.Password?.Trim();
        if (string.IsNullOrEmpty(key)) { OpenAiKeyStatus.Text = "Key 为空，未保存。"; return; }
        try { CredentialStore.SaveOpenAiKey(key); OpenAiKey.Clear(); UpdateKeyStatuses(); OpenAiKeyStatus.Text = "已保存到 Windows 凭据管理器。"; }
        catch (Exception ex) { OpenAiKeyStatus.Text = "保存失败：" + ex.Message; }
    }

    private async void VerifyJevClick(object sender, RoutedEventArgs e)
    {
        JevKeyStatus.Text = "正在验证…";
        try
        {
            var card = await new TypeSafeDecisionProvider().AnalyzeAsync(TestMessage(), Array.Empty<ChatMessage>(), CancellationToken.None);
            JevKeyStatus.Text = card.State is AnalysisState.Ready or AnalysisState.LowConfidence ? "可用 ✓（已返回真实判断）" : card.Question;
        }
        catch (Exception ex) { JevKeyStatus.Text = "验证失败：" + ex.Message; }
    }

    private async void VerifyOpenAiClick(object sender, RoutedEventArgs e)
    {
        OpenAiKeyStatus.Text = "正在验证…";
        try
        {
            var provider = new OpenAiDecisionProvider(OpenAiBaseUrl.Text, OpenAiModel.Text);
            var card = await provider.AnalyzeAsync(TestMessage(), Array.Empty<ChatMessage>(), CancellationToken.None);
            OpenAiKeyStatus.Text = card.State is AnalysisState.Ready or AnalysisState.LowConfidence ? "可用 ✓（已返回真实判断）" : card.Question;
        }
        catch (Exception ex) { OpenAiKeyStatus.Text = "验证失败：" + ex.Message; }
    }

    private static ChatMessage TestMessage() =>
        new("verify", "verify", MessageDirection.Incoming, "请判断这句话是否期待认真回应。", 1, new ScreenRect());

    private void ToggleClick(object sender, RoutedEventArgs e) => _controller.Toggle();

    private async void AnalyzeClick(object sender, RoutedEventArgs e) => await _controller.StartCurrentChatAsync();

    private void CalibrateClick(object sender, RoutedEventArgs e) => _controller.OpenCalibration();

    private void OpenLogClick(object sender, RoutedEventArgs e)
    {
        var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QQJevHud", "runtime.log");
        try { if (File.Exists(log)) Process.Start(new ProcessStartInfo(log) { UseShellExecute = true }); }
        catch { /* diagnostics must never disturb the HUD */ }
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var settings = Collect();
        _controller.ApplySettings(settings);
        ApplyAutoStart(settings.AutoStart);
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e) => Close();

    private static void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return;
            if (enable) key.SetValue("QQJevHud", Environment.ProcessPath ?? string.Empty);
            else key.DeleteValue("QQJevHud", throwOnMissingValue: false);
        }
        catch { /* auto-start is best-effort */ }
    }
}
