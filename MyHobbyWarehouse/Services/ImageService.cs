using System.Windows.Media.Imaging;

namespace MyHobbyWarehouse.Services;

public static class ImageService
{
    private static string? _dbPath;

    public static void Initialize(string dbPath) => _dbPath = dbPath;

    public static string ImagesFolder =>
        Path.Combine(Path.GetDirectoryName(_dbPath ?? "")!, "images");

    public static string LocationImagesFolder =>
        Path.Combine(Path.GetDirectoryName(_dbPath ?? "")!, "images", "locations");

    /// <summary>Returns the image path for a location code if it exists, else null.</summary>
    public static string? FindLocationImage(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp" })
        {
            string path = Path.Combine(LocationImagesFolder, code + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>Copies source image to location images folder.</summary>
    public static string SaveLocationImage(string code, string sourcePath)
    {
        Directory.CreateDirectory(LocationImagesFolder);
        string ext  = Path.GetExtension(sourcePath).ToLowerInvariant();
        string dest = Path.Combine(LocationImagesFolder, code + ext);
        DeleteLocationImages(code);
        File.Copy(sourcePath, dest, overwrite: true);
        return dest;
    }

    /// <summary>Deletes all image files for a location code.</summary>
    public static void DeleteLocationImages(string code)
    {
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp" })
        {
            string p = Path.Combine(LocationImagesFolder, code + ext);
            if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>Returns the image path for a SKU if it exists, else null.</summary>
    public static string? FindImage(string sku)
    {
        if (string.IsNullOrEmpty(sku)) return null;
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp" })
        {
            string path = Path.Combine(ImagesFolder, sku + ext);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>Copies source image to images folder, naming it {sku}{ext}.</summary>
    public static string SaveImage(string sku, string sourcePath)
    {
        Directory.CreateDirectory(ImagesFolder);
        string ext  = Path.GetExtension(sourcePath).ToLowerInvariant();
        string dest = Path.Combine(ImagesFolder, sku + ext);

        // Remove old images for this SKU first
        DeleteImages(sku);
        File.Copy(sourcePath, dest, overwrite: true);
        return dest;
    }

    /// <summary>Deletes all image files for a SKU.</summary>
    public static void DeleteImages(string sku)
    {
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp" })
        {
            string p = Path.Combine(ImagesFolder, sku + ext);
            if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>Loads a BitmapImage from a file using a memory stream to avoid WPF URI caching.</summary>
    public static BitmapImage? LoadBitmapFresh(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new MemoryStream(File.ReadAllBytes(path));
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 320;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    /// <summary>Loads a BitmapImage from a file path.</summary>
    public static BitmapImage? LoadBitmap(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource      = new Uri(path, UriKind.Absolute);
            bmp.CacheOption    = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 320;   // limit memory
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
