using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.Services
{
    public class ImageService // currently unused
    {
        private MusicBeeApiInterface mbApi;
        private SearchService searchService;

        public ImageService(MusicBeeApiInterface mbApi, SearchService searchService)
        {
            this.mbApi = mbApi;
        }

        public void GetArtistImage(string artist) // modify to return a suitable image object
        {
            string imagePath = mbApi.Library_GetArtistPictureThumb(artist); // might be null or empty
            // image needs to be loaded from path
        }

        public void GetAlbumImage(string album) // modify to return a suitable image object
        {
            var first = searchService.database.FirstOrDefault(x => x.Album == album);
            if (first != null)
            {
                mbApi.Library_GetArtworkEx(first.Filepath, 0, true, out var pictureLocation, out _, out byte[] imageData);
                // imageData should contain the bytes of the image if there is one (might be null)
            }
        }

        public void GetFileImage(string filepath) // modify to return a suitable image object
        {
            mbApi.Library_GetArtworkEx(filepath, 0, true, out var pictureLocation, out _, out byte[] imageData); // again, might be null
        }
    }
}
