using QFG5Extractor.Core;
using System;
using System.IO;

namespace QFG5Extractor.Core
{
    public static class Logger
    {
        public static string GetLogFilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "qfg5extractor.log");
        }

        static Logger()
        {
            // Initialize log file (clear it on first run or append if preferred)
            try
            {
                File.WriteAllText(GetLogFilePath(), $"--- Session Started: {DateTime.Now} ---{Environment.NewLine}");
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(GetLogFilePath(), $"[{DateTime.UtcNow}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        public static void OpenLogFile()
        {
            string path = GetLogFilePath();
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = false });
                }
                else
                {
                    System.Diagnostics.Process.Start(path);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Could not open log: " + ex.Message);
            }
        }

        public static System.Diagnostics.Stopwatch StartupWatch = System.Diagnostics.Stopwatch.StartNew();

        public static void LogStartup(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [STARTUP] {message}";
            try { Console.WriteLine(line); } catch { }
            Log(line);
        }

        public static void LogPerf(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [PERF] {message}";
            try { Console.WriteLine(line); } catch { }
            Log(line);
        }

        public static void LogResource(string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [RESOURCE] {message}";
            try { Console.WriteLine(line); } catch { }
            Log(line);
        }

        public static void CountControls(System.Windows.Forms.Control root, ref int total, ref int pictureBoxes, ref int panels, ref int buttons, ref int dataGrids)
        {
            if (root == null) return;
            total++;
            if (root is System.Windows.Forms.PictureBox) pictureBoxes++;
            else if (root is System.Windows.Forms.Panel) panels++;
            else if (root is System.Windows.Forms.Button) buttons++;
            else if (root is System.Windows.Forms.DataGridView) dataGrids++;
            foreach (System.Windows.Forms.Control child in root.Controls)
            {
                CountControls(child, ref total, ref pictureBoxes, ref panels, ref buttons, ref dataGrids);
            }
        }

        public static string GetMemorySnapshot()
        {
            long gcMem = GC.GetTotalMemory(false) / (1024 * 1024);
            long wsMem = 0;
            try { wsMem = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); } catch { }
            return $"GC: {gcMem} MB, WorkingSet: {wsMem} MB";
        }

        public static void LogError(string fileName, string error)
        {
            Log($"[ERROR] Failed processing {fileName}: {error}");
        }

        public static void LogInfo(string info)
        {
            Log($"[INFO] {info}");
        }

        public static void LogWarning(string warning)
        {
            Log($"[WARNING] {warning}");
        }
    }
}
