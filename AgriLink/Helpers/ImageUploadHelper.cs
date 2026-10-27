using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace AgriLink.Helpers
{
    public static class ImageUploadHelper
    {
        // Holds an optional Cloudinary instance if credentials are provided in configuration
        private static Cloudinary? _cloudinary;

        public static void Initialize(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (!string.IsNullOrWhiteSpace(cloudName) &&
                !string.IsNullOrWhiteSpace(apiKey) &&
                !string.IsNullOrWhiteSpace(apiSecret))
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account);
                _cloudinary.Api.Secure = true;
            }
        }

        // Saves an uploaded file to Cloudinary if configured; otherwise falls back to local disk (wwwroot/images/{subfolder})
        public static async Task<string> SaveImageAsync(IFormFile file, IWebHostEnvironment environment, string subfolder)
        {
            if (file == null || file.Length == 0)
            {
                return string.Empty;
            }

            // Cloudinary Cloud Storage
            if (_cloudinary != null)
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"agrilink/{subfolder}",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK && uploadResult.SecureUrl != null)
                {
                    return uploadResult.SecureUrl.ToString();
                }
            }

            // Fallback: Local Disk Storage
            var uploadsFolder = Path.Combine(environment.WebRootPath, "images", subfolder);
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/{subfolder}/{uniqueFileName}";
        }

        // Deletes a previously saved image
        public static void DeleteImage(string? imageUrl, IWebHostEnvironment environment)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return;
            }

            // If it's a Cloudinary URL
            if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (_cloudinary != null)
                {
                    try
                    {
                        // Extract public_id from Cloudinary URL if desired
                        var uri = new Uri(imageUrl);
                        var segments = uri.AbsolutePath.Split('/');
                        var uploadIndex = Array.IndexOf(segments, "upload");
                        if (uploadIndex >= 0 && uploadIndex < segments.Length - 1)
                        {
                            var publicIdWithExt = string.Join('/', segments.Skip(uploadIndex + 2)); // skip version
                            var publicId = Path.ChangeExtension(publicIdWithExt, null);
                            _cloudinary.Destroy(new DeletionParams(publicId));
                        }
                    }
                    catch
                    {
                        // best-effort deletion
                    }
                }
                return;
            }

            // Otherwise, local file
            var fullPath = Path.Combine(environment.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
