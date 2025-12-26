using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MusicBeePlugin.Services;

namespace MusicBeePlugin.Services
{
    public class IpcService : IDisposable
    {
        private const string PIPE_NAME = "MusicBeeSearchBarIPC";
        private CancellationTokenSource _cts;
        private Control _mainControl;

        public IpcService(Control mainControl)
        {
            _mainControl = mainControl;
            _cts = new CancellationTokenSource();
            Task.Factory.StartNew(ServerLoop, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private async Task ServerLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(PIPE_NAME, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        await server.WaitForConnectionAsync(_cts.Token);
                        using (var reader = new StreamReader(server))
                        {
                            string message = await reader.ReadToEndAsync();
                            ProcessMessage(message);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception) 
                { 
                    // Ignore errors to keep loop running
                }
            }
        }

        private void ProcessMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            
            string[] parts = message.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return;

            string type = parts[0].Trim().ToLowerInvariant();
            
            _mainControl.BeginInvoke(new Action(async () => {
                 await HandleAction(type, parts);
            }));
        }

        private async Task HandleAction(string type, string[] parts)
        {
            SearchResult result = null;
            
            try 
            {
                if (type == "song" && parts.Length >= 2)
                {
                    string path = parts[1].Trim();
                    result = new SongResult(new Track(path));
                }
                else if (type == "artist" && parts.Length >= 2)
                {
                    string artist = parts[1].Trim();
                    result = new ArtistResult(artist, artist); 
                }
                else if (type == "album" && parts.Length >= 3)
                {
                    // Format: Album|AlbumName|AlbumArtist
                    string album = parts[1].Trim();
                    string albumArtist = parts[2].Trim();
                    result = new AlbumResult(album, albumArtist, albumArtist);
                }

                if (result != null)
                {
                    var actionService = new ActionService(Plugin.GetConfig().SearchActions);
                    // Keys.None triggers the "Default" action
                    await actionService.RunAction(result.DisplayTitle, result, new KeyEventArgs(Keys.None));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IPC HandleAction Error: {ex}");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}