using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.Services
{
    public class ImageService : IDisposable
    {
        private readonly MusicBeeApiInterface mbApi;
        private readonly Config.SearchUIConfig searchUIConfig;
        private readonly int _defaultImageSize;
        private readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private readonly object _cacheLock = new object();
        private bool disposed = false;
        private readonly int _artworkCornerRadius;

        private readonly string _coverJpgHash;
        private readonly string _coverJpegHash;
        private readonly string _coverPngHash;

        public ImageService(MusicBeeApiInterface mbApi, SearchService searchService, Config.SearchUIConfig searchUIConfig, int imageSize = 40, int artworkCornerRadius = 8)
        {
            this.mbApi = mbApi;
            this.searchUIConfig = searchUIConfig;
            this._defaultImageSize = imageSize;
            this._artworkCornerRadius = artworkCornerRadius;

            _coverJpgHash = MusicBeeHelpers.GenerateSourceFileHash("Cover.jpg");
            _coverJpegHash = MusicBeeHelpers.GenerateSourceFileHash("Cover.jpeg");
            _coverPngHash = MusicBeeHelpers.GenerateSourceFileHash("Cover.png");
        }

        private string GetCacheKey(string identifier, ResultType type, int size) => $"{type}:{identifier}:{size}";

        public Image GetCachedImage(SearchResult result, int size)
        {
            if (result == null) return null;

            try
            {
                switch (result.Type)
                {
                    case ResultType.Artist:
                        var artistResult = ArtistResult.FromSearchResult(result);
                        return GetCachedImage(artistResult.Artist, ResultType.Artist, size);

                    case ResultType.Album:
                        var albumResult = AlbumResult.FromSearchResult(result);
                        return GetCachedImage($"{albumResult.AlbumArtist}:{albumResult.Album}", ResultType.Album, size);

                    case ResultType.Song:
                        var songResult = SongResult.FromSearchResult(result);
                        return GetCachedImage(songResult.Filepath, ResultType.Song, size);

                    default:
                        return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Image GetCachedImage(string identifier, ResultType type, int size)
        {
            string cacheKey = GetCacheKey(identifier, type, size);
            lock (_cacheLock)
            {
                return imageCache.TryGetValue(cacheKey, out Image cachedImage) ? cachedImage : null;
            }
        }

        public async Task<Image> GetArtistImageAsync(string artist, int size)
        {
            string cacheKey = GetCacheKey(artist, ResultType.Artist, size);

            lock (_cacheLock)
            {
                if (imageCache.ContainsKey(cacheKey))
                    return imageCache[cacheKey];
            }

            Image thumb = null;
            try
            {
                string imagePath = mbApi.Library_GetArtistPictureThumb(artist);
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath) && new FileInfo(imagePath).Length > 0)
                {
                    using (var originalImage = Image.FromFile(imagePath))
                    {
                        thumb = CreateSquareThumb(originalImage, size, isCircular: true);
                    }
                }
            }
            catch (Exception)
            {
                thumb?.Dispose();
                return null;
            }

            if (disposed || thumb == null)
            {
                thumb?.Dispose();
                return null;
            }

            lock (_cacheLock)
            {
                if (imageCache.TryGetValue(cacheKey, out var existingImage))
                {
                    thumb.Dispose(); // We created a duplicate, so dispose it
                    return existingImage;
                }
                imageCache[cacheKey] = thumb;
                return thumb;
            }
        }

        private string GetInternalCacheImagePath(string anyTrackFilepath, bool preferExternalCover = true)
        {
            if (string.IsNullOrEmpty(anyTrackFilepath)) return null;

            try
            {
                string albumFolderPath = Path.GetDirectoryName(anyTrackFilepath);
                string albumCachePathPart = MusicBeeHelpers.GenerateAlbumCachePath(albumFolderPath);
                string albumHash = Path.GetFileName(albumCachePathPart);

                string internalCacheDir = Path.Combine(mbApi.Setting_GetPersistentStoragePath(), "InternalCache", "AlbumCovers");
                string searchDir = Path.Combine(internalCacheDir, Path.GetDirectoryName(albumCachePathPart));

                if (!Directory.Exists(searchDir)) return null;

                // Helper to check if an image file exists and meets size requirements.
                bool IsImageSuitable(string filePath)
                {
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

                    // A larger source image will produce a better downscaled result.
                    Size dims = GetImageDimensions(filePath);
                    return dims.Width >= _defaultImageSize * 2 && dims.Height >= _defaultImageSize * 2;
                };

                // Helper to check both .jpg and .png extensions for a given base path.
                string FindSuitableFileWithExtensions(string basePath)
                {
                    string jpgPath = basePath + ".jpg";
                    if (IsImageSuitable(jpgPath)) 
                        return jpgPath;

                    string pngPath = basePath + ".png";
                    if (IsImageSuitable(pngPath)) 
                        return pngPath;

                    return null;
                }

                string checkExternalArt()
                {
                    var preferredHashes = new[] { _coverJpgHash, _coverJpegHash, _coverPngHash };
                    foreach (var hash in preferredHashes)
                    {
                        string basePath = Path.Combine(searchDir, $"{albumHash}_{hash}");
                        string foundPath = FindSuitableFileWithExtensions(basePath);
                        if (foundPath != null) return foundPath;
                    }
                    return null;
                }

                string checkTrackSpecificArt()
                {
                    string trackFileHash = MusicBeeHelpers.GenerateSourceFileHash(Path.GetFileName(anyTrackFilepath));
                    string trackBasePath = Path.Combine(searchDir, $"{albumHash}_{trackFileHash}");
                    return FindSuitableFileWithExtensions(trackBasePath);
                }

                string imagePath = null;
                if (preferExternalCover)
                {
                    imagePath = checkExternalArt() ?? checkTrackSpecificArt();
                }
                else
                {
                    imagePath = checkTrackSpecificArt() ?? checkExternalArt();
                }

                if (!string.IsNullOrEmpty(imagePath)) return imagePath;

                // 3. Fallback: Search all cache files for the album.
                // This is the slowest path, used if standard names don't yield a suitable image.
                var checkedFileSizes = new HashSet<long>();
                var allFiles = Directory.EnumerateFiles(searchDir, $"{albumHash}_*");

                foreach (var file in allFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);

                        // Performance: If we've already checked a file of this size and it was too small,
                        // assume any other file with the same size is also unsuitable and skip it.
                        if (checkedFileSizes.Contains(fileInfo.Length)) continue;

                        if (IsImageSuitable(file))
                        {
                            return file;
                        }
                        else
                        {
                            // Remember this file size is unsuitable to avoid re-checking.
                            checkedFileSizes.Add(fileInfo.Length);
                        }
                    }
                    catch (IOException) { /* Ignore files that might be locked, deleted, or otherwise unreadable */ }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting internal cache image path: {ex.Message}");
            }

            return null;
        }

        private static Size GetImageDimensions(string path)
        {
            try
            {
                // Reading image dimensions this way is faster and uses less memory
                // than Image.FromFile, as it doesn't need to decode the full pixel data.
                // It also avoids locking the file on disk.
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var img = Image.FromStream(fs, useEmbeddedColorManagement: false, validateImageData: false))
                {
                    return new Size(img.Width, img.Height);
                }
            }
            catch (Exception) // FromStream can throw various exceptions for invalid/unsupported formats.
            {
                return Size.Empty;
            }
        }

        public async Task<Image> GetAlbumImageAsync(string albumArtist, string album, int size)
        {
            string cacheKey = GetCacheKey($"{albumArtist}:{album}", ResultType.Album, size);
            lock (_cacheLock)
            {
                if (imageCache.ContainsKey(cacheKey))
                    return imageCache[cacheKey];
            }

            Image thumb = null;
            try
            {
                var query = MusicBeeHelpers.ConstructLibraryQuery(
                    (MetaDataType.AlbumArtist, ComparisonType.Is, albumArtist),
                    (MetaDataType.Album, ComparisonType.Is, album)
                );
                mbApi.Library_QueryFilesEx(query, out string[] files);

                if (files != null && files.Length > 0)
                {
                    if (searchUIConfig.UseMusicBeeCacheForCovers)
                    {
                        string imagePath = GetInternalCacheImagePath(files[0]);
                        if (!string.IsNullOrEmpty(imagePath) && new FileInfo(imagePath).Length > 0)
                        {
                            using (var originalImage = Image.FromFile(imagePath))
                            {
                                thumb = CreateSquareThumb(originalImage, size, isCircular: false);
                            }
                        }
                    }

                    if (thumb == null) // Fallback
                    {
                        mbApi.Library_GetArtworkEx(files[0], 0, true, out _, out _, out byte[] imageData);
                        if (imageData != null && imageData.Length > 0)
                        {
                            using (var ms = new MemoryStream(imageData))
                            using (var originalImage = Image.FromStream(ms))
                            {
                                thumb = CreateSquareThumb(originalImage, size, isCircular: false);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                thumb?.Dispose();
                return null;
            }

            if (disposed || thumb == null)
            {
                thumb?.Dispose();
                return null;
            }

            lock (_cacheLock)
            {
                if (imageCache.TryGetValue(cacheKey, out var existingImage))
                {
                    thumb.Dispose();
                    return existingImage;
                }
                imageCache[cacheKey] = thumb;
                return thumb;
            }
        }

        public async Task<Image> GetFileImageAsync(string filepath, int size)
        {
            string cacheKey = GetCacheKey(filepath, ResultType.Song, size);
            lock (_cacheLock)
            {
                if (imageCache.ContainsKey(cacheKey))
                    return imageCache[cacheKey];
            }

            Image thumb = null;
            try
            {
                if (searchUIConfig.PreferAlbumImageForSongs)
                {
                    string album = mbApi.Library_GetFileTag(filepath, MetaDataType.Album);
                    string albumArtist = mbApi.Library_GetFileTag(filepath, MetaDataType.AlbumArtist);
                    if (!string.IsNullOrEmpty(album) && !string.IsNullOrEmpty(albumArtist))
                    {
                        thumb = await GetAlbumImageAsync(albumArtist, album, size);
                    }
                }

                if (thumb == null && searchUIConfig.UseMusicBeeCacheForCovers)
                {
                    string imagePath = GetInternalCacheImagePath(filepath, preferExternalCover: false);
                    if (!string.IsNullOrEmpty(imagePath) && new FileInfo(imagePath).Length > 0)
                    {
                        using (var originalImage = Image.FromFile(imagePath))
                        {
                            thumb = CreateSquareThumb(originalImage, size, isCircular: false);
                        }
                    }
                }

                // Fallback to original method
                if (thumb == null)
                {
                    mbApi.Library_GetArtworkEx(filepath, 0, true, out _, out _, out byte[] imageData);
                    if (imageData != null && imageData.Length > 0)
                    {
                        using (var ms = new MemoryStream(imageData))
                        using (var originalImage = Image.FromStream(ms))
                        {
                            thumb = CreateSquareThumb(originalImage, size, isCircular: false);
                        }
                    }
                }
            }
            catch (Exception)
            {
                thumb?.Dispose();
                return null;
            }

            if (disposed || thumb == null)
            {
                thumb?.Dispose();
                return null;
            }

            lock (_cacheLock)
            {
                if (imageCache.TryGetValue(cacheKey, out var existingImage))
                {
                    thumb.Dispose();
                    return existingImage;
                }
                imageCache[cacheKey] = thumb;
                return thumb;
            }
        }

        public void ClearCache()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ImageService));
            ClearCacheInternal();
        }

        private void ClearCacheInternal()
        {
            lock (_cacheLock)
            {
                foreach (var image in imageCache.Values)
                {
                    image?.Dispose();
                }
                imageCache.Clear();
            }
        }

        private static GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int diameter = radius * 2;
            diameter = Math.Min(diameter, Math.Min(bounds.Width, bounds.Height));

            if (diameter == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        public Image CreateSquareThumb(Image original, int size, bool isCircular)
        {
            if (original == null) return null;

            var destBitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            destBitmap.SetResolution(original.HorizontalResolution, original.VerticalResolution);

            try
            {
                using (var graphics = Graphics.FromImage(destBitmap))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;

                    // 1. Create a square crop from the center of the original image
                    int srcSize = Math.Min(original.Width, original.Height);
                    if (srcSize <= 0)
                    {
                        destBitmap.Dispose();
                        return null;
                    }
                    int srcX = (original.Width - srcSize) / 2;
                    int srcY = (original.Height - srcSize) / 2;
                    var sourceRect = new Rectangle(srcX, srcY, srcSize, srcSize);
                    var destRect = new Rectangle(0, 0, size, size);

                    // For high-quality rounded/circular images, we use a TextureBrush.
                    // This is more expensive but produces smooth anti-aliased edges.
                    using (var scaledSourceBitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                    using (var scaledGraphics = Graphics.FromImage(scaledSourceBitmap))
                    {
                        scaledGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        scaledGraphics.CompositingQuality = CompositingQuality.HighQuality;
                        scaledGraphics.DrawImage(original, destRect, sourceRect, GraphicsUnit.Pixel);

                        using (var textureBrush = new TextureBrush(scaledSourceBitmap, WrapMode.Clamp))
                        {
                            graphics.Clear(Color.Transparent); // Start with a transparent canvas
                            if (isCircular)
                            {
                                graphics.FillEllipse(textureBrush, destRect);
                            }
                            else // Apply rounded corners
                            {
                                using (var path = GetRoundedRectPath(destRect, _artworkCornerRadius))
                                {
                                    graphics.FillPath(textureBrush, path);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                destBitmap?.Dispose();
                return null;
            }

            return destBitmap;
        }

        public async Task<Image> GetImageAsync(SearchResult result, int size)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ImageService));
            if (result == null) return null;

            Image cached = GetCachedImage(result, size);
            if (cached != null) return cached;

            try
            {
                switch (result.Type)
                {
                    case ResultType.Artist:
                        var artistResult = ArtistResult.FromSearchResult(result);
                        return await GetArtistImageAsync(artistResult.Artist, size);

                    case ResultType.Album:
                        var albumResult = AlbumResult.FromSearchResult(result);
                        return await GetAlbumImageAsync(albumResult.AlbumArtist, albumResult.Album, size);

                    case ResultType.Song:
                        var songResult = SongResult.FromSearchResult(result);
                        return await GetFileImageAsync(songResult.Filepath, size);

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
