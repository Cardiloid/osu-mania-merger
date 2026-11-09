using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace OsuManiaMerger
{
    public static class Util
    {
        public static string Fmt(double v)
        {
            if (Math.Abs(v - Math.Round(v)) < 0.0000001)
                return ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
            return v.ToString("0.########", CultureInfo.InvariantCulture);
        }

        public static string FmtSec(int ms)
        {
            return (ms / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static int ParseInt(string s, string file, int line)
        {
            int v;
            if (!int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                throw new FormatException(string.Format(Lang.T("{0} 第 {1} 行：数字格式错误 \"{2}\"", "{0} line {1}: invalid number \"{2}\""), file, line, s));
            return v;
        }

        public static double ParseDouble(string s, string file, int line)
        {
            double v;
            if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw new FormatException(string.Format(Lang.T("{0} 第 {1} 行：数字格式错误 \"{2}\"", "{0} line {1}: invalid number \"{2}\""), file, line, s));
            return v;
        }

        public static string KeyOf(string line)
        {
            int i = line.IndexOf(':');
            return i > 0 ? line.Substring(0, i).Trim() : "";
        }

        public static string ValOf(string line)
        {
            int i = line.IndexOf(':');
            return i >= 0 ? line.Substring(i + 1).Trim() : "";
        }

        public static bool HasKey(List<string> lines, string key)
        {
            foreach (string l in lines)
            {
                if (string.Equals(KeyOf(l), key, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string GetValue(List<string> lines, string key)
        {
            foreach (string l in lines)
            {
                if (string.Equals(KeyOf(l), key, StringComparison.OrdinalIgnoreCase))
                    return ValOf(l);
            }
            return null;
        }

        public static void SetValue(List<string> lines, string key, string value)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (string.Equals(KeyOf(lines[i]), key, StringComparison.OrdinalIgnoreCase))
                    lines.RemoveAt(i);
            }
            lines.Add(key + ": " + value);
        }

        public static string FormatTime(int ms)
        {
            TimeSpan t = TimeSpan.FromMilliseconds(ms);
            if (t.TotalHours >= 1)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:000}",
                    (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:000}",
                t.Minutes, t.Seconds, t.Milliseconds);
        }

        public static string SafeFileName(string s, string fallback)
        {
            if (s == null) s = "";
            s = s.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            s = s.Replace("\\", "_").Replace("/", "_").Trim();
            if (s.Length > 120) s = s.Substring(0, 120).Trim();
            if (s.Length == 0) s = fallback;
            return s;
        }

        public static string EnsureMp3(string name)
        {
            name = SafeFileName(name, "audio");
            if (name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return name;
            string ext = Path.GetExtension(name);
            if (!string.IsNullOrEmpty(ext)) name = name.Substring(0, name.Length - ext.Length);
            name = name.Trim();
            if (name.Length == 0) name = "audio";
            return name + ".mp3";
        }

        public static double FadeSeconds(int ms)
        {
            return Math.Max(0, ms) / 1000.0;
        }
    }

    public static class Lang
    {
        public static bool English;

        public static string T(string zh, string en)
        {
            return English ? en : zh;
        }
    }

    public static class AppSettings
    {
        public static string Dir()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OsuManiaMerger");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string FilePath()
        {
            return Path.Combine(Dir(), "settings.txt");
        }

        public static string LoadLanguage()
        {
            try
            {
                string f = FilePath();
                if (!File.Exists(f)) return "zh";
                foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                {
                    string s = line.Trim();
                    if (s.StartsWith("language=", StringComparison.OrdinalIgnoreCase))
                        return s.Substring(9).Trim().ToLowerInvariant();
                }
            }
            catch
            {
            }
            return "zh";
        }

        public static void SaveLanguage(string code)
        {
            try
            {
                File.WriteAllText(FilePath(), "language=" + code + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }

    public class TimingPoint
    {
        public double Time;
        public double BeatLength = 500;
        public int Meter = 4;
        public int SampleSet;
        public int SampleIndex;
        public int Volume = 100;
        public bool Uninherited = true;
        public int Effects;

        public static TimingPoint Parse(string line, string file, int lineNo)
        {
            string[] f = line.Split(',');
            if (f.Length < 2)
                throw new FormatException(string.Format(Lang.T("{0} 第 {1} 行：timing point 字段不足", "{0} line {1}: not enough timing point fields"), file, lineNo));
            TimingPoint t = new TimingPoint();
            t.Time = Util.ParseDouble(f[0], file, lineNo);
            t.BeatLength = Util.ParseDouble(f[1], file, lineNo);
            if (f.Length > 2) t.Meter = Util.ParseInt(f[2], file, lineNo);
            if (f.Length > 3) t.SampleSet = Util.ParseInt(f[3], file, lineNo);
            if (f.Length > 4) t.SampleIndex = Util.ParseInt(f[4], file, lineNo);
            if (f.Length > 5) t.Volume = Util.ParseInt(f[5], file, lineNo);
            if (f.Length > 6) t.Uninherited = f[6].Trim() != "0";
            if (f.Length > 7) t.Effects = Util.ParseInt(f[7], file, lineNo);
            return t;
        }

        public string ToLine()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7}",
                Util.Fmt(Time), Util.Fmt(BeatLength), Meter, SampleSet, SampleIndex, Volume,
                Uninherited ? 1 : 0, Effects);
        }

        public TimingPoint CloneShift(double delta)
        {
            TimingPoint t = new TimingPoint();
            t.Time = Time + delta;
            t.BeatLength = BeatLength;
            t.Meter = Meter;
            t.SampleSet = SampleSet;
            t.SampleIndex = SampleIndex;
            t.Volume = Volume;
            t.Uninherited = Uninherited;
            t.Effects = Effects;
            return t;
        }
    }

    public class HitObject
    {
        public int X;
        public int Y;
        public int Time;
        public int Type;
        public int HitSound;
        public bool IsHold;
        public int EndTime;
        public string HoldSample = "";
        public string Extra = "";
        public int Seq;

        public int EndOrTime
        {
            get { return IsHold ? EndTime : Time; }
        }

        public static HitObject Parse(string line, string file, int lineNo)
        {
            string[] f = line.Split(',');
            if (f.Length < 5)
                throw new FormatException(string.Format(Lang.T("{0} 第 {1} 行：hit object 字段不足", "{0} line {1}: not enough hit object fields"), file, lineNo));
            HitObject h = new HitObject();
            h.X = Util.ParseInt(f[0], file, lineNo);
            h.Y = Util.ParseInt(f[1], file, lineNo);
            h.Time = Util.ParseInt(f[2], file, lineNo);
            h.Type = Util.ParseInt(f[3], file, lineNo);
            h.HitSound = Util.ParseInt(f[4], file, lineNo);
            h.IsHold = (h.Type & 128) != 0;
            if (h.IsHold)
            {
                if (f.Length < 6)
                    throw new FormatException(string.Format(Lang.T("{0} 第 {1} 行：长条缺少结尾时间", "{0} line {1}: hold note is missing its end time"), file, lineNo));
                string p = f[5];
                int c = p.IndexOf(':');
                string endPart = c >= 0 ? p.Substring(0, c) : p;
                h.HoldSample = c >= 0 ? p.Substring(c + 1) : "";
                h.EndTime = Util.ParseInt(endPart, file, lineNo);
                if (h.EndTime < h.Time) h.EndTime = h.Time;
            }
            else if (f.Length > 5)
            {
                h.Extra = string.Join(",", f, 5, f.Length - 5);
            }
            return h;
        }

        public HitObject Clone()
        {
            HitObject h = new HitObject();
            h.X = X; h.Y = Y; h.Time = Time; h.Type = Type; h.HitSound = HitSound;
            h.IsHold = IsHold; h.EndTime = EndTime; h.HoldSample = HoldSample;
            h.Extra = Extra; h.Seq = Seq;
            return h;
        }

        public string ToLine(int delta)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(X).Append(',').Append(Y).Append(',').Append(Time + delta)
              .Append(',').Append(Type).Append(',').Append(HitSound);
            if (IsHold)
            {
                sb.Append(',').Append(EndTime + delta).Append(':').Append(HoldSample);
            }
            else if (Extra.Length > 0)
            {
                sb.Append(',').Append(Extra);
            }
            return sb.ToString();
        }
    }

    public class Beatmap
    {
        public string FilePath = "";
        public string Dir = "";
        public string AudioFilename = "";
        public int Keys = 4;
        public int FirstNote;
        public int LastEnd;
        public List<string> GeneralLines = new List<string>();
        public List<string> MetadataLines = new List<string>();
        public List<string> DifficultyLines = new List<string>();
        public List<TimingPoint> TimingPoints = new List<TimingPoint>();
        public List<HitObject> HitObjects = new List<HitObject>();

        public string DisplayName
        {
            get { return Path.GetFileName(FilePath); }
        }

        public string ResolveAudio()
        {
            if (string.IsNullOrEmpty(AudioFilename)) return null;
            string p = AudioFilename.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(p)) return p;
            return Path.Combine(Dir, p);
        }

        public static Beatmap Parse(string path)
        {
            Beatmap b = new Beatmap();
            b.FilePath = path;
            b.Dir = Path.GetDirectoryName(Path.GetFullPath(path));
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            string section = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("//")) continue;
                if (line.StartsWith("["))
                {
                    int end = line.IndexOf(']');
                    section = end > 1 ? line.Substring(1, end - 1) : "";
                    continue;
                }
                if (section == "General") b.GeneralLines.Add(line);
                else if (section == "Metadata") b.MetadataLines.Add(line);
                else if (section == "Difficulty") b.DifficultyLines.Add(line);
                else if (section == "TimingPoints") b.TimingPoints.Add(TimingPoint.Parse(line, path, i + 1));
                else if (section == "HitObjects") b.HitObjects.Add(HitObject.Parse(line, path, i + 1));
            }

            if (b.HitObjects.Count == 0)
                throw new FormatException(Lang.T("这张谱面里没有 note：", "This chart has no notes: ") + Path.GetFileName(path));

            string cs = Util.GetValue(b.DifficultyLines, "CircleSize");
            if (!string.IsNullOrEmpty(cs))
            {
                int k;
                if (int.TryParse(cs.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out k) && k > 0)
                    b.Keys = k;
            }

            string af = Util.GetValue(b.GeneralLines, "AudioFilename");
            b.AudioFilename = af == null ? "" : af.Trim();

            b.FirstNote = b.HitObjects.Min(delegate(HitObject h) { return h.Time; });
            b.LastEnd = b.HitObjects.Max(delegate(HitObject h) { return h.EndOrTime; });
            if (b.LastEnd < b.FirstNote) b.LastEnd = b.FirstNote;

            for (int i = 0; i < b.HitObjects.Count; i++) b.HitObjects[i].Seq = i;
            return b;
        }
    }

    public class ShiftedChart
    {
        public Beatmap Map;
        public int Delta;
        public int SegStart;          // 第一个 note 在合并后的时间
        public int NoteEndGlobal;     // 最后一个 note 结尾在合并后的时间
        public int AudioStartFile;    // 在原始音频文件里的起点
        public int AudioEndFile;      // 在原始音频文件里的终点
        public int AudioStartGlobal;  // 在合并音频里的起点
        public int AudioEndGlobal;    // 在合并音频里的终点
        public int FadeInMs;
        public int FadeOutMs;
    }

    public class AudioSegment
    {
        public string Path;
        public int StartMs;       // 原始音频文件内起点
        public int EndMs;         // 原始音频文件内终点
        public int PlaceStartMs;  // 合并音频内起点
        public int PlaceEndMs;    // 合并音频内终点
        public int FadeInMs;
        public int FadeOutMs;
        public string Label;
    }

    public class MergeOptions
    {
        public int GapMs = 10000;
        public int FadeMs = 5000;
        public string Title = "合谱";
        public string Artist = "";
        public string Creator = "osu!mania Merger";
        public string Difficulty = "Merged";
        public string AudioName = "audio.mp3";
        public string OutOsz = "";
        public string Ffmpeg = "";
        public double OverallDifficulty = 8.0;
        public double HPDrainRate = 8.0;
    }

    public class MergeResult
    {
        public List<ShiftedChart> Charts = new List<ShiftedChart>();
        public List<TimingPoint> Timing = new List<TimingPoint>();
        public List<HitObject> HitObjects = new List<HitObject>();
        public List<AudioSegment> Audio = new List<AudioSegment>();
        public int TotalMs;
        public double BaseBeatLength = 500;
    }

    public static class Ffmpeg
    {
        public static string Find()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] locals = new string[]
            {
                Path.Combine(baseDir, "ffmpeg.exe"),
                Path.Combine(baseDir, "tools", "ffmpeg.exe"),
                Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe")
            };
            foreach (string p in locals)
            {
                if (File.Exists(p)) return p;
            }
            string output;
            try
            {
                if (Run("ffmpeg", "-version", out output, 8000) == 0) return "ffmpeg";
            }
            catch
            {
            }
            return null;
        }

        public static string Q(string s)
        {
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        public static int Run(string exe, string args, out string output, int timeoutMs)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            StringBuilder sb = new StringBuilder();
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) { lock (sb) sb.AppendLine(e.Data); }
                };
                p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) { lock (sb) sb.AppendLine(e.Data); }
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException(Lang.T("ffmpeg 执行超时。", "ffmpeg timed out."));
                }
                p.WaitForExit();
                lock (sb) output = sb.ToString();
                return p.ExitCode;
            }
        }

        public static int ProbeDurationMs(string ffmpeg, string file, Action<string> log)
        {
            string output;
            Ffmpeg.Run(ffmpeg, "-nostdin -hide_banner -i " + Q(file), out output, 60000);
            Match m = Regex.Match(output == null ? "" : output,
                @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                if (log != null) log(Lang.T("  无法读取音频时长，按 5 秒结尾处理：", "  Cannot read audio duration; assuming a 5s tail: ") + Path.GetFileName(file));
                return -1;
            }
            int h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int mi = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double sec = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return (int)Math.Round(((h * 60 + mi) * 60 + sec) * 1000.0);
        }
    }

    public static class ZipWriter
    {
        private static uint[] crcTable;

        public static void Create(string zipPath, string[] entryNames, string[] filePaths)
        {
            if (entryNames.Length != filePaths.Length)
                throw new ArgumentException("entry names and files count mismatch");

            using (FileStream fs = File.Create(zipPath))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                int n = entryNames.Length;
                byte[][] names = new byte[n][];
                uint[] crcs = new uint[n];
                long[] sizes = new long[n];
                long[] offsets = new long[n];

                for (int i = 0; i < n; i++)
                {
                    uint crc;
                    long size;
                    ComputeCrc(filePaths[i], out crc, out size);
                    names[i] = Encoding.UTF8.GetBytes(entryNames[i]);
                    crcs[i] = crc;
                    sizes[i] = size;
                    offsets[i] = fs.Position;
                    WriteLocalHeader(w, names[i], crc, size);
                    using (FileStream src = File.OpenRead(filePaths[i]))
                    {
                        byte[] buf = new byte[81920];
                        int r;
                        while ((r = src.Read(buf, 0, buf.Length)) > 0) w.Write(buf, 0, r);
                    }
                }

                long cdStart = fs.Position;
                for (int i = 0; i < n; i++)
                    WriteCentralHeader(w, names[i], crcs[i], sizes[i], offsets[i]);
                long cdSize = fs.Position - cdStart;
                WriteEndRecord(w, n, cdStart, cdSize);
            }
        }

        private static void ComputeCrc(string path, out uint crc, out long size)
        {
            uint c = 0xFFFFFFFF;
            long total = 0;
            byte[] buf = new byte[81920];
            using (FileStream f = File.OpenRead(path))
            {
                int r;
                while ((r = f.Read(buf, 0, buf.Length)) > 0)
                {
                    c = UpdateCrc(c, buf, r);
                    total += r;
                }
            }
            crc = ~c;
            size = total;
        }

        private static uint UpdateCrc(uint crc, byte[] buf, int len)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint v = i;
                    for (int j = 0; j < 8; j++)
                        v = ((v & 1) != 0) ? (0xEDB88320u ^ (v >> 1)) : (v >> 1);
                    crcTable[i] = v;
                }
            }
            for (int i = 0; i < len; i++)
                crc = crcTable[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static void WriteLocalHeader(BinaryWriter w, byte[] name, uint crc, long size)
        {
            w.Write(0x04034b50);
            w.Write((ushort)20);
            w.Write((ushort)0x0800);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write(crc);
            w.Write((uint)size);
            w.Write((uint)size);
            w.Write((ushort)name.Length);
            w.Write((ushort)0);
            w.Write(name);
        }

        private static void WriteCentralHeader(BinaryWriter w, byte[] name, uint crc, long size, long offset)
        {
            w.Write(0x02014b50);
            w.Write((ushort)20);
            w.Write((ushort)20);
            w.Write((ushort)0x0800);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write(crc);
            w.Write((uint)size);
            w.Write((uint)size);
            w.Write((ushort)name.Length);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((uint)0);
            w.Write((uint)offset);
            w.Write(name);
        }

        private static void WriteEndRecord(BinaryWriter w, int count, long cdStart, long cdSize)
        {
            w.Write(0x06054b50);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)count);
            w.Write((ushort)count);
            w.Write((uint)cdSize);
            w.Write((uint)cdStart);
            w.Write((ushort)0);
        }
    }

    public static class Merger
    {
        public const int DefaultGapMs = 10000;
        public const int DefaultFadeMs = 5000;

        public static MergeResult Plan(List<Beatmap> maps, int gapMs, Func<Beatmap, int> durationProvider)
        {
            if (maps == null || maps.Count == 0)
                throw new InvalidOperationException(Lang.T("没有可合并的谱面。", "No charts to merge."));

            MergeResult r = new MergeResult();
            int prevNoteEnd = 0;
            for (int i = 0; i < maps.Count; i++)
            {
                Beatmap m = maps[i];
                ShiftedChart sc = new ShiftedChart();
                sc.Map = m;

                int firstNote = m.FirstNote;
                int lastNote = m.LastEnd;
                int noteStart = (i == 0) ? firstNote : prevNoteEnd + gapMs;
                int noteEnd = noteStart + (lastNote - firstNote);

                // 第一首保留完整前奏；后面的歌最多保留 5 秒前奏
                int introKept = (i == 0) ? firstNote : Math.Min(firstNote, DefaultFadeMs);
                int audioStartFile = firstNote - introKept;
                int audioStartGlobal = noteStart - introKept;

                int duration = (durationProvider == null) ? -1 : durationProvider(m);
                int outroKept;
                bool longOutro = false;
                if (duration > 0)
                {
                    int outro = Math.Max(0, duration - lastNote);
                    outroKept = Math.Min(outro, DefaultFadeMs);
                    longOutro = outro > DefaultFadeMs;
                }
                else
                {
                    // 读不到时长时，按“结尾还有 5 秒”处理，不作渐弱
                    outroKept = DefaultFadeMs;
                }

                // 即使音频本身比最后一个 note 短，也要把静音补到该 note，
                // 否则会在两段之间留下远超 10 秒的空白
                int audioEndFile = lastNote + outroKept;

                sc.Delta = noteStart - firstNote;
                sc.SegStart = noteStart;
                sc.NoteEndGlobal = noteEnd;
                sc.AudioStartFile = audioStartFile;
                sc.AudioEndFile = audioEndFile;
                sc.AudioStartGlobal = audioStartGlobal;
                sc.AudioEndGlobal = audioStartGlobal + (audioEndFile - audioStartFile);
                sc.FadeInMs = (introKept < firstNote) ? Math.Min(DefaultFadeMs, introKept) : 0;
                sc.FadeOutMs = longOutro ? Math.Min(DefaultFadeMs, audioEndFile - audioStartFile) : 0;
                prevNoteEnd = noteEnd;
                r.Charts.Add(sc);

                AudioSegment seg = new AudioSegment();
                seg.Path = m.ResolveAudio();
                seg.StartMs = sc.AudioStartFile;
                seg.EndMs = sc.AudioEndFile;
                seg.PlaceStartMs = sc.AudioStartGlobal;
                seg.PlaceEndMs = sc.AudioEndGlobal;
                seg.FadeInMs = sc.FadeInMs;
                seg.FadeOutMs = sc.FadeOutMs;
                seg.Label = m.DisplayName;
                r.Audio.Add(seg);
            }
            r.TotalMs = r.Charts[r.Charts.Count - 1].AudioEndGlobal;
            BuildTiming(r);
            BuildHits(r);
            return r;
        }

        private static TimingPoint ActiveRed(List<TimingPoint> pts, double at)
        {
            TimingPoint best = null;
            foreach (TimingPoint p in pts)
            {
                if (!p.Uninherited) continue;
                if (p.Time <= at)
                {
                    if (best == null || p.Time >= best.Time) best = p;
                }
            }
            if (best != null) return best;
            foreach (TimingPoint p in pts)
            {
                if (!p.Uninherited) continue;
                if (best == null || p.Time < best.Time) best = p;
            }
            return best;
        }

        private static void BuildTiming(MergeResult r)
        {
            List<List<TimingPoint>> perChart = new List<List<TimingPoint>>();
            List<TimingPoint> allReds = new List<TimingPoint>();

            foreach (ShiftedChart sc in r.Charts)
            {
                Beatmap m = sc.Map;
                TimingPoint active = ActiveRed(m.TimingPoints, sc.AudioStartFile);

                TimingPoint first = new TimingPoint();
                first.Time = sc.AudioStartGlobal;
                first.Uninherited = true;
                if (active != null)
                {
                    first.BeatLength = active.BeatLength;
                    first.Meter = active.Meter;
                    first.SampleSet = active.SampleSet;
                    first.SampleIndex = active.SampleIndex;
                    first.Volume = active.Volume;
                    first.Effects = active.Effects;
                }
                else
                {
                    first.BeatLength = 500;
                    first.Meter = 4;
                    first.Volume = 100;
                }

                List<TimingPoint> reds = new List<TimingPoint>();
                reds.Add(first);
                foreach (TimingPoint p in m.TimingPoints)
                {
                    if (!p.Uninherited) continue;
                    if (p.Time > sc.AudioStartFile && p.Time <= sc.AudioEndFile)
                        reds.Add(p.CloneShift(sc.Delta));
                }
                reds.Sort(delegate(TimingPoint a, TimingPoint b) { return a.Time.CompareTo(b.Time); });
                perChart.Add(reds);
                allReds.AddRange(reds);
            }

            // 全局基准：在整首拼接后的音频里，累计持续时间最长的 BPM
            Dictionary<int, double> totals = new Dictionary<int, double>();
            for (int ci = 0; ci < r.Charts.Count; ci++)
            {
                ShiftedChart sc = r.Charts[ci];
                List<TimingPoint> reds = perChart[ci];
                for (int i = 0; i < reds.Count; i++)
                {
                    double start = Math.Max(reds[i].Time, sc.AudioStartGlobal);
                    double end = (i + 1 < reds.Count) ? reds[i + 1].Time : sc.AudioEndGlobal;
                    if (end > sc.AudioEndGlobal) end = sc.AudioEndGlobal;
                    double d = end - start;
                    if (d <= 0) continue;
                    int key = (int)Math.Round(reds[i].BeatLength * 1000.0);
                    double old;
                    if (totals.TryGetValue(key, out old)) totals[key] = old + d;
                    else totals[key] = d;
                }
            }

            int bestKey = 0;
            double bestDur = -1;
            foreach (KeyValuePair<int, double> kv in totals)
            {
                if (kv.Value > bestDur)
                {
                    bestDur = kv.Value;
                    bestKey = kv.Key;
                }
            }
            double baseBeat = bestKey / 1000.0;
            if (baseBeat <= 0) baseBeat = 500;
            r.BaseBeatLength = baseBeat;

            List<TimingPoint> all = new List<TimingPoint>();
            foreach (TimingPoint red in allReds)
            {
                all.Add(red);
                TimingPoint g = new TimingPoint();
                g.Time = red.Time;
                g.Uninherited = false;
                double cur = red.BeatLength > 0 ? red.BeatLength : baseBeat;
                // SV = 全局基准BPM / 当前BPM = 当前beatLength / 基准beatLength
                g.BeatLength = Math.Round(-100.0 * baseBeat / cur, 3);
                g.Meter = 4;
                g.SampleSet = red.SampleSet;
                g.SampleIndex = red.SampleIndex;
                g.Volume = red.Volume;
                g.Effects = red.Effects;
                all.Add(g);
            }

            all.Sort(delegate(TimingPoint a, TimingPoint b)
            {
                int c = a.Time.CompareTo(b.Time);
                if (c != 0) return c;
                return b.Uninherited.CompareTo(a.Uninherited);
            });
            r.Timing = all;
        }

        private static void BuildHits(MergeResult r)
        {
            List<HitObject> all = new List<HitObject>();
            int seq = 0;
            foreach (ShiftedChart sc in r.Charts)
            {
                foreach (HitObject h in sc.Map.HitObjects)
                {
                    HitObject c = h.Clone();
                    c.Time += sc.Delta;
                    if (c.IsHold) c.EndTime += sc.Delta;
                    c.Seq = seq++;
                    all.Add(c);
                }
            }
            all.Sort(delegate(HitObject a, HitObject b)
            {
                int c = a.Time.CompareTo(b.Time);
                if (c != 0) return c;
                return a.Seq.CompareTo(b.Seq);
            });
            r.HitObjects = all;
        }

        public static string BuildOsu(List<Beatmap> maps, MergeResult r, string audioFilename, MergeOptions opt)
        {
            Beatmap first = maps[0];
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("osu file format v14");
            sb.AppendLine();

            List<string> general = new List<string>(first.GeneralLines);
            general.RemoveAll(delegate(string l)
            {
                string k = Util.KeyOf(l);
                return string.Equals(k, "AudioFilename", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "AudioLeadIn", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "PreviewTime", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "Mode", StringComparison.OrdinalIgnoreCase);
            });
            general.Add("AudioFilename: " + (audioFilename == null ? "" : audioFilename));
            general.Add("AudioLeadIn: 0");
            general.Add("PreviewTime: -1");
            general.Add("Mode: 3");

            sb.AppendLine("[General]");
            foreach (string l in general) sb.AppendLine(l);
            sb.AppendLine();

            List<string> meta = new List<string>(first.MetadataLines);
            meta.RemoveAll(delegate(string l)
            {
                string k = Util.KeyOf(l);
                return string.Equals(k, "Title", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "TitleUnicode", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "Artist", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "ArtistUnicode", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "Creator", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "Version", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "BeatmapID", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "BeatmapSetID", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(k, "Tags", StringComparison.OrdinalIgnoreCase);
            });
            string oldTags = Util.GetValue(first.MetadataLines, "Tags");
            string tags;
            if (string.IsNullOrEmpty(oldTags)) tags = "merge";
            else if ((" " + oldTags + " ").IndexOf(" merge ", StringComparison.OrdinalIgnoreCase) >= 0) tags = oldTags;
            else tags = oldTags + " merge";
            string title = string.IsNullOrEmpty(opt.Title) ? Lang.T("合谱", "Merged") : opt.Title;
            string artist = string.IsNullOrEmpty(opt.Artist) ? "Unknown" : opt.Artist;
            string creator = string.IsNullOrEmpty(opt.Creator) ? "osu!mania Merger" : opt.Creator;
            string difficulty = string.IsNullOrEmpty(opt.Difficulty) ? "Merged" : opt.Difficulty;
            meta.Add("Title: " + title);
            meta.Add("TitleUnicode: " + title);
            meta.Add("Artist: " + artist);
            meta.Add("ArtistUnicode: " + artist);
            meta.Add("Creator: " + creator);
            meta.Add("Version: " + difficulty);
            meta.Add("Tags: " + tags);
            meta.Add("BeatmapID: 0");
            meta.Add("BeatmapSetID: -1");

            sb.AppendLine("[Metadata]");
            foreach (string l in meta) sb.AppendLine(l);
            sb.AppendLine();

            List<string> diff = new List<string>(first.DifficultyLines);
            Util.SetValue(diff, "CircleSize", first.Keys.ToString(CultureInfo.InvariantCulture));
            Util.SetValue(diff, "OverallDifficulty", Util.Fmt(opt.OverallDifficulty));
            Util.SetValue(diff, "HPDrainRate", Util.Fmt(opt.HPDrainRate));
            sb.AppendLine("[Difficulty]");
            foreach (string l in diff) sb.AppendLine(l);
            sb.AppendLine();

            sb.AppendLine("[Events]");
            sb.AppendLine("//Background and Video events");
            sb.AppendLine("//Break Periods");
            sb.AppendLine("//Storyboard Layer 0 (Background)");
            sb.AppendLine("//Storyboard Layer 1 (Fail)");
            sb.AppendLine("//Storyboard Layer 2 (Pass)");
            sb.AppendLine("//Storyboard Layer 3 (Foreground)");
            sb.AppendLine("//Storyboard Layer 4 (Overlay)");
            sb.AppendLine("//Storyboard Sound Samples");
            sb.AppendLine();

            sb.AppendLine("[TimingPoints]");
            foreach (TimingPoint t in r.Timing) sb.AppendLine(t.ToLine());
            sb.AppendLine();

            sb.AppendLine("[HitObjects]");
            foreach (HitObject h in r.HitObjects) sb.AppendLine(h.ToLine(0));
            sb.AppendLine();

            return sb.ToString();
        }

        public static void BuildAudio(List<AudioSegment> segs, string outFile, string ffmpeg, Action<string> log)
        {
            string temp = Path.Combine(Path.GetTempPath(), "osu_merge_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                List<string> parts = new List<string>();
                int cursor = 0;
                for (int i = 0; i < segs.Count; i++)
                {
                    AudioSegment seg = segs[i];
                    if (string.IsNullOrEmpty(seg.Path) || !File.Exists(seg.Path))
                        throw new FileNotFoundException(Lang.T("找不到音频文件：", "Audio file not found: ") + (seg.Path == null ? Lang.T("(未填写 AudioFilename)", "(AudioFilename is empty)") : seg.Path));

                    int gap = seg.PlaceStartMs - cursor;
                    if (gap > 0)
                    {
                        string sil = Path.Combine(temp, "gap" + i + ".wav");
                        string sargs = "-y -nostdin -hide_banner -loglevel error -f lavfi -i anullsrc=r=44100:cl=stereo -t "
                            + Util.FmtSec(gap) + " -c:a pcm_s16le " + Ffmpeg.Q(sil);
                        RunFfmpeg(ffmpeg, sargs, log);
                        parts.Add(sil);
                        log(Lang.T("  间隔 ", "  Gap ") + Util.FmtSec(gap) + "s");
                    }
                    else if (gap < 0)
                    {
                        log(Lang.T("  警告：音频与前一段重叠 ", "  Warning: audio overlaps the previous segment by ") + Util.FmtSec(-gap) + Lang.T("s，已按紧接处理", "s; treating as adjacent"));
                    }

                    int dur = seg.EndMs - seg.StartMs;
                    if (dur <= 0)
                        throw new InvalidOperationException(Lang.T("谱面音频长度无效：", "Invalid audio length: ") + seg.Label);

                    string wav = Path.Combine(temp, "seg" + i + ".wav");
                    string filter = "apad,asetpts=PTS-STARTPTS";
                    if (seg.FadeInMs > 0)
                        filter += ",afade=t=in:st=0:d=" + Util.FmtSec(Math.Min(seg.FadeInMs, dur)) + ":curve=qua";
                    if (seg.FadeOutMs > 0)
                    {
                        int fo = Math.Min(seg.FadeOutMs, dur);
                        double st = Math.Max(0, (dur - fo) / 1000.0);
                        filter += ",afade=t=out:st=" + st.ToString("0.###", CultureInfo.InvariantCulture)
                            + ":d=" + Util.FmtSec(fo) + ":curve=qua";
                    }

                    log(string.Format(Lang.T("音频 {0}/{1}：{2}  保留 {3}s ~ {4}s（{5}s）{6}{7}", "Audio {0}/{1}: {2}  keep {3}s ~ {4}s ({5}s){6}{7}"),
                        i + 1, segs.Count, seg.Label,
                        Util.FmtSec(seg.StartMs), Util.FmtSec(seg.EndMs), Util.FmtSec(dur),
                        seg.FadeInMs > 0 ? Lang.T("，渐入", ", fade in") : "", seg.FadeOutMs > 0 ? Lang.T("，渐出", ", fade out") : ""));

                    string args = "-y -nostdin -hide_banner -loglevel error -ss " + Util.FmtSec(seg.StartMs)
                        + " -i " + Ffmpeg.Q(seg.Path) + " -t " + Util.FmtSec(dur)
                        + " -vn -af " + Ffmpeg.Q(filter) + " -ac 2 -ar 44100 -c:a pcm_s16le " + Ffmpeg.Q(wav);
                    RunFfmpeg(ffmpeg, args, log);
                    parts.Add(wav);
                    cursor = seg.PlaceEndMs;
                }

                string list = Path.Combine(temp, "list.txt");
                StringBuilder lb = new StringBuilder();
                foreach (string p in parts)
                {
                    string q = p.Replace("\\", "/").Replace("'", "'\\''");
                    lb.AppendLine("file '" + q + "'");
                }
                File.WriteAllText(list, lb.ToString(), new UTF8Encoding(false));

                string cargs = "-y -nostdin -hide_banner -loglevel error -f concat -safe 0 -i " + Ffmpeg.Q(list)
                    + " -map_metadata -1 -c:a libmp3lame -b:a 192k -ar 44100 -id3v2_version 3 " + Ffmpeg.Q(outFile);
                log(Lang.T("拼接音频并编码为 MP3 ...", "Concatenating audio and encoding MP3 ..."));
                RunFfmpeg(ffmpeg, cargs, log);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { }
            }
        }

        private static void RunFfmpeg(string ffmpeg, string args, Action<string> log)
        {
            string output;
            int code = Ffmpeg.Run(ffmpeg, args, out output, 15 * 60 * 1000);
            if (code != 0)
            {
                string tail = output == null ? "" : output.Trim();
                if (tail.Length > 1200) tail = tail.Substring(tail.Length - 1200);
                throw new InvalidOperationException(Lang.T("ffmpeg 执行失败（退出码 ", "ffmpeg failed (exit code ") + code + Lang.T("）：\n", "):\n") + tail);
            }
        }

        public static void MergeToFile(List<Beatmap> maps, MergeOptions opt, Action<string> log)
        {
            if (string.IsNullOrEmpty(opt.Ffmpeg))
                throw new InvalidOperationException(Lang.T("没有可用的 ffmpeg，无法拼接音频。", "ffmpeg is not available; cannot build audio."));

            log(string.Format(Lang.T("共 {0} 张谱面，间隔 {1} 秒，开始合并。", "Merging {0} charts with a {1}s gap."), maps.Count, opt.GapMs / 1000.0));

            Dictionary<string, int> cache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Func<Beatmap, int> probe = delegate(Beatmap m)
            {
                string p = m.ResolveAudio();
                if (string.IsNullOrEmpty(p) || !File.Exists(p))
                    throw new FileNotFoundException(Lang.T("找不到音频文件：", "Audio file not found: ") + (p == null ? Lang.T("(未填写 AudioFilename)", "(AudioFilename is empty)") : p));
                if (!cache.ContainsKey(p))
                    cache[p] = Ffmpeg.ProbeDurationMs(opt.Ffmpeg, p, log);
                return cache[p];
            };

            MergeResult r = Plan(maps, opt.GapMs, probe);
            log(Lang.T("全局基准 BPM：", "Global base BPM: ") + Util.Fmt(60000.0 / r.BaseBeatLength) + Lang.T("（整首合并音频里累计持续时间最长的 BPM）", " (BPM with the longest total time in the merged audio)"));

            for (int i = 0; i < r.Charts.Count; i++)
            {
                ShiftedChart sc = r.Charts[i];
                log(string.Format(Lang.T("  {0}. {1}：{2}K，{3} 个 note，note {4} → {5}", "  {0}. {1}: {2}K, {3} notes, {4} → {5}"),
                    i + 1, sc.Map.DisplayName, sc.Map.Keys, sc.Map.HitObjects.Count,
                    Util.FormatTime(sc.SegStart), Util.FormatTime(sc.NoteEndGlobal)));
                if (sc.AudioEndGlobal < sc.NoteEndGlobal)
                    log(Lang.T("     注意：这张谱的音频比最后一个 note 短，尾部会是静音。", "     Note: this chart audio is shorter than its last note; the tail will be silent."));
            }

            string staging = Path.Combine(Path.GetTempPath(), "osu_osz_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                string audioName = Util.EnsureMp3(opt.AudioName);
                string audioPath = Path.Combine(staging, audioName);
                BuildAudio(r.Audio, audioPath, opt.Ffmpeg, log);

                string title = string.IsNullOrEmpty(opt.Title) ? Lang.T("合谱", "Merged") : opt.Title.Trim();
                string artist = string.IsNullOrEmpty(opt.Artist) ? "Unknown" : opt.Artist.Trim();
                string creator = string.IsNullOrEmpty(opt.Creator) ? "osu!mania Merger" : opt.Creator.Trim();
                string difficulty = string.IsNullOrEmpty(opt.Difficulty) ? "Merged" : opt.Difficulty.Trim();

                string osuEntry = Util.SafeFileName(
                    artist + " - " + title + " (" + creator + ") [" + difficulty + "]", "merged") + ".osu";
                string osuPath = Path.Combine(staging, osuEntry);
                string text = BuildOsu(maps, r, audioName, opt);
                File.WriteAllText(osuPath, text, new UTF8Encoding(true));

                WriteOsz(osuPath, osuEntry, audioPath, audioName, opt.OutOsz, log);
                log(string.Format(Lang.T("谱面已打包：{0}", "Packed: {0}"), opt.OutOsz));
                log(string.Format(Lang.T("总时长：{0}（最后一个 note 结束于 {1}）", "Total: {0} (last note ends at {1})"),
                    Util.FormatTime(r.TotalMs), Util.FormatTime(r.Charts[r.Charts.Count - 1].NoteEndGlobal)));
            }
            finally
            {
                try { Directory.Delete(staging, true); } catch { }
            }
        }

        public static void WriteOsz(string osuPath, string osuEntry, string audioPath, string audioEntry,
            string outOsz, Action<string> log)
        {
            if (File.Exists(outOsz)) File.Delete(outOsz);
            ZipWriter.Create(outOsz,
                new string[] { osuEntry, audioEntry },
                new string[] { osuPath, audioPath });
        }
    }

    public class MainForm : Form
    {
        private List<Beatmap> maps = new List<Beatmap>();
        private ListView lv;
        private NumericUpDown numGap;
        private NumericUpDown numOD;
        private NumericUpDown numHP;
        private Label lblFfmpeg;
        private ComboBox cboPreset;
        private ComboBox cboLang;
        private Button btnSavePreset;
        private TextBox txtOutDir;
        private TextBox txtName;
        private TextBox txtTitle;
        private TextBox txtArtist;
        private TextBox txtCreator;
        private TextBox txtDifficulty;
        private TextBox txtAudioName;
        private Button btnMerge;
        private ProgressBar progress;
        private TextBox txtLog;
        private BackgroundWorker worker;
        private string ffmpegPath;
        private bool metadataPrefilled;
        private bool statsPrefilled;
        private bool applyingPreset;
        private bool changingLanguage;
        private bool nameTouched;
        private bool updatingName;
        private List<MetadataPreset> presets = new List<MetadataPreset>();
        private ToolTip tips;

        public MainForm()
        {
            Text = Lang.T("osu!mania 合谱工具", "osu!mania Merger") + "  ·  v1.7  ·  [Crz]Cardiloid";
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(980, 800);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 780);
            BackColor = Color.FromArgb(245, 246, 249);
            AllowDrop = true;

            BuildUi();
            SetDefaultOutputDir();
            LoadPresets();
            InitPresets();
            UpdateDefaultName();
            RefreshFfmpeg();

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += WorkerDoWork;
            worker.ProgressChanged += WorkerProgress;
            worker.RunWorkerCompleted += WorkerCompleted;
        }

        private void BuildUi()
        {
            tips = new ToolTip();
            tips.AutoPopDelay = 12000;

            GroupBox g1 = MakeGroup(Lang.T("1. 选择谱面（按拼接顺序排列，可直接拖入 .osu）",
                "1. Charts (in merge order; drag .osu files here)"), 12, 8, 956, 250);
            lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = false;
            lv.HideSelection = false;
            lv.BorderStyle = BorderStyle.FixedSingle;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.SetBounds(12, 22, 932, 170);
            lv.Columns.Add(Lang.T("顺序", "#"), 50);
            lv.Columns.Add(Lang.T("文件", "File"), 240);
            lv.Columns.Add(Lang.T("键数", "Keys"), 50);
            lv.Columns.Add(Lang.T("Note", "Notes"), 55);
            lv.Columns.Add(Lang.T("开始", "Start"), 90);
            lv.Columns.Add(Lang.T("结束", "End"), 90);
            lv.Columns.Add(Lang.T("音频", "Audio"), 330);
            lv.AllowDrop = true;
            lv.DragEnter += DragEnterFiles;
            lv.DragDrop += DragDropFiles;
            g1.Controls.Add(lv);

            Button bAdd = MakeButton(Lang.T("添加谱面", "Add charts"), 12, 196, 90);
            bAdd.Click += delegate { AddByDialog(); };
            g1.Controls.Add(bAdd);

            Button bRemove = MakeButton(Lang.T("移除选中", "Remove"), 110, 196, 90);
            bRemove.Click += delegate { RemoveSelected(); };
            g1.Controls.Add(bRemove);

            Button bClear = MakeButton(Lang.T("清空", "Clear"), 208, 196, 70);
            bClear.Click += delegate { maps.Clear(); metadataPrefilled = false; statsPrefilled = false; RefreshList(); };
            g1.Controls.Add(bClear);

            Button bUp = MakeButton(Lang.T("上移", "Up"), 290, 196, 70);
            bUp.Click += delegate { MoveSelected(-1); };
            g1.Controls.Add(bUp);

            Button bDown = MakeButton(Lang.T("下移", "Down"), 368, 196, 70);
            bDown.Click += delegate { MoveSelected(1); };
            g1.Controls.Add(bDown);

            GroupBox g2 = MakeGroup(Lang.T("2. 拼接", "2. Merge"), 12, 266, 956, 96);

            Label l1 = new Label();
            l1.Text = Lang.T("上一张最后一个 note 到下一张第一个 note 的间隔：",
                "Gap from last note to next chart first note:");
            l1.SetBounds(14, 26, 380, 24);
            l1.ForeColor = Color.FromArgb(60, 64, 72);
            g2.Controls.Add(l1);

            numGap = new NumericUpDown();
            numGap.DecimalPlaces = 2;
            numGap.Minimum = 0;
            numGap.Maximum = 600;
            numGap.Increment = 0.5M;
            numGap.Value = 10;
            numGap.BorderStyle = BorderStyle.FixedSingle;
            numGap.SetBounds(400, 24, 80, 25);
            g2.Controls.Add(numGap);
            tips.SetToolTip(numGap, Lang.T("上一张最后一个 note（LN 按面尾）到下一张第一个 note 的间隔。",
                "Time from the last note (LN tail) of a chart to the first note of the next."));

            Label l2 = new Label();
            l2.Text = Lang.T("秒（LN 按面尾计算）", "sec (LN tail counts)");
            l2.SetBounds(486, 26, 170, 24);
            l2.ForeColor = Color.FromArgb(110, 116, 126);
            g2.Controls.Add(l2);

            Label ll = new Label();
            ll.Text = Lang.T("语言", "Language");
            ll.SetBounds(690, 26, 60, 22);
            ll.ForeColor = Color.FromArgb(110, 116, 126);
            g2.Controls.Add(ll);

            cboLang = new ComboBox();
            cboLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLang.FlatStyle = FlatStyle.Flat;
            cboLang.BackColor = Color.White;
            cboLang.SetBounds(752, 24, 110, 25);
            cboLang.Items.Add("中文");
            cboLang.Items.Add("English");
            changingLanguage = true;
            cboLang.SelectedIndex = Lang.English ? 1 : 0;
            changingLanguage = false;
            cboLang.SelectedIndexChanged += delegate { SwitchLanguage(); };
            g2.Controls.Add(cboLang);

            lblFfmpeg = new Label();
            lblFfmpeg.SetBounds(14, 64, 700, 22);
            lblFfmpeg.AutoEllipsis = true;
            g2.Controls.Add(lblFfmpeg);
            tips.SetToolTip(lblFfmpeg, Lang.T("用于拼接音频并生成 .osz。", "Used to merge audio and build the .osz."));

            Button bFf = MakeButton(Lang.T("选择 ffmpeg...", "Select ffmpeg..."), 720, 60, 120);
            bFf.Click += delegate { PickFfmpeg(); };
            g2.Controls.Add(bFf);

            GroupBox g3 = MakeGroup(Lang.T("3. 谱面信息", "3. Beatmap info"), 12, 370, 956, 180);

            Label lp = new Label();
            lp.Text = Lang.T("预设", "Preset");
            lp.SetBounds(14, 26, 60, 22);
            lp.ForeColor = Color.FromArgb(60, 64, 72);
            g3.Controls.Add(lp);

            cboPreset = new ComboBox();
            cboPreset.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPreset.FlatStyle = FlatStyle.Flat;
            cboPreset.BackColor = Color.White;
            cboPreset.SetBounds(70, 24, 230, 25);
            cboPreset.SelectedIndexChanged += delegate { ApplyPreset(cboPreset.SelectedIndex); };
            g3.Controls.Add(cboPreset);
            tips.SetToolTip(cboPreset, Lang.T("选择一个预设，快速填入下面的谱面信息。",
                "Pick a preset to fill the beatmap info below."));

            btnSavePreset = MakeButton(Lang.T("保存", "Save"), 310, 22, 70);
            btnSavePreset.Click += delegate { SaveCurrentAsPreset(); };
            g3.Controls.Add(btnSavePreset);

            AddField(g3, "Title", 400, 26, 450, 480, out txtTitle);
            AddField(g3, "Artist", 14, 62, 70, 390, out txtArtist);
            AddField(g3, "Difficulty", 470, 62, 550, 380, out txtDifficulty);
            AddField(g3, "Creator", 14, 98, 80, 240, out txtCreator);
            AddField(g3, Lang.T("音频文件名", "Audio filename"), 330, 98, 440, 210, out txtAudioName);
            tips.SetToolTip(txtAudioName, Lang.T("写入 .osz 的 mp3 文件名，不需要写 .mp3。",
                "MP3 file name inside the .osz; do not include .mp3."));

            Label l3 = new Label();
            l3.Text = ".mp3";
            l3.SetBounds(655, 100, 40, 22);
            l3.ForeColor = Color.FromArgb(110, 116, 126);
            g3.Controls.Add(l3);

            Label lod = new Label();
            lod.Text = "OD";
            lod.SetBounds(696, 100, 30, 22);
            lod.ForeColor = Color.FromArgb(60, 64, 72);
            g3.Controls.Add(lod);

            numOD = new NumericUpDown();
            numOD.DecimalPlaces = 1;
            numOD.Minimum = 0;
            numOD.Maximum = 10;
            numOD.Increment = 0.1M;
            numOD.Value = 8M;
            numOD.BorderStyle = BorderStyle.FixedSingle;
            numOD.SetBounds(730, 96, 55, 25);
            g3.Controls.Add(numOD);
            tips.SetToolTip(numOD, Lang.T("Overall Difficulty，写入 .osu。", "Overall Difficulty written to the .osu."));

            Label lhp = new Label();
            lhp.Text = "HP";
            lhp.SetBounds(795, 100, 30, 22);
            lhp.ForeColor = Color.FromArgb(60, 64, 72);
            g3.Controls.Add(lhp);

            numHP = new NumericUpDown();
            numHP.DecimalPlaces = 1;
            numHP.Minimum = 0;
            numHP.Maximum = 10;
            numHP.Increment = 0.1M;
            numHP.Value = 8M;
            numHP.BorderStyle = BorderStyle.FixedSingle;
            numHP.SetBounds(830, 96, 55, 25);
            g3.Controls.Add(numHP);
            tips.SetToolTip(numHP, Lang.T("HP Drain Rate，写入 .osu。", "HP Drain Rate written to the .osu."));

            GroupBox g4 = MakeGroup(Lang.T("4. 输出与执行", "4. Output"), 12, 558, 956, 232);

            Label l4 = new Label();
            l4.Text = Lang.T("输出文件夹：", "Output folder:");
            l4.SetBounds(14, 26, 90, 24);
            l4.ForeColor = Color.FromArgb(60, 64, 72);
            g4.Controls.Add(l4);

            txtOutDir = new TextBox();
            txtOutDir.BorderStyle = BorderStyle.FixedSingle;
            txtOutDir.SetBounds(110, 24, 600, 25);
            g4.Controls.Add(txtOutDir);

            Button bOut = MakeButton(Lang.T("浏览...", "Browse..."), 720, 22, 90);
            bOut.Click += delegate { PickOutputDir(); };
            g4.Controls.Add(bOut);

            Label l5 = new Label();
            l5.Text = Lang.T("输出文件名：", "Output name:");
            l5.SetBounds(14, 62, 90, 24);
            l5.ForeColor = Color.FromArgb(60, 64, 72);
            g4.Controls.Add(l5);

            txtName = new TextBox();
            txtName.Text = Lang.T("合谱", "Merged");
            txtName.BorderStyle = BorderStyle.FixedSingle;
            txtName.SetBounds(110, 60, 260, 25);
            g4.Controls.Add(txtName);
            txtName.TextChanged += delegate { if (!updatingName) nameTouched = true; };
            txtTitle.TextChanged += delegate { if (!nameTouched) UpdateDefaultName(); };
            txtArtist.TextChanged += delegate { if (!nameTouched) UpdateDefaultName(); };

            Label l6 = new Label();
            l6.Text = ".osz";
            l6.SetBounds(376, 62, 60, 22);
            l6.ForeColor = Color.FromArgb(110, 116, 126);
            g4.Controls.Add(l6);

            btnMerge = new Button();
            btnMerge.Text = Lang.T("开始合并", "Start merge");
            btnMerge.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            btnMerge.FlatStyle = FlatStyle.Flat;
            btnMerge.FlatAppearance.BorderSize = 0;
            btnMerge.BackColor = Color.FromArgb(43, 108, 224);
            btnMerge.ForeColor = Color.White;
            btnMerge.Cursor = Cursors.Hand;
            btnMerge.SetBounds(450, 54, 180, 44);
            btnMerge.Click += delegate { StartMerge(); };
            g4.Controls.Add(btnMerge);

            progress = new ProgressBar();
            progress.SetBounds(640, 65, 300, 26);
            progress.Style = ProgressBarStyle.Blocks;
            g4.Controls.Add(progress);

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BorderStyle = BorderStyle.FixedSingle;
            txtLog.BackColor = Color.White;
            txtLog.Font = new Font("Consolas", 9F);
            txtLog.SetBounds(14, 108, 928, 110);
            g4.Controls.Add(txtLog);

            DragEnter += DragEnterFiles;
            DragDrop += DragDropFiles;
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (worker != null && worker.IsBusy)
                {
                    e.Cancel = true;
                    MessageBox.Show(this, Lang.T("正在合并，请等待完成后再关闭。", "Merging, please wait before closing."),
                        Lang.T("请稍候", "Please wait"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
        }

        private GroupBox MakeGroup(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.SetBounds(x, y, w, h);
            g.BackColor = Color.White;
            g.ForeColor = Color.FromArgb(45, 50, 58);
            Controls.Add(g);
            return g;
        }

        private void AddField(Control parent, string label, int lx, int y, int bx, int bw, out TextBox box)
        {
            Label l = new Label();
            l.Text = label;
            l.SetBounds(lx, y + 2, bx - lx - 6, 22);
            l.ForeColor = Color.FromArgb(60, 64, 72);
            parent.Controls.Add(l);

            box = new TextBox();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.SetBounds(bx, y, bw, 25);
            parent.Controls.Add(box);
        }

        private Button MakeButton(string text, int x, int y, int w)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, 30);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(208, 212, 220);
            b.BackColor = Color.White;
            b.ForeColor = Color.FromArgb(40, 44, 52);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void SwitchLanguage()
        {
            if (changingLanguage) return;
            string code = cboLang.SelectedIndex == 1 ? "en" : "zh";
            if ((code == "en") == Lang.English) return;
            AppSettings.SaveLanguage(code);
            string msg = code == "en" ? "Language changed. The app will restart." : "语言已切换，程序将重启。";
            string title = code == "en" ? "Language" : "语言";
            MessageBox.Show(this, msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            try
            {
                Application.Restart();
            }
            catch
            {
                try { Process.Start(Application.ExecutablePath); } catch { }
                Application.Exit();
            }
        }

        private void DragEnterFiles(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            else e.Effect = DragDropEffects.None;
        }

        private void DragDropFiles(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) AddFiles(files);
        }

        private void AddByDialog()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = Lang.T("osu! 谱面 (*.osu)|*.osu|所有文件 (*.*)|*.*", "osu! charts (*.osu)|*.osu|All files (*.*)|*.*");
                d.Multiselect = true;
                d.Title = Lang.T("选择要拼接的 .osu 谱面（可多选）", "Select .osu charts to merge (multi-select)");
                if (d.ShowDialog(this) == DialogResult.OK) AddFiles(d.FileNames);
            }
        }

        private void AddFiles(string[] files)
        {
            int added = 0;
            foreach (string f in files)
            {
                if (!string.Equals(Path.GetExtension(f), ".osu", StringComparison.OrdinalIgnoreCase)) continue;
                bool exists = false;
                foreach (Beatmap m in maps)
                {
                    if (string.Equals(m.FilePath, Path.GetFullPath(f), StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                }
                if (exists) continue;
                try
                {
                    Beatmap b = Beatmap.Parse(f);
                    maps.Add(b);
                    added++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, Lang.T("无法读取\n", "Cannot read\n") + f + "\n\n" + ex.Message,
                        Lang.T("解析失败", "Parse failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            if (added > 0)
            {
                if (txtOutDir.Text.Length == 0 && maps.Count > 0) txtOutDir.Text = maps[0].Dir;
                if (!metadataPrefilled && IsDefaultMetadata())
                {
                    RefreshPresetCombo("@first");
                    ApplyPreset(0);
                }
                metadataPrefilled = true;
                if (!statsPrefilled)
                {
                    FillStats(maps[0]);
                    statsPrefilled = true;
                }
                AppendLog(Lang.T("已添加 " + added + " 张谱面。", "Added " + added + " chart(s)."));
            }
            RefreshList();
        }

        private void FillStats(Beatmap m)
        {
            double od, hp;
            string s = Util.GetValue(m.DifficultyLines, "OverallDifficulty");
            if (!string.IsNullOrEmpty(s) && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out od))
                numOD.Value = ClampDecimal(od, numOD.Minimum, numOD.Maximum);
            s = Util.GetValue(m.DifficultyLines, "HPDrainRate");
            if (!string.IsNullOrEmpty(s) && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out hp))
                numHP.Value = ClampDecimal(hp, numHP.Minimum, numHP.Maximum);
        }

        private decimal ClampDecimal(double v, decimal min, decimal max)
        {
            decimal d = (decimal)v;
            if (d < min) d = min;
            if (d > max) d = max;
            return d;
        }

        private void RemoveSelected()
        {
            List<int> idx = new List<int>();
            foreach (int i in lv.SelectedIndices) idx.Add(i);
            idx.Sort();
            for (int i = idx.Count - 1; i >= 0; i--) maps.RemoveAt(idx[i]);
            if (maps.Count == 0) { metadataPrefilled = false; statsPrefilled = false; }
            RefreshList();
        }

        private void MoveSelected(int dir)
        {
            if (lv.SelectedIndices.Count != 1) return;
            int i = lv.SelectedIndices[0];
            int j = i + dir;
            if (j < 0 || j >= maps.Count) return;
            Beatmap tmp = maps[i];
            maps[i] = maps[j];
            maps[j] = tmp;
            RefreshList();
            lv.Items[j].Selected = true;
            lv.Items[j].Focused = true;
        }

        private void RefreshList()
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            for (int i = 0; i < maps.Count; i++)
            {
                Beatmap m = maps[i];
                ListViewItem it = new ListViewItem((i + 1).ToString());
                it.SubItems.Add(m.DisplayName);
                it.SubItems.Add(m.Keys + "K");
                it.SubItems.Add(m.HitObjects.Count.ToString());
                it.SubItems.Add(Util.FormatTime(m.FirstNote));
                it.SubItems.Add(Util.FormatTime(m.LastEnd));
                it.SubItems.Add(m.AudioFilename);
                lv.Items.Add(it);
            }
            lv.EndUpdate();
        }

        private void RefreshFfmpeg()
        {
            ffmpegPath = Ffmpeg.Find();
            if (ffmpegPath == null)
            {
                lblFfmpeg.Text = Lang.T("ffmpeg：未找到（音频拼接需要）", "ffmpeg: not found (required)");
                lblFfmpeg.ForeColor = Color.Firebrick;
            }
            else
            {
                lblFfmpeg.Text = "ffmpeg: " + ffmpegPath;
                lblFfmpeg.ForeColor = Color.FromArgb(46, 125, 50);
            }
        }

        private void PickFfmpeg()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "ffmpeg (ffmpeg.exe)|ffmpeg.exe|" + Lang.T("所有文件 (*.*)|*.*", "All files (*.*)|*.*");
                d.Title = Lang.T("选择 ffmpeg.exe", "Select ffmpeg.exe");
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    ffmpegPath = d.FileName;
                    lblFfmpeg.Text = "ffmpeg: " + ffmpegPath;
                    lblFfmpeg.ForeColor = Color.FromArgb(46, 125, 50);
                }
            }
        }

        private void PickOutputDir()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = Lang.T("选择输出文件夹", "Choose output folder");
                if (txtOutDir.Text.Length > 0) d.SelectedPath = txtOutDir.Text;
                if (d.ShowDialog(this) == DialogResult.OK) txtOutDir.Text = d.SelectedPath;
            }
        }

        private void SetDefaultOutputDir()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OsuManiaMerger");
                try { Directory.CreateDirectory(dir); } catch { }
            }
            txtOutDir.Text = dir;
        }

        private void UpdateDefaultName()
        {
            if (nameTouched) return;
            string a = txtArtist.Text.Trim();
            string t = txtTitle.Text.Trim();
            string raw;
            if (a.Length > 0 && t.Length > 0) raw = a + " - " + t;
            else if (t.Length > 0) raw = t;
            else if (a.Length > 0) raw = a;
            else raw = Lang.T("合谱", "Merged");

            updatingName = true;
            txtName.Text = Util.SafeFileName(raw, Lang.T("合谱", "Merged"));
            updatingName = false;
        }

        private void AppendLog(string s)
        {
            txtLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s + Environment.NewLine);
        }

        private string PresetPath()
        {
            return Path.Combine(AppSettings.Dir(), "presets.txt");
        }

        private void LoadPresets()
        {
            presets.Clear();
            try
            {
                string f = PresetPath();
                if (!File.Exists(f)) return;
                foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                {
                    if (line.Trim().Length == 0) continue;
                    string[] p = line.Split('\t');
                    if (p.Length < 6) continue;
                    MetadataPreset mp = new MetadataPreset();
                    mp.Name = p[0];
                    if (mp.Name == "上次使用" || mp.Name == "Last used") mp.Name = "@last";
                    mp.Title = p[1];
                    mp.Artist = p[2];
                    mp.Creator = p[3];
                    mp.Difficulty = p[4];
                    mp.AudioName = p[5];
                    presets.Add(mp);
                }
            }
            catch
            {
            }
        }

        private void PersistPresets()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (MetadataPreset p in presets)
                {
                    sb.AppendLine(string.Join("\t", new string[]
                    {
                        CleanField(p.Name), CleanField(p.Title), CleanField(p.Artist),
                        CleanField(p.Creator), CleanField(p.Difficulty), CleanField(p.AudioName)
                    }));
                }
                File.WriteAllText(PresetPath(), sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                AppendLog(Lang.T("预设保存失败：", "Failed to save presets: ") + ex.Message);
            }
        }

        private string CleanField(string s)
        {
            if (s == null) return "";
            return s.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private void SavePreset(MetadataPreset p)
        {
            if (p == null || string.IsNullOrEmpty(p.Name)) return;
            bool found = false;
            for (int i = 0; i < presets.Count; i++)
            {
                if (string.Equals(presets[i].Name, p.Name, StringComparison.OrdinalIgnoreCase))
                {
                    presets[i] = p;
                    found = true;
                    break;
                }
            }
            if (!found) presets.Add(p);
            PersistPresets();
        }

        private bool HasPreset(string id)
        {
            foreach (MetadataPreset p in presets)
                if (string.Equals(p.Name, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private MetadataPreset DefaultPreset()
        {
            MetadataPreset p = new MetadataPreset();
            p.Name = "@default";
            p.Title = Lang.T("合谱", "Merged");
            p.Artist = "Unknown";
            p.Creator = "osu!mania Merger";
            p.Difficulty = "Merged";
            p.AudioName = "audio";
            return p;
        }

        private MetadataPreset FirstChartPreset()
        {
            MetadataPreset p = DefaultPreset();
            p.Name = "@first";
            if (maps.Count == 0) return p;
            Beatmap m = maps[0];
            string t = Util.GetValue(m.MetadataLines, "Title");
            string a = Util.GetValue(m.MetadataLines, "Artist");
            string c = Util.GetValue(m.MetadataLines, "Creator");
            string v = Util.GetValue(m.MetadataLines, "Version");
            if (!string.IsNullOrEmpty(t)) p.Title = t;
            if (!string.IsNullOrEmpty(a)) p.Artist = a;
            if (!string.IsNullOrEmpty(c)) p.Creator = c;
            if (!string.IsNullOrEmpty(v)) p.Difficulty = v;
            p.AudioName = "audio";
            return p;
        }

        private MetadataPreset CurrentPreset(string name)
        {
            MetadataPreset p = new MetadataPreset();
            p.Name = name;
            p.Title = txtTitle.Text.Trim();
            p.Artist = txtArtist.Text.Trim();
            p.Creator = txtCreator.Text.Trim();
            p.Difficulty = txtDifficulty.Text.Trim();
            p.AudioName = StripMp3(txtAudioName.Text.Trim());
            return p;
        }

        private string StripMp3(string name)
        {
            if (name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - 4);
            return name;
        }

        private bool IsDefaultMetadata()
        {
            MetadataPreset d = DefaultPreset();
            return txtTitle.Text.Trim() == d.Title
                && txtArtist.Text.Trim() == d.Artist
                && txtCreator.Text.Trim() == d.Creator
                && txtDifficulty.Text.Trim() == d.Difficulty
                && StripMp3(txtAudioName.Text.Trim()) == d.AudioName;
        }

        private void RefreshPresetCombo(string selectId)
        {
            applyingPreset = true;
            cboPreset.Items.Clear();
            PresetChoice first = new PresetChoice();
            first.Id = "@first";
            first.Display = Lang.T("从第一张谱面读取", "From first chart");
            cboPreset.Items.Add(first);
            PresetChoice def = new PresetChoice();
            def.Id = "@default";
            def.Display = Lang.T("合谱默认值", "Default values");
            cboPreset.Items.Add(def);
            foreach (MetadataPreset p in presets)
            {
                PresetChoice pc = new PresetChoice();
                pc.Id = p.Name;
                pc.Display = (p.Name == "@last") ? Lang.T("上次使用", "Last used") : p.Name;
                cboPreset.Items.Add(pc);
            }

            int idx = 1;
            if (!string.IsNullOrEmpty(selectId))
            {
                for (int i = 0; i < cboPreset.Items.Count; i++)
                {
                    PresetChoice pc = cboPreset.Items[i] as PresetChoice;
                    if (pc != null && string.Equals(pc.Id, selectId, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }
            }
            if (idx >= cboPreset.Items.Count) idx = 1;
            cboPreset.SelectedIndex = idx;
            applyingPreset = false;
        }

        private void ApplyPreset(int idx)
        {
            if (applyingPreset || idx < 0) return;
            MetadataPreset p = null;
            if (idx == 0) p = FirstChartPreset();
            else if (idx == 1) p = DefaultPreset();
            else if (idx - 2 < presets.Count) p = presets[idx - 2];
            if (p == null) return;

            applyingPreset = true;
            nameTouched = false;
            txtTitle.Text = p.Title;
            txtArtist.Text = p.Artist;
            txtCreator.Text = p.Creator;
            txtDifficulty.Text = p.Difficulty;
            txtAudioName.Text = StripMp3(p.AudioName);
            applyingPreset = false;
            UpdateDefaultName();
        }

        private void InitPresets()
        {
            RefreshPresetCombo(HasPreset("@last") ? "@last" : null);
            ApplyPreset(cboPreset.SelectedIndex);
        }

        private void SaveCurrentAsPreset()
        {
            string name = null;
            using (PromptForm f = new PromptForm(Lang.T("保存预设", "Save preset"), Lang.T("预设名称：", "Preset name:"), txtTitle.Text.Trim()))
            {
                if (f.ShowDialog(this) == DialogResult.OK) name = f.Value;
            }
            if (string.IsNullOrEmpty(name)) return;
            if (name.StartsWith("@")
                || string.Equals(name, "从第一张谱面读取", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "From first chart", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "合谱默认值", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Default values", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "上次使用", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Last used", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, Lang.T("这个名称是内置选项，换一个吧。", "That name is reserved; pick another one."),
                    Lang.T("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SavePreset(CurrentPreset(name));
            RefreshPresetCombo(name);
            AppendLog(Lang.T("已保存预设：", "Preset saved: ") + name);
        }

        private void StartMerge()
        {
            if (maps.Count < 2)
            {
                MessageBox.Show(this, Lang.T("至少需要 2 张谱面。", "At least 2 charts are required."),
                    Lang.T("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            for (int i = 1; i < maps.Count; i++)
            {
                if (maps[i].Keys != maps[0].Keys)
                {
                    MessageBox.Show(this, string.Format(Lang.T("键数不一致：第 1 张是 {0}K，第 {1} 张是 {2}K。\n\n请先统一键数再合并。",
                        "Key count mismatch: chart 1 is {0}K, chart {1} is {2}K.\n\nMake them equal first."),
                        maps[0].Keys, i + 1, maps[i].Keys), Lang.T("键数不一致", "Key count mismatch"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                MessageBox.Show(this, Lang.T("没有找到 ffmpeg，无法拼接音频并生成 .osz。\n\n请把 ffmpeg.exe 放到程序旁边，或点“选择 ffmpeg...”指定。",
                    "ffmpeg was not found, so audio cannot be merged and the .osz cannot be built.\n\nPut ffmpeg.exe next to this app, or use Select ffmpeg..."),
                    Lang.T("缺少 ffmpeg", "ffmpeg missing"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            for (int i = 0; i < maps.Count; i++)
            {
                string p = maps[i].ResolveAudio();
                if (string.IsNullOrEmpty(p) || !File.Exists(p))
                {
                    MessageBox.Show(this, string.Format(Lang.T("第 {0} 张谱面的音频不存在：\n{1}\n\n请确认 .osu 和音频在同一个文件夹。",
                        "Audio for chart {0} was not found:\n{1}\n\nKeep the .osu and its audio in the same folder."),
                        i + 1, p), Lang.T("找不到音频", "Audio missing"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            string dir = txtOutDir.Text.Trim();
            if (dir.Length == 0)
            {
                MessageBox.Show(this, Lang.T("请选择输出文件夹。", "Choose an output folder."),
                    Lang.T("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("无法创建输出文件夹：\n", "Cannot create output folder:\n") + ex.Message,
                    Lang.T("错误", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!nameTouched) UpdateDefaultName();
            string name = Util.SafeFileName(txtName.Text, Lang.T("合谱", "Merged"));            string outOsz = Path.Combine(dir, name + ".osz");
            if (File.Exists(outOsz))
            {
                if (MessageBox.Show(this, Lang.T("输出文件已存在，是否覆盖？\n", "Output file already exists. Overwrite?\n") + outOsz,
                    Lang.T("确认覆盖", "Confirm overwrite"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }

            int gapMs = (int)Math.Round(numGap.Value * 1000M);
            if (gapMs < 0) gapMs = 0;

            MergeOptions opt = new MergeOptions();
            opt.GapMs = gapMs;
            opt.FadeMs = Merger.DefaultFadeMs;
            opt.Title = txtTitle.Text.Trim();
            opt.Artist = txtArtist.Text.Trim();
            opt.Creator = txtCreator.Text.Trim();
            opt.Difficulty = txtDifficulty.Text.Trim();
            opt.AudioName = Util.EnsureMp3(txtAudioName.Text);
            opt.OutOsz = outOsz;
            opt.Ffmpeg = ffmpegPath;
            opt.OverallDifficulty = (double)numOD.Value;
            opt.HPDrainRate = (double)numHP.Value;

            btnMerge.Enabled = false;
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 30;
            txtLog.Clear();

            MergeJob job = new MergeJob();
            job.Maps = new List<Beatmap>(maps);
            job.Options = opt;
            worker.RunWorkerAsync(job);
        }

        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            MergeJob job = (MergeJob)e.Argument;
            BackgroundWorker bw = (BackgroundWorker)sender;
            Action<string> log = delegate(string s) { bw.ReportProgress(0, s); };
            Merger.MergeToFile(job.Maps, job.Options, log);
            e.Result = job;
        }

        private void WorkerProgress(object sender, ProgressChangedEventArgs e)
        {
            string s = e.UserState as string;
            if (s != null) AppendLog(s);
        }

        private void WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnMerge.Enabled = true;
            progress.Style = ProgressBarStyle.Blocks;
            progress.MarqueeAnimationSpeed = 0;
            progress.Value = 0;

            if (e.Error != null)
            {
                AppendLog(Lang.T("失败：", "Failed: ") + e.Error.Message);
                MessageBox.Show(this, e.Error.Message, Lang.T("合并失败", "Merge failed"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MergeJob job = e.Result as MergeJob;
            AppendLog(Lang.T("合并完成。", "Merge complete."));
            SavePreset(CurrentPreset("@last"));
            RefreshPresetCombo("@last");

            if (job != null)
            {
                DialogResult r = MessageBox.Show(this,
                    Lang.T("合并完成！\n\n", "Merge complete!\n\n") + job.Options.OutOsz
                    + Lang.T("\n\n是否打开输出文件夹？", "\n\nOpen the output folder?"),
                    Lang.T("完成", "Done"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (r == DialogResult.Yes)
                {
                    try { Process.Start("explorer.exe", "/select,\"" + job.Options.OutOsz + "\""); } catch { }
                }
            }
        }
    }

    public class MetadataPreset
    {
        public string Name = "";
        public string Title = "";
        public string Artist = "";
        public string Creator = "";
        public string Difficulty = "";
        public string AudioName = "";
    }

    public class PresetChoice
    {
        public string Id;
        public string Display;
        public override string ToString() { return Display; }
    }

    public class PromptForm : Form
    {
        private TextBox box;
        public string Value { get { return box.Text.Trim(); } }

        public PromptForm(string title, string label, string initial)
        {
            Text = title;
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(380, 130);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            Label l = new Label();
            l.Text = label;
            l.SetBounds(16, 16, 348, 22);
            Controls.Add(l);

            box = new TextBox();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.SetBounds(16, 44, 348, 25);
            box.Text = initial;
            Controls.Add(box);

            Button ok = new Button();
            ok.Text = Lang.T("保存", "Save");
            ok.DialogResult = DialogResult.OK;
            ok.FlatStyle = FlatStyle.Flat;
            ok.FlatAppearance.BorderSize = 0;
            ok.BackColor = Color.FromArgb(43, 108, 224);
            ok.ForeColor = Color.White;
            ok.SetBounds(190, 84, 80, 30);
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = Lang.T("取消", "Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.FlatAppearance.BorderColor = Color.FromArgb(208, 212, 220);
            cancel.BackColor = Color.White;
            cancel.SetBounds(284, 84, 80, 30);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    public class MergeJob
    {
        public List<Beatmap> Maps;
        public MergeOptions Options;
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Lang.English = string.Equals(AppSettings.LoadLanguage(), "en", StringComparison.OrdinalIgnoreCase);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
