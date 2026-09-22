namespace QQJevHud.Services;

public sealed class FrameDebouncer
{
    private readonly TimeSpan _settleTime;
    private string? _candidate;
    private string? _delivered;
    private DateTimeOffset _changedAt;

    public FrameDebouncer(TimeSpan settleTime) => _settleTime = settleTime;

    public bool Observe(string signature, DateTimeOffset now)
    {
        if (!string.Equals(signature, _candidate, StringComparison.Ordinal))
        {
            _candidate = signature;
            _changedAt = now;
            return false;
        }
        if (string.Equals(signature, _delivered, StringComparison.Ordinal) || now - _changedAt < _settleTime) return false;
        _delivered = signature;
        return true;
    }

    public void Reset()
    {
        _candidate = null;
        _delivered = null;
        _changedAt = default;
    }
}
