using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using BDInfo;
using Mono.Options;
using System.Threading;

namespace BDInfo.Cli
{
    internal static class Program
    {
        static void show_help(OptionSet option_set, string msg = null)
        {
            if (msg != null)
                Console.Error.WriteLine(msg);
            Console.Error.WriteLine("Usage: bdinfo-cli <BD_PATH> [REPORT_DEST]");
            Console.Error.WriteLine("BD_PATH may be a directory containing a BDMV folder or a BluRay ISO file.");
            Console.Error.WriteLine("REPORT_DEST is the folder the BDInfo report is to be written to. If not");
            Console.Error.WriteLine("given, the report will be written to BD_PATH. REPORT_DEST is required if");
            Console.Error.WriteLine("BD_PATH is an ISO file.\n");
            option_set.WriteOptionDescriptions(Console.Error);
            Environment.Exit(-1);
        }

        private static int getIntIndex(int min, int max)
        {
            string response;
            int resp = -1;
            do
            {
                while (Console.KeyAvailable)
                    Console.ReadKey();

                Console.Write("Select (q when finished): ");
                response = Console.ReadLine();
                if (response == "q")
                    return -1;

                try
                {
                    resp = int.Parse(response);
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid Input!");
                }

                if (resp > max || resp < min)
                {
                    Console.WriteLine("Invalid Selection!");
                }
            } while (resp > max || resp < min);

            Console.WriteLine();

            return resp;
        }

        private class ScanState
        {
            public TSStreamFile StreamFile;
            public List<TSPlaylistFile> Playlists;
            public long FinishedBytes;
            public Exception Exception;
            public DateTime StartTime;
        }

        private static void ScanBDROMProgress(object state)
        {
            try
            {
                var s = (ScanState)state;
                if (s?.StreamFile == null) return;
                long scanned = s.StreamFile.Size;
                long total = 0;
                if (BDInfoSettings.EnableSSIF && s.StreamFile.InterleavedFile != null)
                    total = s.StreamFile.InterleavedFile.FileInfo?.Length ?? s.StreamFile.InterleavedFile.DFileInfo.Length;
                else
                    total = s.StreamFile.FileInfo?.Length ?? s.StreamFile.DFileInfo.Length;

                var elapsed = DateTime.Now - s.StartTime;
                string etaStr = "--:--:--";
                if (scanned > 0 && elapsed.TotalSeconds > 0 && total > 0)
                {
                    double rate = scanned / elapsed.TotalSeconds; // bytes/sec
                    if (rate > 0)
                    {
                        double remSec = Math.Max(0, total - scanned) / rate;
                        var eta = TimeSpan.FromSeconds(remSec);
                        etaStr = eta.ToString("hh\\:mm\\:ss");
                    }
                }

                string status = String.Format("{0,16}{1,-25}{2,-13}{3}", "", s.StreamFile.Name, elapsed.ToString("hh\\:mm\\:ss"), etaStr);
                // Overwrite the same console line
                try
                {
                    int width = Console.BufferWidth > 0 ? Console.BufferWidth : 120;
                    Console.Write('\r' + status.PadRight(Math.Max(0, width - 1)));
                }
                catch
                {
                    // Fallback if console buffer not available
                    Console.Write('\r' + status + "\n");
                }
            }
            catch { }
        }

