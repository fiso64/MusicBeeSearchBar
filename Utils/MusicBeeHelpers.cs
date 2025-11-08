
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
        private static Type musicBeeApplicationCommandType;
        private static Dictionary<ApplicationCommand, object> applicationCommandMap;
        private static FieldInfo pluginCommandsField;
        private static bool loadedInvokeCommandMethod = false;
        private static bool loadedPluginCommandsField = false;

        public static void LoadInvokeCommandMethod()
        {
            if (loadedInvokeCommandMethod) return;

            var flags = BindingFlags.Public | BindingFlags.Static;

            var mbAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains("MusicBee"));

            if (mbAsm != null)
            {
                foreach (var refType in mbAsm.GetTypes())
                {
                    invokeApplicationCommandMethod = refType.GetMethods(flags).FirstOrDefault(m =>
                    {
                        var parameters = m.GetParameters();
                        return parameters.Length == 3
                            && parameters[0].ParameterType.IsEnum
                            && parameters[0].ParameterType.Name == "ApplicationCommand"
                            && parameters[1].ParameterType == typeof(object)
                            && parameters[2].ParameterType.IsGenericType
                            && parameters[2].ParameterType.GetGenericTypeDefinition() == typeof(IList<>);
                    });

                    if (invokeApplicationCommandMethod != null)
                        break;
                }
            }


            if (invokeApplicationCommandMethod == null)
            {
                Debug.WriteLine("Modern Search Bar: Could not find MusicBee's internal InvokeApplicationCommand method.");
                loadedInvokeCommandMethod = true; // Mark as "tried" to prevent re-running
                return;
            }

            musicBeeApplicationCommandType = invokeApplicationCommandMethod.GetParameters()[0].ParameterType;
            applicationCommandMap = new Dictionary<ApplicationCommand, object>();

            foreach (ApplicationCommand command in Enum.GetValues(typeof(ApplicationCommand)))
            {
                try
                {
                    object mbCommandValue = Enum.Parse(musicBeeApplicationCommandType, command.ToString());
                    if (mbCommandValue != null)
                    {
                        applicationCommandMap[command] = mbCommandValue;
                    }
                }
                catch (ArgumentException)
                {
                    Debug.WriteLine($"Modern Search Bar: Command '{command}' from plugin enum not found in this version of MusicBee. It will be unavailable.");
                }
            }

            loadedInvokeCommandMethod = true;
        }

        private static void LoadPluginCommandsField()
        {
            var flags = BindingFlags.Public | BindingFlags.Static;

            var mbAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains("MusicBee"));

            foreach (var refType in mbAsm.GetTypes())
            {
                pluginCommandsField = refType.GetFields(flags).FirstOrDefault(f =>
                    f.IsStatic && f.FieldType.IsGenericType
                    && f.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                    && f.FieldType.GenericTypeArguments.Length == 1
                    && f.FieldType.GenericTypeArguments[0].IsGenericType
                    && f.FieldType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
                    && f.FieldType.GenericTypeArguments[0].GenericTypeArguments[0] == typeof(string)
                    && f.FieldType.GenericTypeArguments[0].GenericTypeArguments[1] == typeof(EventHandler)
                );

                if (pluginCommandsField != null)
                    break;
            }

            loadedPluginCommandsField = true;
        }

        public static void InvokeCommand(ApplicationCommand command, object parameters = null)
        {
            if (command == ApplicationCommand.None)
                return;
            if (!loadedInvokeCommandMethod)
                LoadInvokeCommandMethod();
            
            if (invokeApplicationCommandMethod == null)
            {
                Debug.WriteLine("Modern Search Bar: Attempted to call InvokeCommand, but the method was not found.");
                return;
            }

            if (applicationCommandMap.TryGetValue(command, out var mappedCommand))
            {
                invokeApplicationCommandMethod.Invoke(null, new object[] { mappedCommand, parameters, null });
            }
            else
            {
                Debug.WriteLine($"Modern Search Bar: Command '{command}' is not available in this version of MusicBee.");
            }
        }

        public static void InvokePluginCommandByName(string command)
        {
            if (!loadedInvokeCommandMethod)
                LoadInvokeCommandMethod();
            if (invokeApplicationCommandMethod == null)
                throw new ArgumentNullException("ApplicationCommand method not found.");

            int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(command);
            hash = hash < 0 ? hash : -hash;
            invokeApplicationCommandMethod.Invoke(null, new object[] { (ApplicationCommand)hash, null, null });
        }

        // The commands can be invoked by calling handler(musicBeeApiInterface, EventArgs.Empty)
        public static List<KeyValuePair<string, EventHandler>> GetPluginCommands()
        {
            if (!loadedPluginCommandsField)
                LoadPluginCommandsField();
            if (pluginCommandsField == null)
                throw new ArgumentNullException("Plugin Commands field not found.");

            return (List<KeyValuePair<string, EventHandler>>)pluginCommandsField.GetValue(null);
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
