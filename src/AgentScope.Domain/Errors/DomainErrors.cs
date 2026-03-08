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
    
    public static class Auth
    {
        public static Error EmailInUse() =>
            Error.Create("Email already in use.", "AUTH_EMAIL_IN_USE");

        public static Error CreationError(string errors) =>
            Error.Create(errors, "AUTH_CREATION_ERROR");

        public static Error InvalidCredentials() =>
            Error.Create("Invalid email or password.", "AUTH_INVALID_CREDENTIALS");

        public static Error UserNotFound() =>
            Error.Create("Domain user not found.", "AUTH_USER_NOT_FOUND");
    }

    public static class Railway
    {
        public static Error OperationFailed(string operation, string errorCode) =>
            Error.Create($"Railway operation '{operation}' failed with '{errorCode}'.", "RAILWAY_OPERATION_FAILED")
                 .WithMetadata("Operation", operation)
                 .WithMetadata("ErrorCode", errorCode);
    }

    public static class Billing
    {
        public static Error UnknownModel(string model) =>
            Error.Create($"No pricing data for model '{model}'.", "BILLING_UNKNOWN_MODEL")
                 .WithMetadata("Model", model);
    }

    public static class Prompts
    {
        public static Error NotFound(string slug) =>
            Error.Create($"Prompt '{slug}' not found.", "PROMPT_NOT_FOUND")
                 .WithMetadata("Slug", slug);

        public static Error VersionNotFound(string slug, int version) =>
            Error.Create($"Prompt '{slug}' version {version.ToString(System.Globalization.CultureInfo.InvariantCulture)} not found.", "PROMPT_VERSION_NOT_FOUND")
                 .WithMetadata("Slug", slug)
                 .WithMetadata("Version", version.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public static Error NoActiveVersion(string slug) =>
            Error.Create($"No active version for prompt '{slug}'.", "PROMPT_NO_ACTIVE_VERSION")
                 .WithMetadata("Slug", slug);

        public static Error SlugConflict(string slug) =>
            Error.Create($"A prompt with slug '{slug}' already exists. Use PUT to publish a new version.", "PROMPT_SLUG_CONFLICT")
                 .WithMetadata("Slug", slug);

        public static Error EmptyContent() =>
            Error.Create("Prompt content cannot be empty.", "PROMPT_EMPTY_CONTENT");
    }
}
