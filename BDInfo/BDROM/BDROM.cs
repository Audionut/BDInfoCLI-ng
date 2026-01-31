//============================================================================
// BDInfo - Blu-ray Video and Audio Analysis Tool
// Copyright © 2010 Cinema Squid
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//=============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using DiscUtils;
using DiscUtils.Udf;

namespace BDInfo
{
    public class BDROM
    {
        public DirectoryInfo? DirectoryRoot = null;
        public DirectoryInfo? DirectoryBDMV = null;

        public DirectoryInfo? DirectoryBDJO = null;
        public DirectoryInfo? DirectoryCLIPINF = null;
        public DirectoryInfo? DirectoryPLAYLIST = null;
        public DirectoryInfo? DirectorySNP = null;
        public DirectoryInfo? DirectorySSIF = null;
        public DirectoryInfo? DirectorySTREAM = null;
        public DirectoryInfo? DirectoryMETA = null;

        public DiscDirectoryInfo? DiscDirectoryRoot = null;
        public DiscDirectoryInfo? DiscDirectoryBDMV = null;

        public DiscDirectoryInfo? DiscDirectoryBDJO = null;
        public DiscDirectoryInfo? DiscDirectoryCLIPINF = null;
        public DiscDirectoryInfo? DiscDirectoryPLAYLIST = null;
        public DiscDirectoryInfo? DiscDirectorySNP = null;
        public DiscDirectoryInfo? DiscDirectorySSIF = null;
        public DiscDirectoryInfo? DiscDirectorySTREAM = null;
        public DiscDirectoryInfo? DiscDirectoryMETA = null;

        public string? VolumeLabel = null;
        public string? DiscTitle = null;
        public ulong Size = 0;
        public bool IsBDPlus = false;
        public bool IsBDJava = false;
        public bool IsDBOX = false;
        public bool IsPSP = false;
        public bool Is3D = false;
        public bool Is50Hz = false;
        public bool IsUHD = false;

        public bool IsImage = false;
        public FileStream? IoStream = null;
        public UdfReader? CdReader = null;

        public Dictionary<string, TSPlaylistFile> PlaylistFiles =
            new Dictionary<string, TSPlaylistFile>();
        public Dictionary<string, TSStreamClipFile> StreamClipFiles =
            new Dictionary<string, TSStreamClipFile>();
        public Dictionary<string, TSStreamFile> StreamFiles =
            new Dictionary<string, TSStreamFile>();
        public Dictionary<string, TSInterleavedFile> InterleavedFiles =
            new Dictionary<string, TSInterleavedFile>();

        private static List<string> ExcludeDirs = new List<string> { "ANY!", "AACS", "BDSVM", "ANYVM", "SLYVM" };

        public delegate bool OnStreamClipFileScanError(
            TSStreamClipFile streamClipFile, Exception ex);

        public event OnStreamClipFileScanError? StreamClipFileScanError;

        public delegate bool OnStreamFileScanError(
            TSStreamFile streamClipFile, Exception ex);

        public event OnStreamFileScanError? StreamFileScanError;

        public delegate bool OnPlaylistFileScanError(
            TSPlaylistFile playlistFile, Exception ex);

        public event OnPlaylistFileScanError? PlaylistFileScanError;

