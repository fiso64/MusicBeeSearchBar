using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using System;
using System.Collections.Generic;

namespace MusicBeePlugin.UI
{
    public class ActionDefinition
    {
        public string Name { get; }
        public Type DataType { get; }
        public Func<BaseActionData> Factory { get; }

        public ActionDefinition(string name, Type dataType, Func<BaseActionData> factory)
        {
            Name = name;
            DataType = dataType;
            Factory = factory;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public static class ActionRegistry
    {
        private static readonly ActionDefinition PlayNow = new ActionDefinition("Play Now", typeof(PlayActionData), () => new PlayActionData());
        private static readonly ActionDefinition QueueNext = new ActionDefinition("Queue Next", typeof(QueueNextActionData), () => new QueueNextActionData());
        private static readonly ActionDefinition QueueLast = new ActionDefinition("Queue Last", typeof(QueueLastActionData), () => new QueueLastActionData());
        private static readonly ActionDefinition SearchInTab = new ActionDefinition("Search In Tab", typeof(SearchInTabActionData), () => new SearchInTabActionData());
        private static readonly ActionDefinition OpenFilter = new ActionDefinition("Open Filter In Tab", typeof(OpenFilterInTabActionData), () => new OpenFilterInTabActionData());
        private static readonly ActionDefinition OpenPlaylist = new ActionDefinition("Open Playlist In Tab", typeof(OpenPlaylistInTabActionData), () => new OpenPlaylistInTabActionData());
        private static readonly ActionDefinition MusicExplorer = new ActionDefinition("Open In Music Explorer", typeof(OpenInMusicExplorerActionData), () => new OpenInMusicExplorerActionData());
        private static readonly ActionDefinition MusicExplorerInTab = new ActionDefinition("Open In Music Explorer In Tab", typeof(OpenInMusicExplorerInTabActionData), () => new OpenInMusicExplorerInTabActionData());

        public static List<ActionDefinition> GetActionsForType(ResultType type)
        {
            var actions = new List<ActionDefinition>
            {
                PlayNow,
                QueueNext,
                QueueLast,
                SearchInTab
            };

            // "Open Filter" is supported by everything except Playlists
            if (type != ResultType.Playlist)
            {
                actions.Add(OpenFilter);
            }
            else
            {
                actions.Add(OpenPlaylist);
            }

            // "Music Explorer" is specific to Artists
            if (type == ResultType.Artist)
            {
                actions.Add(MusicExplorer);
                actions.Add(MusicExplorerInTab);
            }

            return actions;
        }
    }
}