using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using HarmonyLib;
using MusicBeePlugin.Utils;

namespace MusicBeePlugin.Services
{
    // Installed hooks are inert outside the execution context of a custom action.
    // Internal methods are discovered from signatures and their call relationships.
    internal static class CommandSelectionContext
    {
        private const string PatchId = "MusicBeeSearchBar.CommandSelection";
        private static readonly object gate = new object();
        private static Harmony harmony;
        private static int generation;
        private static Func<IList> newTrackList;
        private static readonly AsyncLocal<Selection> selectionContext = new AsyncLocal<Selection>();
        private static Selection current
        {
            get => selectionContext.Value?.Generation == Volatile.Read(ref generation) ? selectionContext.Value : null;
            set => selectionContext.Value = value;
        }
        private static ConditionalWeakTable<ToolStripDropDown, MenuContext> menus = new ConditionalWeakTable<ToolStripDropDown, MenuContext>();
        private static readonly List<WeakReference> trackedMenus = new List<WeakReference>();

        private sealed class Selection
        {
            public readonly int Generation = Volatile.Read(ref generation);
            public Array Tracks;
            public string[] Files;
        }

        public static IDisposable Enter(Assembly assembly, Array tracks, string[] files)
        {
            var control = Control.FromHandle(Plugin.mbApi.MB_GetWindowHandle());
            if (control == null || control.InvokeRequired)
                throw new FeatureUnavailableException("Command selection", "Custom commands must run on MusicBee's UI thread.");
            EnsurePatched(assembly, tracks.GetType().GetElementType(), control);
            var previous = current;
            current = new Selection { Tracks = tracks, Files = (string[])files.Clone() };
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly Selection previous;
            private readonly int thread = Thread.CurrentThread.ManagedThreadId;
            private bool disposed;
            public Scope(Selection previous) { this.previous = previous; }
            public void Dispose()
            {
                if (disposed) return;
                if (thread != Thread.CurrentThread.ManagedThreadId)
                    throw new InvalidOperationException("Selection scopes must end on their owning thread.");
                current = previous;
                disposed = true;
            }
        }

        private static bool IsSelectionQuery(object mode)
        {
            int value = Convert.ToInt32(mode);
            // Selected tracks; selected-or-displayed; tag-edit selection; play selection.
            // Displayed/source-file queries continue to use MusicBee's normal view.
            return value >= 0 && value <= 3;
        }

        private static object CopyTracks()
        {
            var list = newTrackList();
            foreach (var track in current.Tracks) list.Add(track);
            return list;
        }

        private static bool SelectionPrefix<T>(object __0, ref T __result)
        {
            if (current == null || !IsSelectionQuery(__0)) return true;
            __result = (T)CopyTracks();
            return false;
        }

        private static bool QueryFilesPrefix(string __0, ref string[] __1, ref bool __result)
        {
            if (current == null || !string.Equals(__0, "domain=SelectedFiles", StringComparison.OrdinalIgnoreCase)) return true;
            __1 = (string[])current.Files.Clone();
            __result = true;
            return false;
        }

        private static void PanelSelectionPostfix<T>(object __0, ref T __3, ref bool __result)
        {
            if (current == null || !IsSelectionQuery(__0)) return;
            // Preserve the panel/source context supplied by the original resolver, replacing
            // only its track list. Never change the panel's selected rows or its contents.
            __3 = (T)CopyTracks();
            __result = current.Tracks.Length > 0;
        }

        private static Selection MenuSelection(ToolStripDropDown menu)
        {
            while (menu != null)
            {
                if (menus.TryGetValue(menu, out var context) && context.Selection != null && context.Selection.Generation == Volatile.Read(ref generation)) return context.Selection;
                menu = menu.OwnerItem?.Owner as ToolStripDropDown;
            }
            return null;
        }

        private sealed class MenuContext
        {
            public Selection Selection;
        }

        private static void MenuOpenedPostfix(ToolStripDropDown __instance)
        {
            var selection = current ?? MenuSelection(__instance.OwnerItem?.Owner as ToolStripDropDown);
            if (menus.TryGetValue(__instance, out var previous)) previous.Selection = selection;
            else if (selection != null)
            {
                menus.Add(__instance, new MenuContext { Selection = selection });
                trackedMenus.RemoveAll(reference => !reference.IsAlive);
                trackedMenus.Add(new WeakReference(__instance));
                __instance.Closed += MenuClosed;
                __instance.Disposed += MenuDisposed;
            }
        }

