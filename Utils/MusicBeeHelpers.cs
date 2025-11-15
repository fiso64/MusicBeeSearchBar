
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
using System.IO;

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
                Console.WriteLine("Modern Search Bar: Could not find MusicBee's internal InvokeApplicationCommand method.");
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
                    Console.WriteLine($"Modern Search Bar: Command '{command}' from plugin enum not found in this version of MusicBee. It will be unavailable.");
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
                Console.WriteLine("Modern Search Bar: Attempted to call InvokeCommand, but the method was not found.");
                return;
            }

            if (applicationCommandMap.TryGetValue(command, out var mappedCommand))
            {
                invokeApplicationCommandMethod.Invoke(null, new object[] { mappedCommand, parameters, null });
            }
            else
            {
                Console.WriteLine($"Modern Search Bar: Command '{command}' is not available in this version of MusicBee.");
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


        // A cache for the MethodInfo so we don't have to perform the expensive search every time.
        private static MethodInfo _openArtistInMusicExplorerMethod;
        private static bool _searchPerformed = false;

        public static bool OpenArtistInMusicExplorer(string artistName)
        {
            try
            {
                if (!_searchPerformed)
                {
                    _openArtistInMusicExplorerMethod = FindOpenArtistMethod();
                    _searchPerformed = true;
                }

                if (_openArtistInMusicExplorerMethod != null)
                {
                    _openArtistInMusicExplorerMethod.Invoke(null, new object[] { artistName });
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MusicBeeInternals] Failed to call internal function: " + ex.Message);
            }

            return false;
        }

        private static MethodInfo FindOpenArtistMethod()
        {
            var musicBeeAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MusicBee");
            if (musicBeeAssembly == null) return null;

            var targetType = FindMainMusicBeeType(musicBeeAssembly);
            if (targetType == null) return null;

            // Find the specific method by scanning its IL for a unique string literal, as its name is obfuscated.
            const string ArtistUrlFingerprint = "artist://";

            var candidateMethods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m =>
                    m.ReturnType == typeof(void) &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(string)
                );

            foreach (var method in candidateMethods)
            {
                try
                {
                    var methodBody = method.GetMethodBody();
                    if (methodBody == null) continue;

                    if (ContainsStringLiteral(methodBody.GetILAsByteArray(), method.Module, ArtistUrlFingerprint))
                    {
                        return method; // yay
                    }
                }
                catch { continue; }
            }

            return null;
        }

        private static Type FindMainMusicBeeType(Assembly assembly)
        {
            // These interfaces are used by the main Form/Control and are stable framework types.
            Type iMessageFilter = typeof(IMessageFilter);
            Type iContainerControl = typeof(IContainerControl);
            Type iDropTarget = typeof(IDropTarget);

            // This interface is internal to WinForms, so we must resolve it at runtime via reflection.
            Assembly winformsAssembly = typeof(Form).Assembly;
            Type iOleObject = winformsAssembly.GetType("System.Windows.Forms.UnsafeNativeMethods+IOleObject");

            if (iOleObject == null) return null; // Defensive check for future .NET changes.

            try
            {
                // This query combines multiple criteria to uniquely identify the target class.
                return assembly.GetTypes().FirstOrDefault(t =>
                    // Criterion 1: Must be a top-level class, not a nested helper class.
                    t.IsClass && !t.IsNested &&

                    // Criterion 2: Must implement the specific combination of low-level interfaces.
                    iMessageFilter.IsAssignableFrom(t) &&
                    iContainerControl.IsAssignableFrom(t) &&
                    iOleObject.IsAssignableFrom(t) &&
                    iDropTarget.IsAssignableFrom(t) &&

                    // Criterion 3: Must possess a unique "method constellation" to distinguish it
                    // from other components with similar interfaces.
                    t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Any(m => m.ReturnType == typeof(void) && // this is the method we actually want
                                  m.GetParameters().Length == 1 &&
                                  m.GetParameters()[0].ParameterType == typeof(string)) &&
                    t.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                        .Any(m => m.ReturnType == typeof(void) && // some random other method to uniquely identify the class
                                  m.GetParameters().Length == 2 &&
                                  m.GetParameters()[0].ParameterType.IsInterface &&
                                  m.GetParameters()[1].ParameterType.IsEnum)
                );
            }
            catch { return null; }
        }

        private static bool ContainsStringLiteral(byte[] ilBytes, Module module, string literal)
        {
            for (int i = 0; i < ilBytes.Length; i++)
            {
                if (ilBytes[i] == 0x72) // OpCodes.Ldstr
                {
                    if (i + 4 < ilBytes.Length)
                    {
                        int metadataToken = BitConverter.ToInt32(ilBytes, i + 1);
                        try
                        {
                            if (module.ResolveString(metadataToken).Contains(literal))
                            {
                                return true;
                            }
                        }
                        catch { /* Ignore invalid tokens */ }
                    }
                }
            }
            return false;
        }
    }
}
