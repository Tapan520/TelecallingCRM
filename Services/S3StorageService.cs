using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace TelecallingCRM.Services;

public interface IS3StorageService
{
    /// <summary>Returns true if S3 is configured for this app.</summary>
    bool IsEnabled { get; }

    /// <summary>Uploads a stream and returns the public/pre-signed URL.</summary>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken ct = default);

    /// <summary>Deletes an object by its S3 key.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}

public class S3StorageService : IS3StorageService
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<S3StorageService> _log;

    private string? BucketName => _cfg["S3:BucketName"];
    private string? Region     => _cfg["S3:Region"];
    public bool IsEnabled => !string.IsNullOrWhiteSpace(BucketName) &&
                             !string.IsNullOrWhiteSpace(_cfg["S3:AccessKey"]);

    public S3StorageService(IConfiguration cfg, ILogger<S3StorageService> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        var client = BuildClient();
        var key    = $"{folder.Trim('/')}/{Guid.NewGuid()}_{SanitiseFileName(fileName)}";

        var req = new TransferUtilityUploadRequest
        {
            BucketName  = BucketName,
            Key         = key,
            InputStream = stream,
            ContentType = contentType,
            CannedACL   = S3CannedACL.Private   // private — use pre-signed URLs to download
        };

        var transfer = new TransferUtility(client);
        await transfer.UploadAsync(req, ct);

        _log.LogInformation("Uploaded {Key} to S3 bucket {Bucket}", key, BucketName);

        // Return a pre-signed URL valid for 7 days (for initial display)
        var urlReq = new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key        = key,
            Expires    = DateTime.UtcNow.AddDays(7),
            Verb       = HttpVerb.GET
        };
        return client.GetPreSignedURL(urlReq);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var client = BuildClient();
        await client.DeleteObjectAsync(BucketName, key, ct);
        _log.LogInformation("Deleted S3 object {Key}", key);
    }

    private AmazonS3Client BuildClient()
    {
        var accessKey = _cfg["S3:AccessKey"]!;
        var secretKey = _cfg["S3:SecretKey"]!;
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Region ?? "ap-south-1");
        return new AmazonS3Client(accessKey, secretKey, regionEndpoint);
    }

    private static string SanitiseFileName(string name) =>
        System.Text.RegularExpressions.Regex.Replace(Path.GetFileName(name), @"[^a-zA-Z0-9.\-_]", "_");
}
