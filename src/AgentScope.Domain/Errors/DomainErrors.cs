using System.Globalization;
using MonadicSharp;

namespace AgentScope.Domain.Errors;

/// <summary>
/// Typed domain error factory — all errors flow as <see cref="Result"/> failures,
/// never as thrown exceptions.
/// </summary>
public static class DomainErrors
{
    public static class Applications
    {
        public static Error NotFound(Guid id) =>
            Error.Create($"Application '{id}' not found.", "APPLICATION_NOT_FOUND")
                 .WithMetadata("ApplicationId", id.ToString());

        public static Error LimitReached(int limit) =>
            Error.Create($"Application limit of {limit.ToString(CultureInfo.InvariantCulture)} reached for this subscription tier.", "APPLICATION_LIMIT_REACHED")
                 .WithMetadata("Limit", limit.ToString(CultureInfo.InvariantCulture));

        public static Error DuplicateName(string name) =>
            Error.Create($"An application named '{name}' already exists.", "APPLICATION_DUPLICATE_NAME")
                 .WithMetadata("Name", name);
    }

    public static class Traces
    {
        public static Error NotFound(Guid id) =>
            Error.Create($"Trace '{id}' not found.", "TRACE_NOT_FOUND")
                 .WithMetadata("TraceId", id.ToString());

        public static Error RetentionExceeded(int retentionDays) =>
            Error.Create($"Trace is older than the {retentionDays.ToString(CultureInfo.InvariantCulture)}-day retention window.", "TRACE_RETENTION_EXCEEDED")
                 .WithMetadata("RetentionDays", retentionDays.ToString(CultureInfo.InvariantCulture));
    }

    public static class Subscriptions
    {
        public static Error NotFound(Guid userId) =>
            Error.Create($"No active subscription for user '{userId}'.", "SUBSCRIPTION_NOT_FOUND")
                 .WithMetadata("UserId", userId.ToString());

        public static Error EventQuotaExceeded(long limit) =>
            Error.Create($"Monthly event quota of {limit.ToString("N0", CultureInfo.InvariantCulture)} exceeded.", "SUBSCRIPTION_QUOTA_EXCEEDED")
                 .WithMetadata("MonthlyLimit", limit.ToString(CultureInfo.InvariantCulture));
    }

    public static class Users
    {
        public static Error NotFound(Guid id) =>
            Error.Create($"User '{id}' not found.", "USER_NOT_FOUND")
                 .WithMetadata("UserId", id.ToString());

        public static Error DuplicateEmail(string email) =>
            Error.Create($"A user with email '{email}' already exists.", "USER_DUPLICATE_EMAIL")
                 .WithMetadata("Email", email);
    }
}