        private static void MenuDisposed(object sender, EventArgs args)
        {
            menus.Remove((ToolStripDropDown)sender);
        }

        private static void MenuClosed(object sender, ToolStripDropDownClosedEventArgs args)
        {
            var menu = (ToolStripDropDown)sender;
            if (!menus.TryGetValue(menu, out var context)) return;
            var selection = context.Selection;
            // WinForms can close the menu before firing the item's Click event.
            // Release its tracks after that stack completes, without erasing a reopening.
            if (!menu.IsHandleCreated || menu.IsDisposed) { context.Selection = null; return; }
            try
            {
                menu.BeginInvoke((Action)(() =>
                {
                    if (!menu.Visible && ReferenceEquals(context.Selection, selection)) context.Selection = null;
                }));
            }
            catch (InvalidOperationException) { context.Selection = null; }
        }

        private static void MenuClickPrefix(ToolStripItem __instance, ref IDisposable __state)
        {
            var selection = MenuSelection(__instance.Owner as ToolStripDropDown);
            if (selection == null) return;
            __state = new Scope(current);
            current = selection;
        }

        private static void MenuClickFinalizer(IDisposable __state)
        {
            __state?.Dispose();
        }

        private static void EnsurePatched(Assembly assembly, Type trackType, Control control)
        {
            lock (gate)
            {
                if (harmony != null) return;
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var binding = Discover(assembly, trackType, control);
                var getter = binding.Getter;
                var resolver = binding.Resolver;
                var apiQuery = binding.ApiQuery;
                var constructor = getter.ReturnType.GetConstructor(Type.EmptyTypes);
                if (constructor == null) throw Unavailable("The internal track list has no default constructor.");
                newTrackList = () => (IList)constructor.Invoke(null);

                var patcher = new Harmony(PatchId);
                try
                {
                    var prefix = typeof(CommandSelectionContext).GetMethod(nameof(SelectionPrefix), flags).MakeGenericMethod(getter.ReturnType);
                    patcher.Patch(getter, prefix: new HarmonyMethod(prefix));
                    var postfix = typeof(CommandSelectionContext).GetMethod(nameof(PanelSelectionPostfix), flags).MakeGenericMethod(getter.ReturnType);
                    patcher.Patch(resolver, postfix: new HarmonyMethod(postfix));
                    patcher.Patch(apiQuery, prefix: new HarmonyMethod(typeof(CommandSelectionContext), nameof(QueryFilesPrefix)));
                    var menuOpened = typeof(ToolStripDropDown).GetMethod("OnOpened", BindingFlags.Instance | BindingFlags.NonPublic);
                    // OnClick is a tiny virtual wrapper and can already be inlined in
                    // ToolStripMenuItem. HandleClick surrounds the full event dispatch
                    // and contains exception handling, avoiding that inlining path.
                    var itemClick = typeof(ToolStripItem).GetMethod("HandleClick", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventArgs) }, null);
                    patcher.Patch(menuOpened, postfix: new HarmonyMethod(typeof(CommandSelectionContext), nameof(MenuOpenedPostfix)));
                    patcher.Patch(itemClick, prefix: new HarmonyMethod(typeof(CommandSelectionContext), nameof(MenuClickPrefix)),
                        finalizer: new HarmonyMethod(typeof(CommandSelectionContext), nameof(MenuClickFinalizer)));
                    harmony = patcher;
                    Logger.Info("Installed temporary command selection hooks.");
                }
                catch
                {
                    patcher.UnpatchAll(PatchId);
                    throw;
                }
            }
        }

        private sealed class HookBinding
        {
            public MethodInfo Getter, Resolver, ApiQuery;
        }

        private static FeatureUnavailableException Unavailable(string reason)
        {
            return new FeatureUnavailableException("Command selection", reason);
        }

        private static MethodInfo Unique(IEnumerable<MethodInfo> candidates, string description)
        {
            var matches = candidates.Distinct().ToArray();
            if (matches.Length != 1) throw Unavailable("Could not uniquely identify " + description + ".");
            return matches[0];
        }

        private static bool IsTrackList(Type type, Type trackType)
        {
            return typeof(IList).IsAssignableFrom(type) && type.IsGenericType
                && type.GetGenericArguments().Length == 1 && type.GetGenericArguments()[0] == trackType;
        }

        private static bool IsResolver(MethodInfo method, Type listType)
        {
            var p = method.GetParameters();
            return method.ReturnType == typeof(bool) && p.Length == 4 && p[0].ParameterType.IsEnum
                && p[1].ParameterType.IsByRef && p[2].ParameterType.IsByRef
                && p[3].ParameterType == listType.MakeByRefType();
        }

        private static HookBinding Discover(Assembly assembly, Type trackType, Control control)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            // The plugin API's real managed target may survive delegate marshalling.
            // Otherwise find the string/array API wrapper that calls the library query
            // containing the SelectedFiles domain. All candidates are inspected, never run.
            var api = Plugin.mbApi.Library_QueryFilesEx.Method;
            if (api.DeclaringType?.Assembly != assembly)
            {
                api = Unique(assembly.GetTypes().SelectMany(t => t.GetMethods(all)).Where(m =>
                {
                    var p = m.GetParameters();
                    if (m.IsStatic || m.ReturnType != typeof(bool) || p.Length != 2
                        || p[0].ParameterType != typeof(string) || p[1].ParameterType != typeof(string[]).MakeByRefType()) return false;
                    return InternalMethodReader.Calls(m).Any(c => IsTrackList(c.ReturnType, trackType)
                        && InternalMethodReader.HasString(c, "domain=SelectedFiles"));
                }), "MusicBee's selected-files API");
            }
            // Follow the API's query implementation, including its compiler-generated
            // lambda methods, to the static selection getter used by SelectedFiles.
            var apiTypes = new[] { api.DeclaringType }.Concat(api.DeclaringType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));
            var getter = Unique(apiTypes.SelectMany(t => t.GetMethods(all)).SelectMany(InternalMethodReader.Calls).Where(m =>
            {
                var p = m.GetParameters();
                return m.IsStatic && IsTrackList(m.ReturnType, trackType) && p.Length == 1 && p[0].ParameterType.IsEnum
                    && InternalMethodReader.Calls(m).Any(c => c.DeclaringType.IsInterface && IsResolver(c, m.ReturnType));
            }), "MusicBee's selection getter");
            var contract = Unique(InternalMethodReader.Calls(getter).Where(m => m.DeclaringType.IsInterface && IsResolver(m, getter.ReturnType)), "the selection-provider contract");
            if (!contract.DeclaringType.IsAssignableFrom(control.GetType())) throw Unavailable("MusicBee's main window does not implement its selection contract.");
            var map = control.GetType().GetInterfaceMap(contract.DeclaringType);
            int index = Array.IndexOf(map.InterfaceMethods, contract);
            if (index < 0) throw Unavailable("The main-window selection provider was not found.");
            var provider = map.TargetMethods[index];
            var directCalls = InternalMethodReader.Calls(provider).Where(m => IsResolver(m, getter.ReturnType)).ToArray();
            // Explicit interface implementations often forward through an instance
            // wrapper before reaching the shared static resolver.
            var resolver = Unique(directCalls.Where(m => m.IsStatic).Concat(directCalls.Where(m => !m.IsStatic)
                .SelectMany(InternalMethodReader.Calls).Where(m => m.IsStatic && IsResolver(m, getter.ReturnType))), "the main-window selection resolver");
            return new HookBinding { Getter = getter, Resolver = resolver, ApiQuery = api };
        }

        public static void Shutdown()
        {
            lock (gate)
            {
                try { harmony?.UnpatchAll(PatchId); }
                catch (Exception ex) { Logger.Error("Could not remove command selection hooks", ex); }
                harmony = null;
                Interlocked.Increment(ref generation);
                selectionContext.Value = null;
                foreach (var reference in trackedMenus)
                {
                    if (!(reference.Target is ToolStripDropDown menu) || menu.IsDisposed) continue;
                    menu.Closed -= MenuClosed;
                    menu.Disposed -= MenuDisposed;
                }
                trackedMenus.Clear();
                menus = new ConditionalWeakTable<ToolStripDropDown, MenuContext>();
            }
        }
    }
}
