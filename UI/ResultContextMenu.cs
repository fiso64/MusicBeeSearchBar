using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using Newtonsoft.Json;

namespace MusicBeePlugin.UI
{
    internal static class ResultContextMenu
    {
        private sealed class FloatingMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly Theme theme;

            public FloatingMenuRenderer(Theme theme)
            {
                this.theme = theme;
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var brush = new SolidBrush(theme.Background))
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                var bounds = e.ToolStrip.ClientRectangle;
                if (bounds.Width <= 1 || bounds.Height <= 1) return;
                using (var pen = new Pen(theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var color = e.Item.Selected || e.Item.Pressed ? theme.Highlight : theme.Background;
                using (var brush = new SolidBrush(color))
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? theme.Foreground : theme.SecondaryText;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using (var pen = new Pen(theme.Border))
                    e.Graphics.DrawLine(pen, 6, e.Item.Height / 2, e.Item.Width - 6, e.Item.Height / 2);
            }
        }

        public static void ApplyFloatingTheme(ContextMenuStrip menu, Theme theme)
        {
            menu.BackColor = theme.Background;
            menu.ForeColor = theme.Foreground;
            menu.ShowImageMargin = false;
            menu.Renderer = new FloatingMenuRenderer(theme);
        }

        private static string TabLabel(TabChoice tab)
        {
            if (tab == TabChoice.CurrentTab) return "current tab";
            if (tab == TabChoice.NewTab) return "new tab";
            return "tab " + tab.ToString().Substring(3);
        }

        private static string ActionLabel(BaseActionData action)
        {
            if (action is SearchInTabActionData search) return "Search in " + TabLabel(search.TabChoice);
            if (action is OpenFilterInTabActionData filter) return "Open filter in " + TabLabel(filter.TabChoice);
            if (action is OpenPlaylistInTabActionData playlist) return "Open playlist in " + TabLabel(playlist.TabChoice);
            if (action is OpenInMusicExplorerInTabActionData explorer) return "Open in music explorer in " + TabLabel(explorer.TabChoice);
            if (action is OpenInMusicExplorerActionData) return "Open in music explorer";
            if (action is EditTagsActionData) return "Edit tags";
            if (action is CommandActionData command) return command.DisplayName ?? command.PluginCommandName ?? command.Command?.ToString();
            if (action is QueueNextActionData next)
            {
                var options = new List<string>();
                if (next.ShufflePlay) options.Add("shuffled");
                if (next.ClearQueueBeforeAdd) options.Add("clear queue");
                return "Queue next" + (options.Count == 0 ? "" : " (" + string.Join(", ", options) + ")");
            }
            if (action is QueueLastActionData last && last.ShufflePlay) return "Queue last (shuffled)";
            return null;
        }

        private static bool IsStandardAction(BaseActionData action, ResultType type)
        {
            if (action is ShufflePlayActionData) return true;
            if (action is PlayActionData) return true;
            if (action is QueueNextActionData next)
                return !next.ClearQueueBeforeAdd && (!next.ShufflePlay || type == ResultType.Song);
            if (action is QueueLastActionData last)
                return !last.ShufflePlay || type == ResultType.Song;
            return false;
        }

        public static ContextMenuStrip Create(SearchResult result, ActionConfig config,
            Action<ResultActionType> runQuickAction, Action runShuffle, Action<Keys> runConfigured,
            Action runCommand)
        {
            var menu = new ContextMenuStrip();
            if (result.Type == ResultType.Command)
            {
                menu.Items.Add("Run command", null, (s, e) => runCommand());
                return menu;
            }
            if (!ActionService.SupportsQuickActions(result.Type)) return menu;

            menu.Items.Add("Play now", null, (s, e) => runQuickAction(ResultActionType.PlayNow));
            menu.Items.Add("Queue next", null, (s, e) => runQuickAction(ResultActionType.QueueNext));
            menu.Items.Add("Queue last", null, (s, e) => runQuickAction(ResultActionType.QueueLast));
            if (result.Type != ResultType.Song)
                menu.Items.Add("Shuffle play", null, (s, e) => runShuffle());

            var configured = new List<(BaseActionData Action, Keys Modifiers, string Label)>();
            var seen = new HashSet<string>();
            if (config != null)
            {
                var choices = new List<(BaseActionData, Keys)> { (config.Default, Keys.Enter) };
                if (config.Shortcuts != null)
                    choices.AddRange(config.Shortcuts.Select(x => (x.Action, x.Key)));
                foreach (var choice in choices)
                {
                    var action = choice.Item1;
                    if (action == null || IsStandardAction(action, result.Type)) continue;
                    string label = ActionLabel(action);
                    if (label == null) continue;
                    string key = action.GetType().FullName + ":" + JsonConvert.SerializeObject(action);
                    if (seen.Add(key)) configured.Add((action, choice.Item2, label));
                }
            }

            if (configured.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                foreach (var choice in configured)
                {
                    string label = choice.Label;
                    if (configured.Count(x => x.Label == label) > 1)
                    {
                        string shortcut = choice.Modifiers == Keys.Enter ? "default" :
                            new KeysConverter().ConvertToString(choice.Modifiers);
                        label += " (" + shortcut + ")";
                    }
                    var modifiers = choice.Modifiers;
                    menu.Items.Add(label, null, (s, e) => runConfigured(modifiers));
                }
            }
            return menu;
        }
    }
}
