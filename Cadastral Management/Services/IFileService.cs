// Services/IFileService.cs
using Microsoft.AspNetCore.Http;

namespace Cadastral_ManagementServices
{
    public interface IFileService
    {
        Task<string> SaveApplicationDocumentAsync(IFormFile file, int applicationId);
        Task<byte[]> ReadFileAsync(string filePath);
        bool FileExists(string filePath);
        string GetUploadsPath();
        void DeleteFile(string filePath);
    }
}