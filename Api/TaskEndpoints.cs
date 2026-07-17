using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
using System.Security.Claims;

namespace TelecallingCRM.Api;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] string? status, [FromQuery] bool myTasks = false,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var query = db.Tasks
                .Where(t => t.TenantId == tc.TenantId)
                .Include(t => t.AssignedTo).Include(t => t.Lead)
                .AsQueryable();

            if (myTasks) query = query.Where(t => t.AssignedToId == userId);
            if (Enum.TryParse<TelecallingCRM.Data.Models.TaskStatus>(status, true, out var ts))
                query = query.Where(t => t.Status == ts);

            // Auto-flag overdue tasks (on full set before paging)
            var now = DateTime.UtcNow;
            var allMatchingTasks = await query.OrderBy(t => t.DueAt).ToListAsync();
            foreach (var t in allMatchingTasks.Where(t => t.Status == TelecallingCRM.Data.Models.TaskStatus.Pending && t.DueAt < now))
                t.Status = TelecallingCRM.Data.Models.TaskStatus.Overdue;
            await db.SaveChangesAsync();

            var total = allMatchingTasks.Count;
            var paged = allMatchingTasks.Skip((page - 1) * pageSize).Take(pageSize);

            return Results.Ok(new {
                total, page, pageSize,
                tasks = paged.Select(t => new {
                    t.Id, t.Title, t.Description, t.Priority, t.Status, t.DueAt, t.CompletedAt,
                    AssignedTo = t.AssignedTo.FullName,
                    LeadName = t.Lead?.Name
                })
            });
        });

        group.MapPost("/", async ([FromBody] TaskUpsertDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var task = new TaskItem {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                AssignedToId = dto.AssignedToId ?? userId,
                CreatedById = userId,
                Title = dto.Title,
                Description = dto.Description,
                Priority = dto.Priority,
                DueAt = dto.DueAt
            };
            db.Tasks.Add(task);

            if (dto.LeadId.HasValue)
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = dto.LeadId.Value, UserId = userId,
                    Type = ActivityType.TaskCreated, Summary = $"Task created: {dto.Title}"
                });

            // Notify assignee (if different from creator)
            var assigneeId = dto.AssignedToId ?? userId;
            if (assigneeId != userId)
                db.Notifications.Add(new Notification {
                    TenantId = tc.TenantId, UserId = assigneeId,
                    Type = NotificationType.NewLeadAssigned,
                    Title = "New Task Assigned",
                    Body = $"You have been assigned: \"{dto.Title}\" due {dto.DueAt:dd MMM HH:mm}.",
                    Link = "/Tasks"
                });

            await db.SaveChangesAsync();
            return Results.Created($"/api/tasks/{task.Id}", task);
        });

        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var task = await db.Tasks
                .Include(t => t.AssignedTo).Include(t => t.Lead)
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (task == null) return Results.NotFound();
            return Results.Ok(new {
                task.Id, task.Title, task.Description, task.Priority, task.Status,
                task.DueAt, task.CompletedAt, task.LeadId, task.AssignedToId,
                AssignedTo = task.AssignedTo.FullName, LeadName = task.Lead?.Name
            });
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] TaskUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (task == null) return Results.NotFound();
            task.Title = dto.Title;
            task.Description = dto.Description;
            task.Priority = dto.Priority;
            task.DueAt = dto.DueAt;
            if (dto.LeadId.HasValue) task.LeadId = dto.LeadId;
            if (dto.AssignedToId.HasValue) task.AssignedToId = dto.AssignedToId.Value;
            await db.SaveChangesAsync();
            return Results.Ok(new { task.Id, task.Title, task.Status });
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (task == null) return Results.NotFound();
            task.Status = TelecallingCRM.Data.Models.TaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;

            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            if (task.LeadId.HasValue)
                db.ActivityLogs.Add(new ActivityLog {
                    TenantId = tc.TenantId, LeadId = task.LeadId.Value, UserId = userId,
                    Type = ActivityType.TaskCompleted, Summary = $"Task completed: {task.Title}"
                });

            // Fire TaskCompleted webhook
            var dispatcher = http.RequestServices.GetRequiredService<IWebhookDispatcher>();
            Hangfire.BackgroundJob.Enqueue(() => dispatcher.FireAsync(
                tc.TenantId, WebhookEvent.TaskCompleted,
                new { taskId = id, task.Title, task.LeadId, completedBy = userId }));

            await db.SaveChangesAsync();
            return Results.Ok(new { task.Status, task.CompletedAt });
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (task == null) return Results.NotFound();
            db.Tasks.Remove(task);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // POST /api/tasks/{id}/comments
        group.MapPost("/{id:guid}/comments", async (Guid id, [FromBody] TaskCommentDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (task == null) return Results.NotFound();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var comment = new TaskComment { TaskId = id, UserId = userId, Body = dto.Body };
            db.TaskComments.Add(comment);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tasks/{id}/comments/{comment.Id}", new { comment.Id, comment.Body, comment.CreatedAt });
        });

        // GET /api/tasks/{id}/comments
        group.MapGet("/{id:guid}/comments", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var exists = await db.Tasks.AnyAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (!exists) return Results.NotFound();
            var comments = await db.TaskComments
                .Where(c => c.TaskId == id)
                .Include(c => c.User)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.Body, c.CreatedAt, By = c.User.FullName })
                .ToListAsync();
            return Results.Ok(comments);
        });
    }
}

public record TaskUpsertDto(
    string Title, string? Description, TaskPriority Priority,
    DateTime DueAt, Guid? LeadId, Guid? AssignedToId);

public record TaskCommentDto(string Body);