        private static void ScanBDROMThread(object state)
        {
            var s = (ScanState)state;
            try
            {
                s.StreamFile.Scan(s.Playlists, true);
            }
            catch (Exception ex)
            {
                s.Exception = ex;
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            bool help = false;
            bool version = false;
            bool whole = false;
            bool list = false;
            string mpls = null;

            OptionSet option_set = new OptionSet()
                .Add("h|help", "Print out the options.", option => help = option != null)
                .Add("l|list", "Print the list of playlists.", option => list = option != null)
                .Add("m=|mpls=", "Comma separated list of playlists to scan.", option => mpls = option)
                .Add("w|whole", "Scan whole disc - every playlist.", option => whole = option != null)
                .Add("v|version", "Print the version.", option => version = option != null)
            ;

            List<string> nsargs = new List<string>();
            try
            {
                nsargs = option_set.Parse(args);
            }
            catch (OptionException)
            {
                show_help(option_set, "Error - usage is:");
            }

            if (help)
                show_help(option_set);

            if (version)
            {
                Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version.ToString());
                return 0;
            }

            if (list)
                whole = true;

            if (nsargs.Count == 0)
            {
                show_help(option_set, "Error: insufficient args - usage is:");
            }

            string bdPath = nsargs[0];
            if (!File.Exists(bdPath) && !Directory.Exists(bdPath))
            {
                Console.Error.WriteLine(String.Format("error: {0} does not exist", bdPath));
                return 2;
            }

            string reportPath = bdPath;
            if (nsargs.Count == 1 && !Directory.Exists(bdPath))
            {
                Console.Error.WriteLine(String.Format("error: REPORT_DEST must be given if BD_PATH is an ISO.", bdPath));
                return 2;
            }
            if (nsargs.Count == 2)
                reportPath = nsargs[1];
            if (!Directory.Exists(reportPath))
            {
                Console.Error.WriteLine(String.Format("error: {0} does not exist or is not a directory", reportPath));
                return 2;
            }

            // Create BDROM and scan
            Console.WriteLine("Please wait while we scan the disc...");
            BDROM bdrom;
            try
            {
                bdrom = new BDROM(bdPath);
                bdrom.StreamClipFileScanError += (clip, ex) => { Console.Error.WriteLine($"StreamClip error: {clip?.Name} {ex.Message}"); return true; };
                bdrom.StreamFileScanError += (stream, ex) => { Console.Error.WriteLine($"StreamFile error: {stream?.Name} {ex.Message}"); return true; };
                bdrom.PlaylistFileScanError += (playlist, ex) => { Console.Error.WriteLine($"Playlist error: {playlist?.Name} {ex.Message}"); return true; };
                bdrom.Scan();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error scanning BD: {ex.Message}");
                return 1;
            }

            // Playlist selection
            List<TSPlaylistFile> selectedPlaylists = new List<TSPlaylistFile>();

            // Build groups and print playlists similar to original LoadPlaylists
            bool hasHiddenTracks = false;
            List<List<TSPlaylistFile>> groups = new List<List<TSPlaylistFile>>();

            TSPlaylistFile[] sortedPlaylistFiles = new TSPlaylistFile[bdrom.PlaylistFiles.Count];
            bdrom.PlaylistFiles.Values.CopyTo(sortedPlaylistFiles, 0);
            Array.Sort(sortedPlaylistFiles, (a, b) => String.Compare(a.Name, b.Name, StringComparison.Ordinal));

            foreach (TSPlaylistFile playlist1 in sortedPlaylistFiles)
            {
                if (!playlist1.IsValid) continue;

                int matchingGroupIndex = 0;
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    List<TSPlaylistFile> group = groups[groupIndex];
                    foreach (TSPlaylistFile playlist2 in group)
                    {
                        if (!playlist2.IsValid) continue;
                        foreach (TSStreamClip clip1 in playlist1.StreamClips)
                        {
                            foreach (TSStreamClip clip2 in playlist2.StreamClips)
                            {
                                if (clip1.Name == clip2.Name)
                                {
                                    matchingGroupIndex = groupIndex + 1;
                                    break;
                                }
                            }
                            if (matchingGroupIndex > 0) break;
                        }
                        if (matchingGroupIndex > 0) break;
                    }
                    if (matchingGroupIndex > 0) break;
                }
                if (matchingGroupIndex > 0)
                {
                    groups[matchingGroupIndex - 1].Add(playlist1);
                }
                else
                {
                    groups.Add(new List<TSPlaylistFile> { playlist1 });
                }
            }

            int playlistIdx = 1;
            Dictionary<int, TSPlaylistFile> playlistDict = new Dictionary<int, TSPlaylistFile>();