        public BDROM(
            string path)
        {
            //
            // Locate BDMV directories.
            //
            if ((new FileInfo(path).Attributes & FileAttributes.Directory) != FileAttributes.Directory)
            {
                IsImage = true;
                IoStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                CdReader = new UdfReader(IoStream);
            }

            if (!IsImage)
                DirectoryBDMV = GetDirectoryBDMV(path);
            else
                DiscDirectoryBDMV = GetDiscDirectoryBDMV();

            if ((!IsImage && DirectoryBDMV == null) || (IsImage && DiscDirectoryBDMV == null))
            {
                throw new Exception("Unable to locate BD structure.");
            }

            if (IsImage)
            {
                var discBdmv = DiscDirectoryBDMV!;
                DiscDirectoryRoot = discBdmv.Parent;

                DiscDirectoryBDJO = GetDiscDirectory("BDJO", discBdmv, 0);
                DiscDirectoryCLIPINF = GetDiscDirectory("CLIPINF", discBdmv, 0);
                DiscDirectoryPLAYLIST = GetDiscDirectory("PLAYLIST", discBdmv, 0);
                DiscDirectorySNP = GetDiscDirectory("SNP", DiscDirectoryRoot, 0);
                DiscDirectorySTREAM = GetDiscDirectory("STREAM", discBdmv, 0);
                DiscDirectorySSIF = GetDiscDirectory("SSIF", DiscDirectorySTREAM, 0);
                DiscDirectoryMETA = GetDiscDirectory("META", discBdmv, 0);
            }
            else
            {
                var dirBdmv = DirectoryBDMV!;
                DirectoryRoot = dirBdmv.Parent;

                DirectoryBDJO = GetDirectory("BDJO", dirBdmv, 0);
                DirectoryCLIPINF = GetDirectory("CLIPINF", dirBdmv, 0);
                DirectoryPLAYLIST = GetDirectory("PLAYLIST", dirBdmv, 0);
                DirectorySNP = GetDirectory("SNP", DirectoryRoot, 0);
                DirectorySTREAM = GetDirectory("STREAM", dirBdmv, 0);
                DirectorySSIF = GetDirectory("SSIF", DirectorySTREAM, 0);
                DirectoryMETA = GetDirectory("META", dirBdmv, 0);
            }

            if ((!IsImage & (DirectoryCLIPINF == null || DirectoryPLAYLIST == null)) || (IsImage & (DiscDirectoryCLIPINF == null || DiscDirectoryPLAYLIST == null)))
            {
                throw new Exception("Unable to locate BD structure.");
            }

            //
            // Initialize basic disc properties.
            //
            if (IsImage)
            {
                VolumeLabel = CdReader?.VolumeLabel;
                Size = (ulong)GetDiscDirectorySize(DiscDirectoryRoot);

                var indexFiles = DiscDirectoryBDMV?.GetFiles();
                DiscFileInfo? indexFile = null;

                for (int i = 0; i < indexFiles?.Length; i++)
                {
                    if (indexFiles[i].Name.ToLower() == "index.bdmv")
                    {
                        indexFile = indexFiles[i];
                        break;
                    }
                }

                if (indexFile != null)
                {
                    using (var indexStream = indexFile.OpenRead())
                    {
                        ReadIndexVersion(indexStream);
                    }
                }

                if (null != GetDiscDirectory("BDSVM", DiscDirectoryRoot, 0))
                {
                    IsBDPlus = true;
                }
                if (null != GetDiscDirectory("SLYVM", DiscDirectoryRoot, 0))
                {
                    IsBDPlus = true;
                }
                if (null != GetDiscDirectory("ANYVM", DiscDirectoryRoot, 0))
                {
                    IsBDPlus = true;
                }

                if (DiscDirectoryBDJO != null)
                {
                    var ddjo = DiscDirectoryBDJO!;
                    if (ddjo.GetFiles().Length > 0)
                    {
                        IsBDJava = true;
                    }
                }

                if (DiscDirectorySNP != null)
                {
                    var dsnp = DiscDirectorySNP!;
                    if (dsnp.GetFiles("*.mnv").Length > 0 || dsnp.GetFiles("*.MNV").Length > 0)
                    {
                        IsPSP = true;
                    }
                }

                if (DiscDirectorySSIF != null)
                {
                    var dssif = DiscDirectorySSIF!;
                    if (dssif.GetFiles().Length > 0)
                    {
                        Is3D = true;
                    }
                }

                DiscFileInfo[] discFiles = DiscDirectoryRoot?.GetFiles("FilmIndex.xml") ?? new DiscFileInfo[0];
                if (discFiles.Length > 0)
                {
                    IsDBOX = true;
                }

                if(DiscDirectoryMETA != null)
                {
                    DiscFileInfo[] metaFiles = DiscDirectoryMETA.GetFiles("bdmt_eng.xml", SearchOption.AllDirectories);
                    if (metaFiles != null && metaFiles.Length > 0)
                    {
                        ReadDiscTitle(metaFiles[0].OpenText());
                    }
                }

                //
                // Initialize file lists.
                //

                if (DiscDirectoryPLAYLIST != null)
                {
                    var discPlaylist = DiscDirectoryPLAYLIST!;
                    DiscFileInfo[] files = discPlaylist.GetFiles("*.mpls");
                    if (files.Length == 0)
                    {
                        files = discPlaylist.GetFiles("*.MPLS");
                    }
                    foreach (DiscFileInfo file in files)
                    {
                        PlaylistFiles.Add(file.Name.ToUpper(), new TSPlaylistFile(this, file, CdReader!));
                    }
                }

                if (DiscDirectorySTREAM != null)
                {
                    var discStream = DiscDirectorySTREAM!;
                    DiscFileInfo[] files = discStream.GetFiles("*.m2ts");
                    if (files.Length == 0)
                    {
                        files = DiscDirectoryPLAYLIST?.GetFiles("*.M2TS") ?? new DiscFileInfo[0];
                    }
                    foreach (DiscFileInfo file in files)
                    {
                        StreamFiles.Add(file.Name.ToUpper(), new TSStreamFile(file, CdReader!));
                    }
                }

                if (DiscDirectoryCLIPINF != null)
                {
                    var discClipinf = DiscDirectoryCLIPINF!;
                    DiscFileInfo[] files = discClipinf.GetFiles("*.clpi");
                    if (files.Length == 0)
                    {
                        files = DiscDirectoryPLAYLIST?.GetFiles("*.CLPI") ?? new DiscFileInfo[0];
                    }
                    foreach (DiscFileInfo file in files)
                    {
                        StreamClipFiles.Add(file.Name.ToUpper(), new TSStreamClipFile(file, CdReader!));
                    }
                }

                if (DiscDirectorySSIF != null)
                {
                    var discSsif = DiscDirectorySSIF!;
                    DiscFileInfo[] files = discSsif.GetFiles("*.ssif");
                    if (files.Length == 0)
                    {
                        files = discSsif.GetFiles("*.SSIF");
                    }
                    foreach (DiscFileInfo file in files)
                    {
                        InterleavedFiles.Add(file.Name.ToUpper(), new TSInterleavedFile(file, CdReader!));
                    }
                }
            }
            else
            {
                VolumeLabel = GetVolumeLabel(DirectoryRoot);
                Size = (ulong)GetDirectorySize(DirectoryRoot);

                var indexFiles = DirectoryBDMV!.GetFiles();
                FileInfo? indexFile = null;

                for (int i = 0; i < indexFiles.Length; i++)
                {
                    if (indexFiles[i].Name.ToLower() == "index.bdmv")
                    {
                        indexFile = indexFiles[i];
                        break;
                    }
                }

                if (indexFile != null)
                {
                    using (var indexStream = indexFile.OpenRead())
                    {
                        ReadIndexVersion(indexStream);
                    }
                }

                if (null != GetDirectory("BDSVM", DirectoryRoot, 0))
                {
                    IsBDPlus = true;
                }
                if (null != GetDirectory("SLYVM", DirectoryRoot, 0))
                {
                    IsBDPlus = true;
                }
                if (null != GetDirectory("ANYVM", DirectoryRoot, 0))
                {
                    IsBDPlus = true;
                }

                if (DirectoryBDJO != null)
                {
                    var ddjo = DirectoryBDJO!;
                    if (ddjo.GetFiles().Length > 0)
                    {
                        IsBDJava = true;
                    }
                }

                if (DirectorySNP != null)
                {
                    var dsnp = DirectorySNP!;
                    if (dsnp.GetFiles("*.mnv").Length > 0 || dsnp.GetFiles("*.MNV").Length > 0)
                    {
                        IsPSP = true;
                    }
                }

                if (DirectorySSIF != null)
                {
                    var dssif = DirectorySSIF!;
                    if (dssif.GetFiles().Length > 0)
                    {
                        Is3D = true;
                    }
                }

                if (DirectoryRoot != null && File.Exists(Path.Combine(DirectoryRoot.FullName, "FilmIndex.xml")))
                {
                    IsDBOX = true;
                }

                if (DirectoryMETA != null)
                {
                    FileInfo[] metaFiles = DirectoryMETA.GetFiles("bdmt_eng.xml", SearchOption.AllDirectories);
                    if (metaFiles != null && metaFiles.Length > 0)
                    {
                        ReadDiscTitle(metaFiles[0].OpenText());
                    }
                }

                //
                // Initialize file lists.
                //

                if (DirectoryPLAYLIST != null)
                {
                    var dirPlaylist = DirectoryPLAYLIST!;
                    FileInfo[] files = dirPlaylist.GetFiles("*.mpls");
                    if (files.Length == 0)
                    {
                        files = dirPlaylist.GetFiles("*.MPLS");
                    }
                    foreach (FileInfo file in files)
                    {
                        PlaylistFiles.Add(
                            file.Name.ToUpper(), new TSPlaylistFile(this, file));
                    }
                }

                if (DirectorySTREAM != null)
                {
                    var dirStream = DirectorySTREAM!;
                    FileInfo[] files = dirStream.GetFiles("*.m2ts");
                    if (files.Length == 0)
                    {
                        files = DirectoryPLAYLIST?.GetFiles("*.M2TS") ?? new FileInfo[0];
                    }
                    foreach (FileInfo file in files)
                    {
                        StreamFiles.Add(
                            file.Name.ToUpper(), new TSStreamFile(file));
                    }
                }

                if (DirectoryCLIPINF != null)
                {
                    var dirClipinf = DirectoryCLIPINF!;
                    FileInfo[] files = dirClipinf.GetFiles("*.clpi");
                    if (files.Length == 0)
                    {
                        files = DirectoryPLAYLIST?.GetFiles("*.CLPI") ?? new FileInfo[0];
                    }
                    foreach (FileInfo file in files)
                    {
                        StreamClipFiles.Add(
                            file.Name.ToUpper(), new TSStreamClipFile(file));
                    }
                }

                if (DirectorySSIF != null)
                {
                    var dirSsif = DirectorySSIF!;
                    FileInfo[] files = dirSsif.GetFiles("*.ssif");
                    if (files.Length == 0)
                    {
                        files = dirSsif.GetFiles("*.SSIF");
                    }
                    foreach (FileInfo file in files)
                    {
                        InterleavedFiles.Add(
                            file.Name.ToUpper(), new TSInterleavedFile(file));
                    }
                }
            }
        }

