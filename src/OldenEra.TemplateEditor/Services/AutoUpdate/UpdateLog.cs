using System;
using System.IO;
using System.Text;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public interface IUpdateLog
{
    void Info(string message);
    void Warn(string message, Exception? ex = null);
    void Error(string message, Exception? ex = null);
}

public sealed class UpdateLog : IUpdateLog
{
    private const long MaxBytes = 64 * 1024;
    private readonly string _path;
    // Static lock: all instances target the same default file path, so cross-
    // instance writes must serialize to avoid interleaved File.AppendAllText.
    private static readonly object Gate = new();

    public UpdateLog(string? path = null)
    {
        _path = path ?? UpdatePaths.LogFile;
    }

    public void Info(string message)  => Append("INFO", message, null);
    public void Warn(string message, Exception? ex = null)  => Append("WARN", message, ex);
    public void Error(string message, Exception? ex = null) => Append("ERROR", message, ex);

    private void Append(string level, string message, Exception? ex)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var sb = new StringBuilder();
                sb.Append('[').Append(DateTime.UtcNow.ToString("o")).Append("] ");
                sb.Append(level).Append(": ").AppendLine(message);
                if (ex != null) sb.AppendLine(ex.ToString());

                File.AppendAllText(_path, sb.ToString());
                TrimIfNeeded();
            }
        }
        catch
        {
            // Swallow — never let logging take down the app.
        }
    }

    private void TrimIfNeeded()
    {
        try
        {
            var fi = new FileInfo(_path);
            if (!fi.Exists || fi.Length <= MaxBytes) return;

            byte[] all = File.ReadAllBytes(_path);
            int keep = (int)MaxBytes / 2;
            byte[] tail = new byte[keep];
            Array.Copy(all, all.Length - keep, tail, 0, keep);
            File.WriteAllBytes(_path, tail);
        }
        catch
        {
            // Swallow.
        }
    }
}
