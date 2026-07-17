using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class DripSequenceEndpoints
{
    public static void MapDripSequenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drip").WithTags("DripAutomation").RequireAuthorization().RequireRateLimiting("api");

        // GET /api/drip
        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var seqs = await db.DripSequences
                .Where(s => s.TenantId == tc.TenantId)
                .Include(s => s.Steps)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new {
                    s.Id, s.Name, s.Trigger, s.IsActive, s.CampaignId, s.CreatedAt,
                    StepCount = s.Steps.Count,
                    EnrollmentCount = db.DripEnrollments.Count(e => e.SequenceId == s.Id),
                    CampaignName = s.Campaign != null ? s.Campaign.Name : null
                })
                .ToListAsync();
            return Results.Ok(seqs);
        });

        // GET /api/drip/{id}
        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var seq = await db.DripSequences
                .Include(s => s.Steps.OrderBy(st => st.StepOrder))
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (seq is null) return Results.NotFound();
            return Results.Ok(new {
                seq.Id, seq.Name, seq.Trigger, seq.IsActive, seq.CampaignId, seq.CreatedAt,
                steps = seq.Steps.Select(st => new {
                    st.Id, st.StepOrder, st.StepType, st.DelayDays, st.Payload
                }).ToList()
            });
        });

        // POST /api/drip
        group.MapPost("/", async ([FromBody] DripSequenceUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var seq = new DripSequence
            {
                TenantId = tc.TenantId,
                Name = dto.Name,
                Trigger = dto.Trigger,
                CampaignId = dto.CampaignId,
                IsActive = dto.IsActive
            };
            foreach (var (step, i) in dto.Steps.Select((s, i) => (s, i)))
            {
                seq.Steps.Add(new DripStep
                {
                    StepOrder = i,
                    StepType = step.StepType,
                    DelayDays = step.DelayDays,
                    Payload = step.Payload
                });
            }
            db.DripSequences.Add(seq);
            await db.SaveChangesAsync();
            return Results.Created($"/api/drip/{seq.Id}", new { seq.Id, seq.Name });
        });

        // PUT /api/drip/{id}
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] DripSequenceUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var seq = await db.DripSequences.Include(s => s.Steps)
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (seq is null) return Results.NotFound();
            seq.Name = dto.Name;
            seq.Trigger = dto.Trigger;
            seq.CampaignId = dto.CampaignId;
            seq.IsActive = dto.IsActive;
            db.DripSteps.RemoveRange(seq.Steps);
            foreach (var (step, i) in dto.Steps.Select((s, i) => (s, i)))
            {
                seq.Steps.Add(new DripStep
                {
                    StepOrder = i,
                    StepType = step.StepType,
                    DelayDays = step.DelayDays,
                    Payload = step.Payload
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { seq.Id, seq.Name });
        });

        // DELETE /api/drip/{id}
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var seq = await db.DripSequences.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (seq is null) return Results.NotFound();
            db.DripSequences.Remove(seq);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // POST /api/drip/{id}/enroll — enroll a lead
        group.MapPost("/{id:guid}/enroll", async (Guid id, [FromBody] DripEnrollDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var seq = await db.DripSequences.Include(s => s.Steps.OrderBy(st => st.StepOrder))
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId && s.IsActive);
            if (seq is null) return Results.NotFound();
            var exists = await db.DripEnrollments.AnyAsync(e => e.SequenceId == id && e.LeadId == dto.LeadId && e.Status == EnrollmentStatus.Active);
            if (exists) return Results.Conflict(new { error = "Lead already enrolled in this sequence." });
            var firstStep = seq.Steps.MinBy(s => s.StepOrder);
            var enrollment = new DripEnrollment
            {
                SequenceId = id,
                LeadId = dto.LeadId,
                TenantId = tc.TenantId,
                Status = EnrollmentStatus.Active,
                CurrentStep = 0,
                NextRunAt = DateTime.UtcNow.AddDays(firstStep?.DelayDays ?? 0)
            };
            db.DripEnrollments.Add(enrollment);
            await db.SaveChangesAsync();
            return Results.Ok(new { enrollment.Id, enrollment.NextRunAt });
        });

        // GET /api/drip/{id}/enrollments
        group.MapGet("/{id:guid}/enrollments", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var enrollments = await db.DripEnrollments
                .Where(e => e.SequenceId == id && e.TenantId == tc.TenantId)
                .Select(e => new {
                    e.Id, e.LeadId, e.Status, e.CurrentStep, e.EnrolledAt, e.NextRunAt,
                    LeadName = e.Lead.Name
                })
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();
            return Results.Ok(enrollments);
        });

        // POST /api/drip/enrollments/{eid}/cancel
        group.MapPost("/enrollments/{eid:guid}/cancel", async (Guid eid, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var en = await db.DripEnrollments.FirstOrDefaultAsync(e => e.Id == eid && e.TenantId == tc.TenantId);
            if (en is null) return Results.NotFound();
            en.Status = EnrollmentStatus.Cancelled;
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}

public record DripSequenceUpsertDto(string Name, AutomationTrigger Trigger, bool IsActive, Guid? CampaignId, List<DripStepDto> Steps);
public record DripStepDto(AutomationStepType StepType, int DelayDays, string Payload);
public record DripEnrollDto(Guid LeadId);
