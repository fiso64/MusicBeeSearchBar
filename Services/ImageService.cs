using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.Services
{
    public class ImageService : IDisposable
    {
        private readonly MusicBeeApiInterface mbApi;
        private readonly int imageSize;
        private readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private bool disposed = false;

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
            return imageCache.TryGetValue(cacheKey, out Image cachedImage) ? cachedImage : null;
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

                if (new FileInfo(imagePath).Length <= 0)
                    return null;

                using (var originalImage = Image.FromFile(imagePath))
                {
                    var thumb = CreateSquareThumb(originalImage, makeCircular: true);
                    if (!disposed)
                    {
                        imageCache[cacheKey] = thumb;
                    } 
                    else
                    {
                        thumb?.Dispose();
                        thumb = null;
                    }
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
                    if (!disposed) { imageCache[cacheKey] = thumb; } else { thumb?.Dispose(); thumb = null; }
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
                    if (!disposed) { imageCache[cacheKey] = thumb; } else { thumb?.Dispose(); thumb = null; }
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
            if (disposed) throw new ObjectDisposedException(nameof(ImageService));
            ClearCacheInternal();
        }

        private void ClearCacheInternal()
        {
            foreach (var image in imageCache.Values)
            {
                image?.Dispose();
            }
            imageCache.Clear();
        }

        public Image CreateSquareThumb(Image original, bool makeCircular = false)
        {
            if (original == null) return null;

            int srcSize = Math.Min(original.Width, original.Height);
            if (srcSize <= 0) return null;

            int srcX = (original.Width - srcSize) / 2;
            int srcY = (original.Height - srcSize) / 2;
            var sourceRect = new Rectangle(srcX, srcY, srcSize, srcSize);

            var destBitmap = new Bitmap(imageSize, imageSize, PixelFormat.Format32bppArgb);
            destBitmap.SetResolution(original.HorizontalResolution, original.VerticalResolution);

            using (var graphics = Graphics.FromImage(destBitmap))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingQuality = CompositingQuality.HighQuality;

                var destRect = new Rectangle(0, 0, imageSize, imageSize);

                try
                {
                    if (!makeCircular)
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.DrawImage(original, destRect, sourceRect, GraphicsUnit.Pixel);
                    }
                    else
                    {
                        Bitmap scaledSourceBitmap = null;
                        try
                        {
                            scaledSourceBitmap = new Bitmap(imageSize, imageSize, PixelFormat.Format32bppArgb);
                            scaledSourceBitmap.SetResolution(original.HorizontalResolution, original.VerticalResolution);

                            using (var scaledGraphics = Graphics.FromImage(scaledSourceBitmap))
                            {
                                scaledGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                scaledGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                scaledGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                                scaledGraphics.CompositingQuality = CompositingQuality.HighQuality;

                                var tempDestRect = new Rectangle(0, 0, imageSize, imageSize);

                                scaledGraphics.DrawImage(original, tempDestRect, sourceRect, GraphicsUnit.Pixel);

                            }

                            using (var textureBrush = new TextureBrush(scaledSourceBitmap, WrapMode.Clamp))
                            {
                                graphics.Clear(Color.Transparent);
                                graphics.FillEllipse(textureBrush, destRect);
                            }

                        }
                        finally
                        {
                            scaledSourceBitmap?.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    destBitmap?.Dispose();
                    return null;
                }
            }

            return destBitmap;
        }

        public async Task<Image> GetImageAsync(SearchResult result)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ImageService));
            if (result == null) return null;

            Image cached = GetCachedImage(result);
            if (cached != null) return cached;

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                ClearCacheInternal();
            }

            disposed = true;
        }
    }
}
