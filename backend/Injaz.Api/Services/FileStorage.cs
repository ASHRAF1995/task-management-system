using Microsoft.Extensions.Options;

namespace Injaz.Api.Services;

/// <summary>تخزين المرفقات على القرص خارج wwwroot. يمكن استبداله لاحقاً بـ Azure Blob أو S3.</summary>
public class FileStorage
{
    private readonly string _root;

    public FileStorage(IOptions<StorageOptions> options, IWebHostEnvironment env)
    {
        var path = options.Value.UploadsPath;
        _root = Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken ct)
    {
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = File.Create(Path.Combine(_root, storedName));
        await file.CopyToAsync(stream, ct);
        return storedName;
    }

    public Stream? OpenRead(string storedName)
    {
        var full = Path.Combine(_root, Path.GetFileName(storedName));
        return File.Exists(full) ? File.OpenRead(full) : null;
    }

    public void Delete(string storedName)
    {
        var full = Path.Combine(_root, Path.GetFileName(storedName));
        if (File.Exists(full)) File.Delete(full);
    }
}
