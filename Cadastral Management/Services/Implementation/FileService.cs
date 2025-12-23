using Cadastral_ManagementServices;
using Microsoft.AspNetCore.Http;

namespace Cadastral_Management.Services.Implementation
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;

        public FileService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> SaveApplicationDocumentAsync(IFormFile file, int applicationId)
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "applications");

            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var fileName = $"{applicationId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/applications/{fileName}";
        }

        public async Task<byte[]> ReadFileAsync(string filePath)
        {
            var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
            return await System.IO.File.ReadAllBytesAsync(fullPath);
        }

        public bool FileExists(string filePath)
        {
            var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
            return System.IO.File.Exists(fullPath);
        }

        public string GetUploadsPath()
        {
            return Path.Combine(_environment.WebRootPath, "uploads");
        }

        public void DeleteFile(string filePath)
        {
            var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}