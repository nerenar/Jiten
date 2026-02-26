namespace Jiten.Core;

public interface ICdnService
{
    Task<string> UploadFile(byte[] file, string fileName);
    Task DeleteFile(string storagePath);
    string GetCdnUrl(string storagePath);
}