        private void ReadDiscTitle(StreamReader fileStream)
        {
            try
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(fileStream);
                var xNsMgr = new XmlNamespaceManager(xDoc.NameTable);
                xNsMgr.AddNamespace("di", "urn:BDA:bdmv;discinfo");
                var xNode = xDoc.DocumentElement?.SelectSingleNode("di:discinfo/di:title/di:name", xNsMgr);
                DiscTitle = xNode?.InnerText;

                if (!string.IsNullOrEmpty(DiscTitle) && DiscTitle.ToLowerInvariant() == "blu-ray")
                    DiscTitle = null;
            }
            catch (Exception)
            {
                DiscTitle = null;
            }
            finally
            {
                fileStream.Close();
            }

        }

        public void Scan()
        {
            List<TSStreamClipFile> errorStreamClipFiles = new List<TSStreamClipFile>();
            foreach (TSStreamClipFile streamClipFile in StreamClipFiles.Values)
            {
                try
                {
                    streamClipFile.Scan();
                }
                catch (Exception ex)
                {
                    errorStreamClipFiles.Add(streamClipFile);
                    if (StreamClipFileScanError != null)
                    {
                        if (StreamClipFileScanError(streamClipFile, ex))
                        {
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else throw;
                }
            }

            foreach (TSStreamFile streamFile in StreamFiles.Values)
            {
                string ssifName = Path.GetFileNameWithoutExtension(streamFile.Name) + ".SSIF";
                if (InterleavedFiles.ContainsKey(ssifName))
                {
                    streamFile.InterleavedFile = InterleavedFiles[ssifName];
                }
            }

            TSStreamFile[] streamFiles = new TSStreamFile[StreamFiles.Count];
            StreamFiles.Values.CopyTo(streamFiles, 0);
            Array.Sort(streamFiles, CompareStreamFiles);

            List<TSPlaylistFile> errorPlaylistFiles = new List<TSPlaylistFile>();
            foreach (TSPlaylistFile playlistFile in PlaylistFiles.Values)
            {
                try
                {
                    playlistFile.Scan(StreamFiles, StreamClipFiles);
                }
                catch (Exception ex)
                {
                    errorPlaylistFiles.Add(playlistFile);
                    if (PlaylistFileScanError != null)
                    {
                        if (PlaylistFileScanError(playlistFile, ex))
                        {
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else throw;
                }
            }

            List<TSStreamFile> errorStreamFiles = new List<TSStreamFile>();
            foreach (TSStreamFile streamFile in streamFiles)
            {
                try
                {
                    List<TSPlaylistFile> playlists = new List<TSPlaylistFile>();
                    foreach (TSPlaylistFile playlist in PlaylistFiles.Values)
                    {
                        foreach (TSStreamClip streamClip in playlist.StreamClips)
                        {
                            if (streamClip.Name == streamFile.Name)
                            {
                                playlists.Add(playlist);
                                break;
                            }
                        }
                    }
                    streamFile.Scan(playlists, false);
                }
                catch (Exception ex)
                {
                    errorStreamFiles.Add(streamFile);
                    if (StreamFileScanError != null)
                    {
                        if (StreamFileScanError(streamFile, ex))
                        {
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else throw;
                }
            }

            foreach (TSPlaylistFile playlistFile in PlaylistFiles.Values)
            {
                playlistFile.Initialize();
                if (!Is50Hz)
                {
                    int vidStreamCount = playlistFile.VideoStreams.Count;
                    foreach (TSVideoStream videoStream in playlistFile.VideoStreams)
                    {
                        if (videoStream.FrameRate == TSFrameRate.FRAMERATE_25 ||
                            videoStream.FrameRate == TSFrameRate.FRAMERATE_50)
                        {
                            Is50Hz = true;
                        }

                        if (vidStreamCount > 1 && Is3D)
                        {
                            if ((videoStream.StreamType == TSStreamType.AVC_VIDEO && playlistFile.MVCBaseViewR) ||
                                (videoStream.StreamType == TSStreamType.MVC_VIDEO && !playlistFile.MVCBaseViewR))
                                videoStream.BaseView = true;
                            else
                                if (videoStream.StreamType == TSStreamType.AVC_VIDEO || videoStream.StreamType == TSStreamType.MVC_VIDEO)
                                videoStream.BaseView = false;
                        }

                    }
                }
            }
        }

        private DiscDirectoryInfo? GetDiscDirectoryBDMV()
        {
            if (CdReader == null)
                return null;
            var DiscDirInfo = CdReader.GetDirectoryInfo("BDMV");
            return DiscDirInfo;
        }

        private DirectoryInfo? GetDirectoryBDMV(
            string path)
        {
                DirectoryInfo? dir = new DirectoryInfo(path);

                while (dir != null)
                {
                    if (dir.Name == "BDMV")
                    {
                        return dir;
                    }
                    dir = dir.Parent;
                }

                return GetDirectory("BDMV", new DirectoryInfo(path), 0);
        }

        private DirectoryInfo? GetDirectory(string name, DirectoryInfo? dir, int searchDepth)
        {
            if (dir != null)
            {
                DirectoryInfo[] children = dir.GetDirectories();
                foreach (DirectoryInfo child in children)
                {
                    if (child.Name == name)
                    {
                        return child;
                    }
                }
                if (searchDepth > 0)
                {
                    foreach (DirectoryInfo child in children)
                    {
                        var found = GetDirectory(
                            name, child, searchDepth - 1);
                        if (found != null) return found;
                    }
                }
            }
            return null;
        }

        private DiscDirectoryInfo? GetDiscDirectory(string name, DiscDirectoryInfo? dir, int searchDepth)
        {
            if (dir != null)
            {
                DiscDirectoryInfo[] children = dir.GetDirectories();
                foreach (DiscDirectoryInfo child in children)
                {
                    if (child.Name == name)
                    {
                        return child;
                    }
                }
                if (searchDepth > 0)
                {
                    foreach (DiscDirectoryInfo child in children)
                    {
                        var found = GetDiscDirectory(name, child, searchDepth - 1);
                        if (found != null) return found;
                    }
                }
            }
            return null;
        }

        private long GetDirectorySize(DirectoryInfo? directoryInfo)
        {
            long size = 0;

            if (directoryInfo == null)
                return 0;

            FileInfo[] pathFiles = directoryInfo.GetFiles();
            foreach (FileInfo pathFile in pathFiles)
            {
                if (pathFile.Extension.ToUpper() == ".SSIF")
                {
                    continue;
                }
                size += pathFile.Length;
            }

            DirectoryInfo[] pathChildren = directoryInfo.GetDirectories();
            foreach (DirectoryInfo pathChild in pathChildren)
            {
                size += GetDirectorySize(pathChild);
            }
            return size;
        }

        private long GetDiscDirectorySize(DiscDirectoryInfo? directoryInfo)
        {
            long size = 0;
            if (directoryInfo == null)
                return 0;

            DiscFileInfo[] pathFiles = directoryInfo.GetFiles();
            foreach (DiscFileInfo pathFile in pathFiles)
            {
                if (pathFile.Extension.ToUpper() == "SSIF")
                {
                    continue;
                }
                size += pathFile.Length;
            }

            DiscDirectoryInfo[] pathChildren = directoryInfo.GetDirectories();
            foreach (DiscDirectoryInfo pathChild in pathChildren)
            {
                size += GetDiscDirectorySize(pathChild);
            }
            return size;
        }

        private string GetVolumeLabel(DirectoryInfo? dir)
        {
            string label = "";
            if (!IsImage)
            {
                uint serialNumber = 0;
                uint maxLength = 0;
                uint volumeFlags = new uint();
                StringBuilder volumeLabel = new StringBuilder(256);
                StringBuilder fileSystemName = new StringBuilder(256);

                try
                {
                    if (dir != null)
                    {
                        long result = GetVolumeInformation(
                            dir.FullName,
                            volumeLabel,
                            (uint)volumeLabel.Capacity,
                            ref serialNumber,
                            ref maxLength,
                            ref volumeFlags,
                            fileSystemName,
                            (uint)fileSystemName.Capacity);

                        label = volumeLabel.ToString();
                    }
                }
                catch { }
            }
            else
            {
                label = CdReader?.VolumeLabel ?? string.Empty;
            }
            if (label.Length == 0)
            {
                label = dir?.Name ?? string.Empty;
            }

            return label;
        }

        public static int CompareStreamFiles(
            TSStreamFile x,
            TSStreamFile y)
        {
            // TODO: Use interleaved file sizes
            var xInfo = x?.FileInfo;
            var yInfo = y?.FileInfo;

            if (xInfo == null && yInfo == null)
                return 0;
            if (xInfo == null && yInfo != null)
                return 1;
            if (xInfo != null && yInfo == null)
                return -1;

            // both non-null here
            if (xInfo!.Length > yInfo!.Length)
                return 1;
            if (yInfo.Length > xInfo.Length)
                return -1;
            return 0;
        }

        private void ReadIndexVersion(Stream indexStream)
        {
            var buffer = new byte[8];
            int count = indexStream.Read(buffer, 0, 8);
            int pos = 0;
            if (count > 0)
            {
                var indexVer = ToolBox.ReadString(buffer, count, ref pos);
                IsUHD = indexVer == "INDX0300";
            }
        }

        public void CloseDiscImage()
        {
            if (IsImage && CdReader != null)
            {
                CdReader?.Dispose();
                CdReader = null;
                IoStream?.Close();
                IoStream?.Dispose();
                IoStream = null;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern long GetVolumeInformation(
            string PathName,
            StringBuilder VolumeNameBuffer,
            uint VolumeNameSize,
            ref uint VolumeSerialNumber,
            ref uint MaximumComponentLength,
            ref uint FileSystemFlags,
            StringBuilder FileSystemNameBuffer,
            uint FileSystemNameSize);
    }
}
