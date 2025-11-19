using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MusicBeePlugin.Utils;

namespace MusicBeePlugin.Services
{
    public class FeatureUnavailableException : Exception
    {
        public FeatureUnavailableException(string featureName, string reason)
            : base($"Feature '{featureName}' is unavailable.\nReason: {reason}") { }
    }

    public class ReflectionService
    {
        private static ReflectionService _instance;
        public static ReflectionService Instance => _instance ?? (_instance = new ReflectionService());

        private MethodInfo _invokeCommandMethod;
        private Type _mbApplicationCommandType;
        private Dictionary<ApplicationCommand, object> _commandMap;
        private bool _invokeCommandTried;

        private FieldInfo _pluginCommandsField;
        private bool _pluginCommandsTried;

        private MethodInfo _openArtistMethod;
        private bool _openArtistTried;

        private ReflectionService() { }

        private void EnsureInvokeCommandLoaded()
        {
            if (_invokeCommandTried) return;
            _invokeCommandTried = true;

            try
            {
                var flags = BindingFlags.Public | BindingFlags.Static;
                var mbAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains("MusicBee"));
                if (mbAsm == null) return;

                foreach (var refType in mbAsm.GetTypes())
                {
                    var m = refType.GetMethods(flags).FirstOrDefault(x =>
                    {
                        var parameters = x.GetParameters();
                        return parameters.Length == 3
                            && parameters[0].ParameterType.IsEnum
                            && parameters[0].ParameterType.Name == "ApplicationCommand"
                            && parameters[1].ParameterType == typeof(object)
                            && parameters[2].ParameterType.IsGenericType
                            && parameters[2].ParameterType.GetGenericTypeDefinition() == typeof(IList<>);
                    });

                    if (m != null)
                    {
                        _invokeCommandMethod = m;
                        _mbApplicationCommandType = m.GetParameters()[0].ParameterType;
                        break;
                    }
                }

                if (_invokeCommandMethod != null)
                {
                    _commandMap = new Dictionary<ApplicationCommand, object>();
                    foreach (ApplicationCommand command in Enum.GetValues(typeof(ApplicationCommand)))
                    {
                        try
                        {
                            object mbCommandValue = Enum.Parse(_mbApplicationCommandType, command.ToString());
                            if (mbCommandValue != null)
                                _commandMap[command] = mbCommandValue;
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading InvokeCommand: " + ex);
            }
        }

        public void InvokeCommand(ApplicationCommand command, object parameter = null)
        {
            if (command == ApplicationCommand.None) return;

            EnsureInvokeCommandLoaded();

            if (_invokeCommandMethod == null)
            {
                throw new FeatureUnavailableException("Invoke Internal Command",
                    "Could not find the internal MusicBee method for processing commands. This feature depends on internal MusicBee implementation which might have changed.");
            }

            if (_commandMap != null && _commandMap.TryGetValue(command, out var mappedCommand))
            {
                try
                {
                    _invokeCommandMethod.Invoke(null, new object[] { mappedCommand, parameter, null });
                }
                catch (Exception ex)
                {
                    throw new FeatureUnavailableException($"Invoke Command: {command}", $"Error executing command: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
            else
            {
                throw new FeatureUnavailableException($"Invoke Command: {command}",
                    $"The command '{command}' could not be mapped to the internal MusicBee command enumeration.");
            }
        }

        public void InvokePluginCommand(string commandName)
        {
            EnsureInvokeCommandLoaded();
            if (_invokeCommandMethod == null)
                throw new FeatureUnavailableException("Invoke Plugin Command", "Internal command processor not found.");

            try
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(commandName);
                object enumValue = Enum.ToObject(_mbApplicationCommandType, hash);
                _invokeCommandMethod.Invoke(null, new object[] { enumValue, null, null });
            }
            catch (Exception ex)
            {
                throw new FeatureUnavailableException($"Invoke Plugin Command: {commandName}", $"Error executing plugin command: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void EnsurePluginCommandsLoaded()
        {
            if (_pluginCommandsTried) return;
            _pluginCommandsTried = true;

            try
            {
                var flags = BindingFlags.Public | BindingFlags.Static;
                var mbAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.Contains("MusicBee"));
                if (mbAsm == null) return;

                foreach (var refType in mbAsm.GetTypes())
                {
                    var f = refType.GetFields(flags).FirstOrDefault(x =>
                        x.IsStatic && x.FieldType.IsGenericType
                        && x.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                        && x.FieldType.GenericTypeArguments.Length == 1
                        && x.FieldType.GenericTypeArguments[0].IsGenericType
                        && x.FieldType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
                        && x.FieldType.GenericTypeArguments[0].GenericTypeArguments[0] == typeof(string)
                        && x.FieldType.GenericTypeArguments[0].GenericTypeArguments[1] == typeof(EventHandler)
                    );

                    if (f != null)
                    {
                        _pluginCommandsField = f;
                        break;
                    }
                }
            }
            catch { }
        }

        public List<KeyValuePair<string, EventHandler>> GetPluginCommands()
        {
            EnsurePluginCommandsLoaded();
            if (_pluginCommandsField == null) return new List<KeyValuePair<string, EventHandler>>();

            try
            {
                return (List<KeyValuePair<string, EventHandler>>)_pluginCommandsField.GetValue(null);
            }
            catch
            {
                return new List<KeyValuePair<string, EventHandler>>();
            }
        }

        private void EnsureOpenArtistMethodLoaded()
        {
            if (_openArtistTried) return;
            _openArtistTried = true;

            _openArtistMethod = FindOpenArtistMethod();
        }

        public void OpenArtistInMusicExplorer(string artistName)
        {
            EnsureOpenArtistMethodLoaded();

            if (_openArtistMethod == null)
            {
                throw new FeatureUnavailableException("Open in Music Explorer",
                    "Could not locate the internal 'ShowMusicExplorer' method. This feature is version-dependent and may not be available.");
            }

            try
            {
                _openArtistMethod.Invoke(null, new object[] { artistName });
            }
            catch (Exception ex)
            {
                throw new FeatureUnavailableException("Open in Music Explorer", $"Error invoking method: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private MethodInfo FindOpenArtistMethod()
        {
            var musicBeeAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MusicBee");
            if (musicBeeAssembly == null) return null;

            var targetType = FindMainMusicBeeType(musicBeeAssembly);
            if (targetType == null) return null;

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
                        return method;
                    }
                }
                catch { continue; }
            }

            return null;
        }

        private Type FindMainMusicBeeType(Assembly assembly)
        {
            Type iMessageFilter = typeof(IMessageFilter);
            Type iContainerControl = typeof(IContainerControl);
            Type iDropTarget = typeof(IDropTarget);

            Assembly winformsAssembly = typeof(Form).Assembly;
            Type iOleObject = winformsAssembly.GetType("System.Windows.Forms.UnsafeNativeMethods+IOleObject");

            if (iOleObject == null) return null;

            try
            {
                return assembly.GetTypes().FirstOrDefault(t =>
                    t.IsClass && !t.IsNested &&
                    iMessageFilter.IsAssignableFrom(t) &&
                    iContainerControl.IsAssignableFrom(t) &&
                    iOleObject.IsAssignableFrom(t) &&
                    iDropTarget.IsAssignableFrom(t) &&
                    t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Any(m => m.ReturnType == typeof(void) &&
                                  m.GetParameters().Length == 1 &&
                                  m.GetParameters()[0].ParameterType == typeof(string)) &&
                    t.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                        .Any(m => m.ReturnType == typeof(void) &&
                                  m.GetParameters().Length == 2 &&
                                  m.GetParameters()[0].ParameterType.IsInterface &&
                                  m.GetParameters()[1].ParameterType.IsEnum)
                );
            }
            catch { return null; }
        }

        private bool ContainsStringLiteral(byte[] ilBytes, Module module, string literal)
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
                        catch { }
                    }
                }
            }
            return false;
        }
    }
}