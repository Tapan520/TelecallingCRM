using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents").RequireAuthorization();

        // Upload document for a lead
        group.MapPost("/upload/{leadId:guid}", async (Guid leadId, IFormFile file,
            [FromForm] string type, TenantContext tc, AppDbContext db, HttpContext http,
            IWebHostEnvironment env, IS3StorageService s3) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".csv" };
            var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExt))
                return Results.BadRequest($"File type '{fileExt}' is not allowed. Allowed: {string.Join(", ", allowedExtensions)}");
            const long maxBytes = 10 * 1024 * 1024;
            if (file.Length > maxBytes)
                return Results.BadRequest("File size must not exceed 10 MB.");

            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId && l.TenantId == tc.TenantId);
            if (lead == null) return Results.NotFound();

            string fileUrl;
            if (s3.IsEnabled)
            {
                // Upload to S3
                await using var stream = file.OpenReadStream();
                fileUrl = await s3.UploadAsync(stream, file.FileName,
                    file.ContentType ?? "application/octet-stream",
                    $"tenants/{tc.TenantId}/documents");
            }
            else
            {
                // Fallback: local wwwroot/uploads
                var uploadDir = Path.Combine(env.WebRootPath, "uploads", tc.TenantId.ToString());
                Directory.CreateDirectory(uploadDir);
                var safeName = $"{Guid.NewGuid()}{fileExt}";
                var filePath = Path.Combine(uploadDir, safeName);
                await using var stream = File.Create(filePath);
                await file.CopyToAsync(stream);
                fileUrl = $"/uploads/{tc.TenantId}/{safeName}";
            }

            var docType = Enum.TryParse<DocumentType>(type, true, out var dt) ? dt : DocumentType.Other;
            var doc = new LeadDocument {
                TenantId = tc.TenantId, LeadId = leadId, UploadedById = userId,
                FileName = file.FileName, FileUrl = fileUrl,
                Type = docType, FileSizeBytes = file.Length
            };
            db.LeadDocuments.Add(doc);
            db.ActivityLogs.Add(new ActivityLog {
                TenantId = tc.TenantId, LeadId = leadId, UserId = userId,
                Type = ActivityType.DocumentUploaded,
                Summary = $"Document uploaded: {file.FileName} ({docType})"
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { doc.Id, doc.FileName, doc.FileUrl, doc.Type, doc.FileSizeBytes });
        }).DisableAntiforgery();

        // List documents for a lead
        group.MapGet("/lead/{leadId:guid}", async (Guid leadId, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var docs = await db.LeadDocuments
                .Where(d => d.LeadId == leadId && d.TenantId == tc.TenantId)
                .Include(d => d.UploadedBy)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new { d.Id, d.FileName, d.FileUrl, d.Type, d.FileSizeBytes, d.UploadedAt, UploadedBy = d.UploadedBy.FullName })
                .ToListAsync();
            return Results.Ok(docs);
        });

        // Delete document
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db,
            IWebHostEnvironment env, IS3StorageService s3) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var doc = await db.LeadDocuments.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tc.TenantId);
            if (doc == null) return Results.NotFound();

            if (s3.IsEnabled)
            {
                // Extract S3 key from pre-signed URL (path before '?')
                if (Uri.TryCreate(doc.FileUrl, UriKind.Absolute, out var uri))
                {
                    var key = uri.AbsolutePath.TrimStart('/');
                    await s3.DeleteAsync(key);
                }
            }
            else
            {
                var physicalPath = Path.Combine(env.WebRootPath,
                    doc.FileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(physicalPath)) File.Delete(physicalPath);
            }

            db.LeadDocuments.Remove(doc);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
