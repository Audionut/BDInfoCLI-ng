using System;

namespace BDInfo
{
    // Minimal, cross-platform settings shim for CLI builds.
    // Values are initialized to the same defaults used by the WinForms settings.
    public static class BDInfoSettings
    {
        public static bool GenerateStreamDiagnostics { get; set; } = true;
        public static bool ExtendedStreamDiagnostics { get; set; } = false;
        public static bool EnableSSIF { get; set; } = true;
        public static bool DisplayChapterCount { get; set; } = false;
        public static bool AutosaveReport { get; set; } = false;
        public static bool GenerateFrameDataFile { get; set; } = false;
        public static bool FilterLoopingPlaylists { get; set; } = true;
        public static bool FilterShortPlaylists { get; set; } = true;
        public static int FilterShortPlaylistsValue { get; set; } = 20;
        public static bool UseImagePrefix { get; set; } = false;
        public static string UseImagePrefixValue { get; set; } = "video-";
        public static bool KeepStreamOrder { get; set; } = true;
        public static bool GenerateTextSummary { get; set; } = true;
        public static string LastPath { get; set; } = string.Empty;

        public static void SaveSettings()
        {
            // No-op for CLI shim. If persisted settings are later required,
            // implement file-based storage here.
        }
    }
}
