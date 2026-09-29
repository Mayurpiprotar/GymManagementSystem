namespace GymManagementSystem.Services;

using GymManagementSystem.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Result container holding storage metadata after a trainer application document is safely written to disk.
/// </summary>
public class TrainerDocumentSaveResult
{
    /// <summary>
    /// Server-generated unique GUID filename with extension (e.g. "a1b2c3d4e5f6...pdf").
    /// Never exposes or trusts client-supplied filenames on disk.
    /// </summary>
    public string StorageFileName { get; set; } = string.Empty;

    /// <summary>
    /// Original file name provided by applicant, preserved for display metadata only.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Relative storage path from project root (e.g. "App_Data/TrainerDocuments/{guid}.pdf").
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Size of the saved file in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }
}

/// <summary>
/// Infrastructure service for safely validating, storing, retrieving, and deleting trainer certification documents.
/// IMPORTANT: Files are stored strictly outside wwwroot in App_Data/TrainerDocuments.
/// They are NEVER publicly accessible via direct URL.
/// Storage filenames are server-generated GUIDs to eliminate path traversal and malicious overwrites.
/// </summary>
public class TrainerDocumentStorage
{
    private readonly string _storageDirectory;

    // Magic byte signatures for genuine document formats
    private static readonly byte[] PdfMagicBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF
    private static readonly byte[] JpegMagicBytes = [0xFF, 0xD8, 0xFF];        // FF D8 FF
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]; // PNG signature

    public TrainerDocumentStorage(IWebHostEnvironment? environment = null)
    {
        var contentRoot = environment?.ContentRootPath ?? Directory.GetCurrentDirectory();
        _storageDirectory = Path.Combine(contentRoot, "App_Data", "TrainerDocuments");

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    /// <summary>
    /// Gets the absolute directory path where trainer documents are stored outside wwwroot.
    /// </summary>
    public string StorageDirectory => _storageDirectory;

    /// <summary>
    /// Validates an uploaded IFormFile against size constraints, allowed extensions, MIME types, and magic bytes.
    /// </summary>
    public bool ValidateDocument(IFormFile? file, out string? errorMessage)
    {
        if (file == null || file.Length == 0)
        {
            errorMessage = "No file was uploaded or the uploaded file is empty.";
            return false;
        }

        using var stream = file.OpenReadStream();
        return ValidateDocument(stream, file.FileName, file.ContentType, file.Length, out errorMessage);
    }

    /// <summary>
    /// Validates an arbitrary stream and metadata against trainer document rules.
    /// Checks:
    /// 1. File size &lt;= 5 MB
    /// 2. Extension in [.pdf, .jpg, .jpeg, .png]
    /// 3. MIME content-type is reasonable (if provided)
    /// 4. Binary signature (magic bytes) matches format
    /// </summary>
    public bool ValidateDocument(
        Stream? stream,
        string? originalFileName,
        string? contentType,
        long fileLength,
        out string? errorMessage)
    {
        if (stream == null || fileLength <= 0)
        {
            errorMessage = "File stream is empty or missing.";
            return false;
        }

        // 1. File size check
        if (fileLength > GymConstants.TrainerDocuments.MaxFileSizeBytes)
        {
            var maxMb = GymConstants.TrainerDocuments.MaxFileSizeBytes / (1024 * 1024);
            var fileMb = fileLength / (1024 * 1024.0);
            errorMessage = $"File size ({fileMb:F2} MB) exceeds the maximum allowed size of {maxMb} MB.";
            return false;
        }

        // 2. Extension check
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            errorMessage = "Filename was not provided.";
            return false;
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !GymConstants.TrainerDocuments.AllowedExtensions.Contains(extension))
        {
            errorMessage = $"Unsupported file format '{extension}'. Allowed formats: {string.Join(", ", GymConstants.TrainerDocuments.AllowedExtensions)}.";
            return false;
        }

        // 3. MIME type check (when provided by client)
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var isMimeValid = extension switch
            {
                ".pdf" => contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase),
                ".jpg" or ".jpeg" => contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                                  || contentType.Equals("image/pjpeg", StringComparison.OrdinalIgnoreCase)
                                  || contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase),
                ".png" => contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
                       || contentType.Equals("image/x-png", StringComparison.OrdinalIgnoreCase),
                _ => false
            };

            if (!isMimeValid)
            {
                errorMessage = $"Content-Type '{contentType}' does not match expected MIME type for '{extension}' files.";
                return false;
            }
        }

        // 4. Magic bytes / Binary signature check
        var initialPosition = stream.CanSeek ? stream.Position : 0;
        var headerBytes = new byte[8];
        var bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);

        if (stream.CanSeek)
        {
            stream.Position = initialPosition;
        }

        if (bytesRead < 3)
        {
            errorMessage = "File is too short to verify header signature.";
            return false;
        }

        bool signatureMatches = extension switch
        {
            ".pdf" => bytesRead >= 4 && MatchesSignature(headerBytes, PdfMagicBytes),
            ".jpg" or ".jpeg" => bytesRead >= 3 && MatchesSignature(headerBytes, JpegMagicBytes),
            ".png" => bytesRead >= 8 && MatchesSignature(headerBytes, PngMagicBytes),
            _ => false
        };

        if (!signatureMatches)
        {
            errorMessage = $"File signature does not match genuine '{extension}' format. File may be corrupted or disguised.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates and saves an uploaded trainer document to disk using a generated GUID filename.
    /// Never trusts client-supplied filenames on disk.
    /// Returns the storage metadata for database persistence.
    /// </summary>
    public async Task<TrainerDocumentSaveResult> SaveDocumentAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file), "No file provided for storage.");
        }

        if (!ValidateDocument(file, out var error))
        {
            throw new InvalidOperationException($"Document validation failed: {error}");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storageFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(_storageDirectory, storageFileName);

        await using (var outputStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var inputStream = file.OpenReadStream())
        {
            await inputStream.CopyToAsync(outputStream, cancellationToken);
        }

        var sanitizedOriginalName = Path.GetFileName(file.FileName);
        var relativePath = $"{GymConstants.TrainerDocuments.StorageDirectoryRelativePath}/{storageFileName}";

        return new TrainerDocumentSaveResult
        {
            StorageFileName = storageFileName,
            OriginalFileName = sanitizedOriginalName,
            RelativePath = relativePath,
            FileSizeBytes = file.Length
        };
    }

    /// <summary>
    /// Stream overload for saving documents directly (e.g. testing or programmatic generation).
    /// </summary>
    public async Task<TrainerDocumentSaveResult> SaveDocumentAsync(
        Stream stream,
        string originalFileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var length = stream.CanSeek ? stream.Length : 0;
        if (!ValidateDocument(stream, originalFileName, contentType, length, out var error))
        {
            throw new InvalidOperationException($"Document validation failed: {error}");
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var storageFileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(_storageDirectory, storageFileName);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        await using (var outputStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(outputStream, cancellationToken);
        }

        var sanitizedOriginalName = Path.GetFileName(originalFileName);
        var relativePath = $"{GymConstants.TrainerDocuments.StorageDirectoryRelativePath}/{storageFileName}";

        return new TrainerDocumentSaveResult
        {
            StorageFileName = storageFileName,
            OriginalFileName = sanitizedOriginalName,
            RelativePath = relativePath,
            FileSizeBytes = length
        };
    }

    /// <summary>
    /// Resolves the absolute physical path on disk for a stored document.
    /// Strictly guards against directory traversal attacks.
    /// </summary>
    public string GetPhysicalPath(string storageFileNameOrRelativePath)
    {
        if (string.IsNullOrWhiteSpace(storageFileNameOrRelativePath))
        {
            throw new ArgumentException("Storage file name or relative path cannot be empty.", nameof(storageFileNameOrRelativePath));
        }

        // Reject directory traversal indicators immediately
        if (storageFileNameOrRelativePath.Contains("..") || storageFileNameOrRelativePath.Contains(':'))
        {
            throw new InvalidOperationException("Directory traversal attempt detected in document path.");
        }

        // Extract filename only to prevent path traversal like "../../../etc/passwd"
        var fileName = Path.GetFileName(storageFileNameOrRelativePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Invalid document path specified.", nameof(storageFileNameOrRelativePath));
        }

        var combinedPath = Path.Combine(_storageDirectory, fileName);
        var fullPath = Path.GetFullPath(combinedPath);

        // Security check: ensure path is strictly within _storageDirectory
        var normalizedStorageDir = Path.GetFullPath(_storageDirectory);
        if (!fullPath.StartsWith(normalizedStorageDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path traversal attempt detected.");
        }

        return fullPath;
    }

    /// <summary>
    /// Checks whether the specified stored document exists physically on disk.
    /// </summary>
    public bool FileExists(string storageFileNameOrRelativePath)
    {
        try
        {
            var path = GetPhysicalPath(storageFileNameOrRelativePath);
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Safely opens a read stream to a stored document.
    /// Used by authorized admin endpoints for viewing applicant certifications.
    /// </summary>
    public Stream OpenReadStream(string storageFileNameOrRelativePath)
    {
        var physicalPath = GetPhysicalPath(storageFileNameOrRelativePath);
        if (!File.Exists(physicalPath))
        {
            throw new FileNotFoundException("Requested trainer document was not found on disk.", storageFileNameOrRelativePath);
        }

        return new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>
    /// Safely deletes a document from disk if it exists.
    /// Returns true if the file was deleted, false if it did not exist.
    /// </summary>
    public bool DeleteDocument(string? storageFileNameOrRelativePath)
    {
        if (string.IsNullOrWhiteSpace(storageFileNameOrRelativePath))
        {
            return false;
        }

        try
        {
            var path = GetPhysicalPath(storageFileNameOrRelativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // Silently return false on deletion failure
        }

        return false;
    }

    private static bool MatchesSignature(byte[] buffer, byte[] signature)
    {
        if (buffer.Length < signature.Length)
        {
            return false;
        }

        for (int i = 0; i < signature.Length; i++)
        {
            if (buffer[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }
}
