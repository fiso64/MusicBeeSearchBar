using MusicBeePlugin.Config;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;
using MusicBeePlugin.Services;
using System.Threading.Tasks;

namespace MusicBeePlugin.UI
{
    public class SearchPanel : UserControl
    {
        private readonly MusicBeeApiInterface mbApi;
        private readonly SynchronizationContext musicBeeContext;
        private readonly SearchUIConfig searchUIConfig;
        private readonly Func<string, SearchResult, KeyEventArgs, Task<bool>> resultAcceptAction;
        private SearchControl searchControl;
        
        public SearchPanel(
            MusicBeeApiInterface mbApi,
            SynchronizationContext musicBeeContext,
            SearchUIConfig searchUIConfig,
            Func<string, SearchResult, KeyEventArgs, Task<bool>> resultAcceptAction
        )
        {
            this.mbApi = mbApi;
            this.musicBeeContext = musicBeeContext;
            this.searchUIConfig = searchUIConfig;
            this.resultAcceptAction = resultAcceptAction;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.BackColor = searchUIConfig.BaseColor;

            searchControl = new SearchControl(musicBeeContext, mbApi, searchUIConfig)
            {
                Dock = DockStyle.Fill
            };

            // A panel does not auto-resize, so we don't need to handle ResizeRequested.
            // It also does not close, so CloseRequested is ignored.
            searchControl.ResultAccepted += OnResultAccepted;
            
            this.Controls.Add(searchControl);
        }

        private async Task OnResultAccepted(string searchText, SearchResult result, KeyEventArgs e)
        {
            // After accepting a result, clear the search text in the panel.
            searchControl.SetSearchText("");
            await resultAcceptAction(searchText, result, e);
        }

        public void FocusSearchBox()
        {
            searchControl.FocusSearchBox();
        }
    }
}