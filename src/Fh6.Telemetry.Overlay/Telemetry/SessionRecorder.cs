using System.IO;
using Fh6.Telemetry.Core;

namespace Fh6.Telemetry.Overlay.Telemetry;

/// <summary>
/// Records the live UDP session to disk so raw telemetry is never silently lost. Frames are
/// appended to a working JSONL as they arrive (written from the pump thread; UI calls Save/
/// StartNew). "Save" materializes the session under its name in the chosen formats and begins
/// a fresh session. With <see cref="AlwaysSave"/> on, each session is persisted automatically
/// on roll-over / exit even without an explicit Save; off, unsaved sessions are discarded.
/// </summary>
public sealed class SessionRecorder : IDisposable
{
    private readonly string _dir;
    private readonly string _workingPath;
    private readonly object _lock = new();
    private JsonlCaptureWriter? _writer;
    private long _frameCount;
    private bool _saved;

    public string SessionName { get; set; } = "";
    public bool AlwaysSave { get; set; }
    public string Directory => _dir;

    public SessionRecorder(string capturesDir)
    {
        _dir = capturesDir;
        System.IO.Directory.CreateDirectory(_dir);
        _workingPath = Path.Combine(_dir, "_active-session.jsonl");
    }

    public static string DefaultName() => $"session-{DateTime.Now:yyyyMMdd-HHmmss}";

    public bool HasData { get { lock (_lock) return _frameCount > 0; } }

    /// <summary>Opens a fresh working file. Auto-saves the previous one first if configured.</summary>
    public void StartNew()
    {
        lock (_lock)
        {
            AutoSaveIfNeeded();
            _writer?.Dispose();
            _writer = new JsonlCaptureWriter(_workingPath); // FileMode.Create truncates
            _frameCount = 0;
            _saved = false;
            SessionName = DefaultName();
        }
    }

    public void Write(in CaptureFrame frame)
    {
        lock (_lock)
        {
            if (_writer is null) return;
            _writer.Write(frame.TimestampMs, frame.Data);
            _frameCount++;
            // Flush ~once a second so an abrupt termination loses at most a second of data.
            if (_frameCount % 60 == 0) _writer.Flush();
        }
    }

    /// <summary>
    /// Materializes the current session as <see cref="SessionName"/> in the selected formats.
    /// Returns the produced file paths. Recording continues into the same working file.
    /// </summary>
    public List<string> Save(bool jsonl, bool csv, bool bin)
    {
        lock (_lock)
        {
            var outputs = new List<string>();
            if (_writer is null || _frameCount == 0) return outputs;
            _writer.Flush();

            var name = Sanitize(string.IsNullOrWhiteSpace(SessionName) ? DefaultName() : SessionName);
            // At least one format; default to JSONL if nothing ticked.
            if (!jsonl && !csv && !bin) jsonl = true;

            if (jsonl)
            {
                var p = Path.Combine(_dir, name + ".jsonl");
                File.Copy(_workingPath, p, overwrite: true);
                outputs.Add(p);
            }
            if (csv)
            {
                var p = Path.Combine(_dir, name + ".csv");
                using var w = new StreamWriter(p);
                CsvExporter.Export(new JsonlReplaySource(_workingPath).Frames(), w);
                outputs.Add(p);
            }
            if (bin)
            {
                var p = Path.Combine(_dir, name + ".bin");
                using var s = File.Create(p);
                BinCapture.Write(new JsonlReplaySource(_workingPath).Frames(), s);
                outputs.Add(p);
            }

            _saved = true;
            return outputs;
        }
    }

    private void AutoSaveIfNeeded()
    {
        if (AlwaysSave && !_saved && _frameCount > 0)
            Save(jsonl: true, csv: false, bin: false);
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            AutoSaveIfNeeded();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
