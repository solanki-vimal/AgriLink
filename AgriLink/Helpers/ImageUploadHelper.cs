namespace AgriLink.Helpers
{
    public static class ImageUploadHelper
    {
        // Saves an uploaded file under wwwroot/images/{subfolder}/ with a
        // generated unique name, and returns the web-relative path to store
        // in the database (e.g. "/images/produce/abc123.jpg").
        public static async Task<string> SaveImageAsync(IFormFile file, IWebHostEnvironment environment, string subfolder)
        {
            var uploadsFolder = Path.Combine(environment.WebRootPath, "images", subfolder);
            Directory.CreateDirectory(uploadsFolder); // no-op if it already exists

            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/{subfolder}/{uniqueFileName}";
        }

        // Deletes a previously saved image given its web-relative path.
        // Safe to call even if the file doesn't exist (e.g. path was null).
        public static void DeleteImage(string? relativePath, IWebHostEnvironment environment)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            var fullPath = Path.Combine(environment.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
