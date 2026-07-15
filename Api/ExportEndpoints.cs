using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/export").WithTags("Export").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/leads/csv", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? status, [FromQuery] string? source) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var leads = await GetLeadsQuery(tc, db, status, source).ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("Name,Phone,AlternatePhone,Email,Company,Industry,City,State,Status,Priority,Source,AiScore,AssignedTo,Campaign,NextFollowUp,CreatedAt");
            foreach (var l in leads)
                csv.AppendLine($"\"{l.Name}\",\"{l.Phone}\",\"{l.AlternatePhone}\",\"{l.Email}\",\"{l.Company}\"," +
                               $"\"{l.Industry}\",\"{l.City}\",\"{l.State}\"," +
                               $"{l.Status},{l.Priority},\"{l.Source}\",{l.AiScore}," +
                               $"\"{l.AssignedTo?.FullName}\",\"{l.Campaign?.Name}\"," +
                               $"\"{l.NextFollowUpAt:yyyy-MM-dd}\",\"{l.CreatedAt:yyyy-MM-dd}\"");
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"leads_{DateTime.UtcNow:yyyyMMdd}.csv");
        });

        group.MapGet("/leads/xlsx", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? status, [FromQuery] string? source) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var leads = await GetLeadsQuery(tc, db, status, source).ToListAsync();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Leads");
            string[] hdrs = ["Name","Phone","Alt Phone","Email","Company","Industry","City","State","Status","Priority","Source","AI Score","Assigned To","Campaign","Next Follow-up","Created"];
            for (var i = 0; i < hdrs.Length; i++) { ws.Cell(1, i + 1).Value = hdrs[i]; ws.Cell(1, i + 1).Style.Font.Bold = true; }
            for (var r = 0; r < leads.Count; r++)
            {
                var l = leads[r]; var row = r + 2;
                ws.Cell(row,1).Value=l.Name; ws.Cell(row,2).Value=l.Phone; ws.Cell(row,3).Value=l.AlternatePhone??string.Empty;
                ws.Cell(row,4).Value=l.Email??string.Empty; ws.Cell(row,5).Value=l.Company??string.Empty;
                ws.Cell(row,6).Value=l.Industry??string.Empty; ws.Cell(row,7).Value=l.City??string.Empty;
                ws.Cell(row,8).Value=l.State??string.Empty; ws.Cell(row,9).Value=l.Status.ToString();
                ws.Cell(row,10).Value=l.Priority; ws.Cell(row,11).Value=l.Source??string.Empty;
                ws.Cell(row,12).Value=l.AiScore; ws.Cell(row,13).Value=l.AssignedTo?.FullName??string.Empty;
                ws.Cell(row,14).Value=l.Campaign?.Name??string.Empty;
                ws.Cell(row,15).Value=l.NextFollowUpAt?.ToString("yyyy-MM-dd")??string.Empty;
                ws.Cell(row,16).Value=l.CreatedAt.ToString("yyyy-MM-dd");
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"leads_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        });

        group.MapGet("/calls/csv", async (TenantContext tc, AppDbContext db, [FromQuery] int days = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var calls = await GetCallsQuery(tc, db, days).ToListAsync();
            var csv = new StringBuilder();
            csv.AppendLine("Lead,Phone,Agent,StartedAt,DurationSeconds,Outcome,Direction,AiSentiment,Notes");
            foreach (var c in calls)
                csv.AppendLine($"\"{c.Lead.Name}\",\"{c.Lead.Phone}\",\"{c.Agent.FullName}\"," +
                               $"\"{c.StartedAt:yyyy-MM-dd HH:mm}\",{c.DurationSeconds},{c.Outcome}," +
                               $"{c.Direction},\"{c.AiSentiment}\",\"{c.Notes?.Replace("\"", "'")}\"");
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"calls_{DateTime.UtcNow:yyyyMMdd}.csv");
        });

        group.MapGet("/calls/xlsx", async (TenantContext tc, AppDbContext db, [FromQuery] int days = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var calls = await GetCallsQuery(tc, db, days).ToListAsync();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Calls");
            string[] hdrs = ["Lead","Phone","Agent","Started At","Duration (s)","Outcome","Direction","AI Sentiment","AI Summary","Notes"];
            for (var i = 0; i < hdrs.Length; i++) { ws.Cell(1, i + 1).Value = hdrs[i]; ws.Cell(1, i + 1).Style.Font.Bold = true; }
            for (var r = 0; r < calls.Count; r++)
            {
                var c = calls[r]; var row = r + 2;
                ws.Cell(row,1).Value=c.Lead.Name; ws.Cell(row,2).Value=c.Lead.Phone;
                ws.Cell(row,3).Value=c.Agent.FullName; ws.Cell(row,4).Value=c.StartedAt.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row,5).Value=c.DurationSeconds; ws.Cell(row,6).Value=c.Outcome.ToString();
                ws.Cell(row,7).Value=c.Direction.ToString(); ws.Cell(row,8).Value=c.AiSentiment??string.Empty;
                ws.Cell(row,9).Value=c.AiSummary??string.Empty; ws.Cell(row,10).Value=c.Notes??string.Empty;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"calls_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        });

        group.MapGet("/agents/csv", async (TenantContext tc, AppDbContext db, [FromQuery] int days = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var data = await GetAgentPerf(tc, db, days);
            var csv = new StringBuilder();
            csv.AppendLine("Agent,TotalCalls,Connected,TalkTimeSeconds,Converted,ConversionRate%");
            foreach (var a in data)
            {
                var rate = a.TotalCalls > 0 ? Math.Round((double)a.Converted / a.TotalCalls * 100, 1) : 0;
                csv.AppendLine($"\"{a.FullName}\",{a.TotalCalls},{a.Connected},{a.TalkSeconds},{a.Converted},{rate}");
            }
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"agents_{DateTime.UtcNow:yyyyMMdd}.csv");
        });

        group.MapGet("/agents/xlsx", async (TenantContext tc, AppDbContext db, [FromQuery] int days = 30) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var data = await GetAgentPerf(tc, db, days);
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Agents");
            string[] hdrs = ["Agent","Total Calls","Connected","Talk Time (s)","Converted","Conv. Rate %"];
            for (var i = 0; i < hdrs.Length; i++) { ws.Cell(1, i + 1).Value = hdrs[i]; ws.Cell(1, i + 1).Style.Font.Bold = true; }
            for (var r = 0; r < data.Count; r++)
            {
                var a = data[r]; var row = r + 2;
                var rate = a.TotalCalls > 0 ? Math.Round((double)a.Converted / a.TotalCalls * 100, 1) : 0;
                ws.Cell(row,1).Value=a.FullName; ws.Cell(row,2).Value=a.TotalCalls;
                ws.Cell(row,3).Value=a.Connected; ws.Cell(row,4).Value=a.TalkSeconds;
                ws.Cell(row,5).Value=a.Converted; ws.Cell(row,6).Value=rate;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"agents_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        });
    }

    private static IQueryable<Lead> GetLeadsQuery(TenantContext tc, AppDbContext db, string? status, string? source)
    {
        var q = db.Leads.Where(l => l.TenantId == tc.TenantId).Include(l => l.AssignedTo).Include(l => l.Campaign).AsQueryable();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<LeadStatus>(status, true, out var ls)) q = q.Where(l => l.Status == ls);
        if (!string.IsNullOrEmpty(source)) q = q.Where(l => l.Source == source);
        return q.OrderByDescending(l => l.CreatedAt);
    }

    private static IQueryable<Call> GetCallsQuery(TenantContext tc, AppDbContext db, int days)
        => db.Calls.Where(c => c.TenantId == tc.TenantId && c.StartedAt >= DateTime.UtcNow.AddDays(-days))
                   .Include(c => c.Lead).Include(c => c.Agent).OrderByDescending(c => c.StartedAt);

    private static async Task<List<AgentPerfRow>> GetAgentPerf(TenantContext tc, AppDbContext db, int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await db.Calls
            .Where(c => c.TenantId == tc.TenantId && c.StartedAt >= since)
            .GroupBy(c => c.AgentId)
            .Select(g => new AgentPerfRow {
                AgentId = g.Key,
                TotalCalls = g.Count(),
                Connected = g.Count(c => c.DurationSeconds > 10),
                TalkSeconds = g.Sum(c => c.DurationSeconds),
                Converted = g.Count(c => c.Outcome == CallOutcome.Converted)
            })
            .Join(db.Users, a => a.AgentId, u => u.Id, (a, u) => new AgentPerfRow {
                FullName = u.FullName, TotalCalls = a.TotalCalls, Connected = a.Connected,
                TalkSeconds = a.TalkSeconds, Converted = a.Converted
            })
            .OrderByDescending(x => x.TotalCalls)
            .ToListAsync();
    }
}

internal record AgentPerfRow
{
    public Guid AgentId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public int TotalCalls { get; init; }
    public int Connected { get; init; }
    public int TalkSeconds { get; init; }
    public int Converted { get; init; }
}
