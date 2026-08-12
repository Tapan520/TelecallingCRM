namespace TelecallingCRM.Services;

public static class PhoneNumberHelper
{
    /// <summary>
    /// Returns the phone number masked if PhoneMaskingEnabled is true for the tenant
    /// and the caller's role is NOT admin or superadmin.
    /// Format: first 5 digits visible, rest replaced with '*'.
    /// e.g. "9876543210" ? "98765*****"
    /// </summary>
    public static string? Mask(string? phone, string role, bool tenantMaskingEnabled)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return phone;

        // Masking is off for this tenant — everyone sees the full number
        if (!tenantMaskingEnabled)
            return phone;

        // Admin and SuperAdmin always see the full number
        if (role is "admin" or "superadmin")
            return phone;

        // Manager and Agent: show first 5 chars, mask the rest with '*'
        if (phone.Length <= 5)
            return new string('*', phone.Length);

        return phone[..5] + new string('*', phone.Length - 5);
    }
}