            // Populate playlist dictionary (no full table print to keep CLI output minimal).
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                List<TSPlaylistFile> group = groups[groupIndex];
                group.Sort((x, y) => String.Compare(x.Name, y.Name, StringComparison.Ordinal));
                foreach (TSPlaylistFile playlist in group)
                {
                    if (!playlist.IsValid) continue;
                    playlistDict[playlistIdx] = playlist;
                    if (whole)
                        selectedPlaylists.Add(playlist);
                    playlistIdx++;
                }
            }
            if (whole)
            {
                // already populated selectedPlaylists
            }
            else if (mpls != null)
            {
                foreach (string playlistName in mpls.Split(','))
                {
                    string Name = playlistName.ToUpper();
                    if (bdrom.PlaylistFiles.ContainsKey(Name))
                    {
                        var pl = bdrom.PlaylistFiles[Name];
                        if (!selectedPlaylists.Contains(pl)) selectedPlaylists.Add(pl);
                    }
                }
                if (selectedPlaylists.Count == 0)
                {
                    Console.Error.WriteLine("No matching playlists found on BD");
                    return 1;
                }
            }
            else
            {
                for (int selectedIdx; (selectedIdx = getIntIndex(1, playlistIdx - 1)) > 0; )
                {
                    selectedPlaylists.Add(playlistDict[selectedIdx]);
                    Console.WriteLine(String.Format("Added {0}", selectedIdx));
                }
                if (selectedPlaylists.Count == 0)
                {
                    Console.WriteLine("No playlists selected. Exiting.");
                    return 0;
                }
            }

            if (list)
            {
                return 0;
            }

            // Scan selected playlists
            var scanResult = new ScanBDROMResult { ScanException = new Exception("Scan is still running.") };
            List<TSStreamFile> streamFiles = new List<TSStreamFile>();
            // Build unique stream file list for selected playlists
            foreach (TSPlaylistFile playlist in selectedPlaylists)
            {
                foreach (TSStreamClip clip in playlist.StreamClips)
                {
                    if (!streamFiles.Contains(clip.StreamFile))
                    {
                        streamFiles.Add(clip.StreamFile);
                    }
                }
            }

