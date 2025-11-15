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
        private readonly int imageSize;
        private readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private bool disposed = false;

        private readonly string _coverJpgHash;
        private readonly string _coverJpegHash;
        private readonly string _coverPngHash;

        public ImageService(MusicBeeApiInterface mbApi, SearchService searchService, Config.SearchUIConfig searchUIConfig, int imageSize = 40)
        {
            this.mbApi = mbApi;
            this.searchUIConfig = searchUIConfig;
            this.imageSize = imageSize;

            _coverJpgHash = MusicBeeHelpers.GenerateSourceFileHash("Cover.jpg");
            _coverJpegHash = MusicBeeHelpers.GenerateSourceFileHash("Cover.jpeg");
            _coverPngHash = MusicBeeHelpers.GenerateSourceFileHash("Cover.png");
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
                    if (!disposed && thumb != null)
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
                    return dims.Width >= imageSize * 2 && dims.Height >= imageSize * 2;
                };

                // Helper to check both .jpg and .png extensions for a given base path.
                string FindSuitableFileWithExtensions(string basePath)
                {
                    string jpgPath = basePath + ".jpg";
                    if (IsImageSuitable(jpgPath)) return jpgPath;

                    string pngPath = basePath + ".png";
                    if (IsImageSuitable(pngPath)) return pngPath;

                    return null;
                }

                string checkExternalArt()
                {
                    var preferredHashes = new[] { _coverJpgHash, _coverJpegHash, _coverPngHash };
                    foreach (var hash in preferredHashes)
                    {
                        string basePath = Path.Combine(searchDir, $"{albumHash}_{hash}");
                        string foundPath = FindSuitableFileWithExtensions(basePath);
                        if (foundPath != null)
                            return foundPath;
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

                if (searchUIConfig.UseMusicBeeCacheForCovers)
                {
                    string imagePath = GetInternalCacheImagePath(files[0]);
                    if (!string.IsNullOrEmpty(imagePath) && new FileInfo(imagePath).Length > 0)
                    {
                        using (var originalImage = Image.FromFile(imagePath))
                        {
                            var thumb = CreateSquareThumb(originalImage);
                            if (!disposed) { imageCache[cacheKey] = thumb; } else { thumb?.Dispose(); thumb = null; }
                            return thumb;
                        }
                    }
                }

                // Fallback to original method
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
                if (searchUIConfig.UseMusicBeeCacheForCovers)
                {
                    string imagePath = GetInternalCacheImagePath(filepath, preferExternalCover: false);
                    if (!string.IsNullOrEmpty(imagePath) && new FileInfo(imagePath).Length > 0)
                    {
                        using (var originalImage = Image.FromFile(imagePath))
                        {
                            var thumb = CreateSquareThumb(originalImage);
                            if (!disposed) { imageCache[cacheKey] = thumb; } else { thumb?.Dispose(); thumb = null; }
                            return thumb;
                        }
                    }
                }

                // Fallback to original method
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
