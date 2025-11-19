using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using System.IO;
using MusicBeePlugin.Services;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.Utils
{
    public static partial class MusicBeeHelpers
    {
        public static void LoadInvokeCommandMethod()
        {
            // No-op: handled lazily by ReflectionService
        }

        public static void InvokeCommand(ApplicationCommand command, object parameters = null)
        {
            try
            {
                ReflectionService.Instance.InvokeCommand(command, parameters);
            }
            catch (FeatureUnavailableException ex)
            {
                MessageBox.Show(ex.Message, "Modern Search Bar: Feature Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static void InvokePluginCommandByName(string command)
        {
            try
            {
                ReflectionService.Instance.InvokePluginCommand(command);
            }
            catch (FeatureUnavailableException ex)
            {
                MessageBox.Show(ex.Message, "Modern Search Bar: Feature Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static List<KeyValuePair<string, EventHandler>> GetPluginCommands()
        {
            return ReflectionService.Instance.GetPluginCommands();
        }

        public static IntPtr FocusSearchBox()
        {
            InvokeCommand(ApplicationCommand.GeneralGotoSearch);
            if (!WinApiHelpers.IsEditFocused())
            {
                InvokeCommand(ApplicationCommand.GeneralGotoSearch);
            }
            return WinApiHelpers.GetFocus();
        }

        public static void FocusMainPanel()
        {
            InvokeCommand(ApplicationCommand.GeneralGotoSearch);

            if (WinApiHelpers.IsEditFocused())
                InvokeCommand(ApplicationCommand.GeneralGotoSearch);
        }

        public static void FocusLeftSidebar()
        {
            FocusSearchBox();
            System.Threading.Thread.Sleep(50);
            WinApiHelpers.SendKey(Keys.Tab);
        }

        public static void MinimiseRestoreOrFocus()
        {
            IntPtr hwnd = mbApi.MB_GetWindowHandle();
            var state = WinApiHelpers.WinGetMinMax(hwnd);
            if (state == WinApiHelpers.WindowState.None && !WinApiHelpers.GetForegroundWindow().Equals(hwnd))
            {
                WinApiHelpers.SetForegroundWindow(hwnd);
            }
            else
            {
                InvokeCommand(ApplicationCommand.ViewApplicationMinimise);
            }
        }

        public static string ConstructLibraryQuery(params (MetaDataType Field, ComparisonType Comparison, string Value)[] conditions)
        {
            var dict = new Dictionary<MetaDataType, string>()
            {
                { MetaDataType.Artist, "ArtistPeople" },
                { MetaDataType.Album, "Album" },
                { MetaDataType.AlbumArtist, "AlbumArtist" },
                { MetaDataType.TrackTitle, "Title" },
                { MetaDataType.Comment, "Comment" },
            };

            var query = new XElement("SmartPlaylist",
                new XElement("Source",
                    new XAttribute("Type", 1),
                    new XElement("Conditions",
                        new XAttribute("CombineMethod", "All"),
                        conditions.Select(c => new XElement("Condition",
                            new XAttribute("Field", dict[c.Field]),
                            new XAttribute("Comparison", c.Comparison.ToString()),
                            new XAttribute("Value", c.Value)
                        ))
                    )
                )
            );

            return query.ToString(SaveOptions.DisableFormatting);
        }

        public static MetaDataType GetTagTypeByName(string name)
        {
            foreach (var tag in Enum.GetValues(typeof(MetaDataType)))
            {
                if (mbApi.Setting_GetFieldName((MetaDataType)tag) == name)
                    return (MetaDataType)tag;
            }

            throw new Exception($"Tag not found: {name}");
        }

        public static void SetTag(MetaDataType tag, string value, string[] files)
        {
            if (files == null || files.Length == 0)
                return;

            foreach (var file in files)
            {
                mbApi.Library_SetFileTag(file, tag, value);
                mbApi.Library_CommitTagsToFile(file);
            }
        }

        public static void SetTag(MetaDataType tag, string value, string file)
        {
            SetTag(tag, value, new string[] { file });
        }

        public static void SetTags(string file, params (string tagName, string value)[] tagPairs)
        {
            foreach (var (tagName, value) in tagPairs)
            {
                var tag = GetTagTypeByName(tagName);
                SetTag(tag, value, new string[] { file });
            }
        }

        public static void SetTags(string[] files, params (string tagName, string value)[] tagPairs)
        {
            foreach (var (tagName, value) in tagPairs)
            {
                var tag = GetTagTypeByName(tagName);
                SetTag(tag, value, files);
            }
        }

        public static void SetTagsSelected(params (string tagName, string value)[] tagPairs)
        {
            mbApi.Library_QueryFilesEx("domain=SelectedFiles", out string[] files);

            foreach (var (tagName, value) in tagPairs)
            {
                var tag = GetTagTypeByName(tagName);
                SetTag(tag, value, files);
            }
        }

        public static (string artist, string title, string album, string albumArtist, string path) GetFirstSelectedTrack()
        {
            mbApi.Library_QueryFilesEx("domain=SelectedFiles", out string[] files);

            if (files == null || files.Length == 0)
            {
                return default;
            }

            string artist = mbApi.Library_GetFileTag(files[0], MetaDataType.Artist);
            string title = mbApi.Library_GetFileTag(files[0], MetaDataType.TrackTitle);
            string album = mbApi.Library_GetFileTag(files[0], MetaDataType.Album);
            string albumArtist = mbApi.Library_GetFileTag(files[0], MetaDataType.AlbumArtist);
            return (artist, title, album, albumArtist, files[0]);
        }

        public static string GenerateAlbumCachePath(string albumFolderPath)
        {
            // musicbee's hashing function always has a trailing slash in the paths
            if (!albumFolderPath.EndsWith("\\"))
                albumFolderPath += "\\";

            int firstBackslashIndex = albumFolderPath.IndexOf('\\');
            string processedPath = albumFolderPath.Substring(firstBackslashIndex + 1);
            int albumHashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(processedPath);

            string subfolder = Math.Abs(albumHashCode % 10).ToString();
            string albumHash = albumHashCode.ToString("X");

            return Path.Combine(subfolder, albumHash);
        }

        public static string GenerateSourceFileHash(string sourceFilename)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(sourceFilename).ToString("X");
        }

        public static string GenerateFullCacheKey(string albumFolderPath, string sourceFilename)
        {
            return $"{GenerateAlbumCachePath(albumFolderPath)}_{GenerateSourceFileHash(sourceFilename)}";
        }

        public static bool OpenArtistInMusicExplorer(string artistName)
        {
            try
            {
                ReflectionService.Instance.OpenArtistInMusicExplorer(artistName);
                return true;
            }
            catch (FeatureUnavailableException ex)
            {
                MessageBox.Show(ex.Message, "Modern Search Bar: Feature Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }
    }
}