            try
            {
                long totalBytes = 0;
                foreach (TSStreamFile sf in streamFiles)
                {
                    if (BDInfoSettings.EnableSSIF && sf.InterleavedFile != null)
                    {
                        totalBytes += sf.InterleavedFile.FileInfo?.Length ?? sf.InterleavedFile.DFileInfo.Length;
                    }
                    else
                    {
                        totalBytes += sf.FileInfo?.Length ?? sf.DFileInfo.Length;
                    }
                }

                Console.WriteLine();

                if (whole)
                {
                    Console.WriteLine();
                    Console.WriteLine("{0,16}{1,-15}{2,-13}{3}", "", "File", "Elapsed", "Remaining");
                    Console.Write("Scanning entire disc...");
                    var discSw = System.Diagnostics.Stopwatch.StartNew();
                    var scanState = new ScanState();
                    foreach (TSStreamFile streamFile in streamFiles)
                    {
                        try
                        {
                            List<TSPlaylistFile> playlists = new List<TSPlaylistFile>();
                            foreach (TSPlaylistFile p in bdrom.PlaylistFiles.Values)
                            {
                                foreach (TSStreamClip clip in p.StreamClips)
                                {
                                    if (clip.Name == streamFile.Name)
                                    {
                                        if (!playlists.Contains(p)) playlists.Add(p);
                                    }
                                }
                            }

                            scanState.StreamFile = streamFile;
                            scanState.Playlists = playlists;
                            scanState.Exception = null;
                            scanState.StartTime = DateTime.Now;
                            using (var timer = new System.Threading.Timer(ScanBDROMProgress, scanState, 1000, 1000))
                            {
                                Thread thread = new Thread(ScanBDROMThread);
                                thread.Start(scanState);
                                while (thread.IsAlive)
                                {
                                    Thread.Sleep(250);
                                }
                            }

                            // print a final status line for this file and move to next line
                            try { ScanBDROMProgress(scanState); Console.WriteLine(); } catch { }

                            // update finished bytes estimate
                            if (streamFile.FileInfo != null)
                                scanState.FinishedBytes += streamFile.FileInfo.Length;
                            else
                                scanState.FinishedBytes += streamFile.DFileInfo.Length;

                            if (scanState.Exception != null)
                            {
                                scanResult.FileExceptions[streamFile.Name] = scanState.Exception;
                            }
                        }
                        catch (Exception ex)
                        {
                            scanResult.FileExceptions[streamFile.Name] = ex;
                        }
                    }
                    discSw.Stop();
                    Console.WriteLine($" done ({discSw.Elapsed:hh\\:mm\\:ss})");
                }
                else
                {
                    var scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (TSPlaylistFile playlist in selectedPlaylists)
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();

                        // Print a short progress header for this playlist (matches original CLI behavior)
                        TimeSpan playlistLengthSpan = new TimeSpan((long)(playlist.TotalLength * 10000000));
                        int hours = (int)playlistLengthSpan.TotalHours;
                        Console.WriteLine(String.Format("{0,16}{1,-15}{2,-13}{3}", "", "File", "Elapsed", "Remaining"));

                        List<TSStreamFile> filesForPlaylist = new List<TSStreamFile>();
                        foreach (TSStreamClip clip in playlist.StreamClips)
                        {
                            if (!scanned.Contains(clip.StreamFile.Name))
                            {
                                filesForPlaylist.Add(clip.StreamFile);
                            }
                        }

                        var scanState = new ScanState();
                        foreach (TSStreamFile streamFile in filesForPlaylist)
                        {
                            try
                            {
                                List<TSPlaylistFile> playlists = new List<TSPlaylistFile>();
                                foreach (TSPlaylistFile p in bdrom.PlaylistFiles.Values)
                                {
                                    foreach (TSStreamClip clip in p.StreamClips)
                                    {
                                        if (clip.Name == streamFile.Name)
                                        {
                                            if (!playlists.Contains(p)) playlists.Add(p);
                                        }
                                    }
                                }

                                scanState.StreamFile = streamFile;
                                scanState.Playlists = playlists;
                                scanState.Exception = null;
                                scanState.StartTime = DateTime.Now;
                                using (var timer = new System.Threading.Timer(ScanBDROMProgress, scanState, 1000, 1000))
                                {
                                    Thread thread = new Thread(ScanBDROMThread);
                                    thread.Start(scanState);
                                    while (thread.IsAlive)
                                    {
                                        Thread.Sleep(250);
                                    }
                                }
                                
                                // print a final status line for this file and move to next line
                                try { ScanBDROMProgress(scanState); Console.WriteLine(); } catch { }
                                if (streamFile.FileInfo != null)
                                    scanState.FinishedBytes += streamFile.FileInfo.Length;
                                else
                                    scanState.FinishedBytes += streamFile.DFileInfo.Length;

                                if (scanState.Exception != null)
                                {
                                    scanResult.FileExceptions[streamFile.Name] = scanState.Exception;
                                }

                                scanned.Add(streamFile.Name);
                            }
                            catch (Exception ex)
                            {
                                scanResult.FileExceptions[streamFile.Name] = ex;
                            }
                        }
                        sw.Stop();
                        Console.WriteLine($"Playlist {playlist.Name} done ({sw.Elapsed:hh\\:mm\\:ss})");
                    }
                }
                scanResult.ScanException = null;
            }
            catch (Exception ex)
            {
                scanResult.ScanException = ex;
            }

            // Generate report (headless)
            try
            {
                Console.WriteLine("Please wait while we generate the report...");
                new ReportGenerator().Generate(bdrom, selectedPlaylists, scanResult, reportPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            return 0;
        }
    }
}
        
