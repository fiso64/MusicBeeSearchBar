using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MusicBeePlugin.Plugin;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace MusicBeePlugin.Services
{
    public class ImageService
    {
        private MusicBeeApiInterface mbApi;
        private readonly int imageSize;
        private readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();

        public ImageService(MusicBeeApiInterface mbApi, SearchService searchService, int imageSize = 40)
        {
            this.mbApi = mbApi;
            this.imageSize = imageSize;
        }

        private string GetCacheKey(string identifier, ResultType type) => $"{type}:{identifier}";

        public Image GetCachedImage(SearchResult result)
        {
            if (result == null) return null;

            try
            {
                switch (result.Type)
                {
                    case ResultType.Artist:
                        var artistResult = ArtistResult.FromSearchResult(result);
                        return GetCachedImage(artistResult.Artist, ResultType.Artist);
                        
                    case ResultType.Album:
                        var albumResult = AlbumResult.FromSearchResult(result);
                        return GetCachedImage($"{albumResult.AlbumArtist}:{albumResult.Album}", ResultType.Album);
                        
                    case ResultType.Song:
                        var songResult = SongResult.FromSearchResult(result);
                        return GetCachedImage(songResult.Filepath, ResultType.Song);
                        
                    default:
                        return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Image GetCachedImage(string identifier, ResultType type)
        {
            string cacheKey = GetCacheKey(identifier, type);
            return imageCache.ContainsKey(cacheKey) ? imageCache[cacheKey] : null;
        }

        public async Task<Image> GetArtistImageAsync(string artist)
        {
            string cacheKey = GetCacheKey(artist, ResultType.Artist);
            if (imageCache.ContainsKey(cacheKey))
                return imageCache[cacheKey];

            try
            {
                string imagePath = mbApi.Library_GetArtistPictureThumb(artist);
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    return null;

                using (var originalImage = Image.FromFile(imagePath))
                {
                    var thumb = CreateSquareThumb(originalImage, makeCircular: true);
                    imageCache[cacheKey] = thumb;
                    return thumb;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<Image> GetAlbumImageAsync(string albumArtist, string album)
        {
            string cacheKey = GetCacheKey($"{albumArtist}:{album}", ResultType.Album);
            if (imageCache.ContainsKey(cacheKey))
                return imageCache[cacheKey];

            try
            {
                var query = MusicBeeHelpers.ConstructLibraryQuery(
                    (MetaDataType.AlbumArtist, ComparisonType.Is, albumArtist),
                    (MetaDataType.Album, ComparisonType.Is, album)
                );
                mbApi.Library_QueryFilesEx(query, out string[] files);

                if (files == null || files.Length == 0)
                    return null;

                mbApi.Library_GetArtworkEx(files[0], 0, true, out _, out _, out byte[] imageData);
                if (imageData == null || imageData.Length == 0)
                    return null;

                using (var ms = new MemoryStream(imageData))
                using (var originalImage = Image.FromStream(ms))
                {
                    var thumb = CreateSquareThumb(originalImage);
                    imageCache[cacheKey] = thumb;
                    return thumb;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<Image> GetFileImageAsync(string filepath)
        {
            string cacheKey = GetCacheKey(filepath, ResultType.Song);
            if (imageCache.ContainsKey(cacheKey))
                return imageCache[cacheKey];

            try
            {
                mbApi.Library_GetArtworkEx(filepath, 0, true, out _, out _, out byte[] imageData);
                if (imageData == null || imageData.Length == 0)
                    return null;

                using (var ms = new MemoryStream(imageData))
                using (var originalImage = Image.FromStream(ms))
                {
                    var thumb = CreateSquareThumb(originalImage);
                    imageCache[cacheKey] = thumb;
                    return thumb;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void ClearCache()
        {
            foreach (var image in imageCache.Values)
            {
                image?.Dispose();
            }
            imageCache.Clear();
        }

        private Image CreateSquareThumb(Image original, bool makeCircular = false)
        {
            int size = Math.Min(original.Width, original.Height);
            var bitmap = new Bitmap(imageSize, imageSize);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Configure for high quality, high performance scaling
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                if (makeCircular)
                {
                    graphics.Clear(Color.Transparent);
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(0, 0, imageSize, imageSize);
                        graphics.SetClip(path);
                    }
                }

                // Calculate cropping rectangle
                int x = (original.Width - size) / 2;
                int y = (original.Height - size) / 2;

                // Draw and scale in one operation
                graphics.DrawImage(original, 
                    new Rectangle(0, 0, imageSize, imageSize),
                    new Rectangle(x, y, size, size),
                    GraphicsUnit.Pixel);
            }

            return bitmap;
        }

        public async Task<Image> GetImageAsync(SearchResult result)
        {
            if (result == null) return null;

            try
            {
                switch (result.Type)
                {
                    case ResultType.Artist:
                        var artistResult = ArtistResult.FromSearchResult(result);
                        return await GetArtistImageAsync(artistResult.Artist);
                        
                    case ResultType.Album:
                        var albumResult = AlbumResult.FromSearchResult(result);
                        return await GetAlbumImageAsync(albumResult.AlbumArtist, albumResult.Album);
                        
                    case ResultType.Song:
                        var songResult = SongResult.FromSearchResult(result);
                        return await GetFileImageAsync(songResult.Filepath);
                        
                    default:
                        return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
