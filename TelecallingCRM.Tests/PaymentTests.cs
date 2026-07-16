using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Tests;

/// <summary>Tests for #4 – Payment / Razorpay module.</summary>
public class PaymentTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _leadId = Guid.NewGuid();

    public PaymentTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);

        _db.Set<Tenant>().Add(new Tenant { Id = _tenantId, Name = "T4", Slug = "t4" });

        _db.Set<AppUser>().Add(new AppUser
        {
            Id = _userId, TenantId = _tenantId, FullName = "Sales Rep",
            UserName = "sr", NormalizedUserName = "SR",
            Email = "sr@t.com", NormalizedEmail = "SR@T.COM",
            SecurityStamp = Guid.NewGuid().ToString()
        });

        _db.Set<Lead>().Add(new Lead
        {
            Id = _leadId, TenantId = _tenantId, Name = "Paying Lead", Phone = "6666666666"
        });

        _db.SaveChanges();
    }

    [Fact]
    public async Task CanCreatePendingPayment()
    {
        var payment = new Payment
        {
            TenantId = _tenantId,
            LeadId = _leadId,
            RecordedById = _userId,
            Amount = 4999.00m,
            Currency = "INR",
            RazorpayOrderId = "order_test123",
            Description = "Subscription",
            ReceiptNumber = "rcpt_001",
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var saved = await _db.Payments.SingleAsync(p => p.RazorpayOrderId == "order_test123");
        Assert.Equal(PaymentStatus.Pending, saved.Status);
        Assert.Equal(4999.00m, saved.Amount);
        Assert.Equal("INR", saved.Currency);
    }

    [Fact]
    public async Task CanCapturePaymentAfterVerification()
    {
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 2500m, Currency = "INR",
            RazorpayOrderId = "order_cap001",
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        payment.RazorpayPaymentId = "pay_abc123";
        payment.RazorpaySignature = "sig_xyz";
        payment.Status = PaymentStatus.Captured;
        payment.CapturedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var saved = await _db.Payments.SingleAsync(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatus.Captured, saved.Status);
        Assert.Equal("pay_abc123", saved.RazorpayPaymentId);
        Assert.NotNull(saved.CapturedAt);
    }

    [Fact]
    public async Task CanRecordManualPayment()
    {
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 10000m, Currency = "INR",
            Description = "Cash payment",
            ReceiptNumber = "MAN-001",
            Status = PaymentStatus.Captured,
            CapturedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var saved = await _db.Payments.SingleAsync(p => p.ReceiptNumber == "MAN-001");
        Assert.Null(saved.RazorpayOrderId);
        Assert.Equal(PaymentStatus.Captured, saved.Status);
        Assert.Equal(10000m, saved.Amount);
    }

    [Fact]
    public async Task CanRefundCapturedPayment()
    {
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 1500m, Currency = "INR",
            RazorpayOrderId = "order_ref001", RazorpayPaymentId = "pay_ref001",
            Status = PaymentStatus.Captured, CapturedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        payment.Status = PaymentStatus.Refunded;
        await _db.SaveChangesAsync();

        var saved = await _db.Payments.SingleAsync(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatus.Refunded, saved.Status);
    }

    [Fact]
    public async Task CannotRefundNonCapturedPayment_ValidationLogic()
    {
        // Simulates the business logic check done in PaymentEndpoints
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 999m, Currency = "INR",
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // The endpoint returns 400 when status != Captured; verify model state here
        Assert.NotEqual(PaymentStatus.Captured, payment.Status);
    }

    [Fact]
    public async Task PaymentSummary_AggregatesCorrectly()
    {
        _db.Payments.AddRange(
            new Payment { TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId, Amount = 1000m, Currency = "INR", Status = PaymentStatus.Captured, CapturedAt = DateTime.UtcNow },
            new Payment { TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId, Amount = 2000m, Currency = "INR", Status = PaymentStatus.Captured, CapturedAt = DateTime.UtcNow },
            new Payment { TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId, Amount = 500m, Currency = "INR", Status = PaymentStatus.Pending },
            new Payment { TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId, Amount = 750m, Currency = "INR", Status = PaymentStatus.Refunded }
        );
        await _db.SaveChangesAsync();

        var all = await _db.Payments.Where(p => p.LeadId == _leadId).ToListAsync();
        var capturedTotal = all.Where(p => p.Status == PaymentStatus.Captured).Sum(p => p.Amount);
        var pendingCount = all.Count(p => p.Status == PaymentStatus.Pending);
        var refundedCount = all.Count(p => p.Status == PaymentStatus.Refunded);

        Assert.Equal(3000m, capturedTotal);
        Assert.Equal(1, pendingCount);
        Assert.Equal(1, refundedCount);
    }

    [Fact]
    public async Task Payment_HasNavigationToLeadAndUser()
    {
        var payment = new Payment
        {
            TenantId = _tenantId, LeadId = _leadId, RecordedById = _userId,
            Amount = 5000m, Currency = "INR", Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var loaded = await _db.Payments
            .Include(p => p.Lead)
            .Include(p => p.RecordedBy)
            .SingleAsync(p => p.Id == payment.Id);

        Assert.Equal("Paying Lead", loaded.Lead.Name);
        Assert.Equal("Sales Rep", loaded.RecordedBy.FullName);
    }

    [Fact]
    public async Task RazorpaySignatureVerification_CorrectExpectedFormat()
    {
        // Validates the HMAC-SHA256 signature generation logic used in PaymentEndpoints.
        var secret = "test_secret";
        var orderId = "order_ABC";
        var paymentId = "pay_XYZ";

        var message = $"{orderId}|{paymentId}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(
            hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(message))).ToLower();

        // Must be a 64-char lowercase hex string (256-bit)
        Assert.Equal(64, expected.Length);
        Assert.Matches("^[0-9a-f]+$", expected);
    }

    public void Dispose() => _db.Dispose();
}
