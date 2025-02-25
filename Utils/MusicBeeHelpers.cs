
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using static MusicBeePlugin.Plugin;
using System.Linq.Expressions;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace MusicBeePlugin.Utils
{
    public static partial class MusicBeeHelpers
    {
        private static MethodInfo invokeApplicationCommandMethod;
        private static bool LoadedMethods = false;

        public static void LoadMethods()
        {
            var flags = BindingFlags.Public | BindingFlags.Static;

            var mbAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains("MusicBee"));

            foreach (var refType in mbAsm.GetTypes())
            {
                invokeApplicationCommandMethod = refType.GetMethods(flags).FirstOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 3
                        && parameters[0].ParameterType.Name == "ApplicationCommand"
                        && parameters[1].ParameterType == typeof(object)
                        && parameters[2].ParameterType.IsGenericType
                        && parameters[2].ParameterType.GetGenericTypeDefinition() == typeof(IList<>);
                });

                if (invokeApplicationCommandMethod != null)
                    break;
            }

            LoadedMethods = true;
        }

        public static void InvokeCommand(ApplicationCommand command, object parameters = null)
        {
            if (command == ApplicationCommand.None)
                return;
            if (!LoadedMethods)
                LoadMethods();
            if (invokeApplicationCommandMethod == null)
                throw new ArgumentNullException("ApplicationCommand method not found.");

            invokeApplicationCommandMethod.Invoke(null, new object[] { command, parameters, null });
        }

        public static void InvokePluginCommandByName(string command)
        {
            if (!LoadedMethods)
                LoadMethods();
            if (invokeApplicationCommandMethod == null)
                throw new ArgumentNullException("ApplicationCommand method not found.");

            int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(command);
            hash = hash < 0 ? hash : -hash;
            invokeApplicationCommandMethod.Invoke(null, new object[] { (ApplicationCommand)hash, null, null });
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

            //var hwnd = mbApi.MB_GetWindowHandle();
            //var windowRect = WinApiHelpers.GetWindowRect(hwnd);
            //int windowWidth = windowRect.Width;

            //WinApiHelpers.SendShiftTab();
            //Thread.Sleep(10);

            //var focusedControl = WinApiHelpers.GetFocus();
            //var controlRect = WinApiHelpers.GetWindowRect(focusedControl);

            //if (controlRect.X > windowWidth / 2)
            //{
            //    WinApiHelpers.SendShiftTab();
            //}
        }

        public static void FocusLeftSidebar()
        {
            FocusSearchBox();
            Thread.Sleep(50);
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
    }
}
