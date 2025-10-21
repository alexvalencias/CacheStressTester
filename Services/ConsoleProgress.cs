namespace CacheStressTester.Services;

public class ConsoleProgress
{
    private readonly int _total;
    private readonly int _width;
    private bool _active = true; // Failsafe
    private int _lastPercent = -1;

    public ConsoleProgress(int total, int width = 40)
    {
        _total = Math.Max(1, total);
        _width = width;
    }

    public void Update(int current, string message = "")
    {
        if (!_active) return;

        current = Math.Clamp(current, 0, _total);
        double pct = (double)current / _total;
        int percent = (int)Math.Round(pct * 100);
        if (percent == _lastPercent && string.IsNullOrEmpty(message))
            return;

        _lastPercent = percent;

        int filled = (int)Math.Round(pct * _width);
        int unfilled = _width - filled;

        string bar = new string('#', filled) + new string('-', unfilled);
        Console.Write($"\r[{bar}] {percent,3}% {message.PadRight(30)}");
    }

    /// <summary>
    /// Marks the progress as stopped
    /// preserving the last percent and showing a stopping message
    /// </summary>
    public void End(string message = "Stopping...")
    {
        if (!_active) return;
        _active = false;

        // Reuse last % and progress bar
        int filled = (int)Math.Round((_lastPercent / 100.0) * _width);
        int unfilled = _width - filled;
        string bar = new string('#', filled) + new string('-', unfilled);

        Console.Write($"\r[{bar}] {_lastPercent,3}% {message.PadRight(30)}");
        Console.WriteLine();
        Console.WriteLine();
    }

    /// <summary>
    /// Marks completion explicitly and shows 100%
    /// </summary>
    public void Complete(string finalMessage = "Done")
    {
        if (!_active) return;
        _active = false;

        string bar = new string('#', _width);
        Console.WriteLine($"\r[{bar}] 100% {finalMessage.PadRight(30)}");
        Console.WriteLine();
    }
}
