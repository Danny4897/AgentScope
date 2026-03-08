using System.Diagnostics;

namespace AgentScope.Sdk;

/// <summary>
/// Extension methods for <see cref="Activity"/> that tag Railway-Oriented Programming (ROP)
/// operations so AgentScope can visualise the Bind / Map / Then chain and pinpoint failures.
/// </summary>
public static class AgentScopeActivityExtensions
{
    /// <summary>Tags this span as a <c>Bind</c> monadic step.</summary>
    public static Activity? TagBind(this Activity? activity, bool isSuccess, string? errorCode = null)
        => TagRailway(activity, "Bind", isSuccess, errorCode);

    /// <summary>Tags this span as a <c>Map</c> projection step.</summary>
    public static Activity? TagMap(this Activity? activity, bool isSuccess, string? errorCode = null)
        => TagRailway(activity, "Map", isSuccess, errorCode);

    /// <summary>Tags this span as a <c>Then</c> sequencing step.</summary>
    public static Activity? TagThen(this Activity? activity, bool isSuccess, string? errorCode = null)
        => TagRailway(activity, "Then", isSuccess, errorCode);

    /// <summary>Tags this span as a <c>TryCatch</c> error boundary.</summary>
    public static Activity? TagTryCatch(this Activity? activity, bool isSuccess, string? errorCode = null)
        => TagRailway(activity, "TryCatch", isSuccess, errorCode);

    /// <summary>Tags this span as a <c>Match</c> terminal step.</summary>
    public static Activity? TagMatch(this Activity? activity, bool isSuccess, string? errorCode = null)
        => TagRailway(activity, "Match", isSuccess, errorCode);

    private static Activity? TagRailway(Activity? activity, string operation, bool isSuccess, string? errorCode)
    {
        if (activity is null) return null;
        activity.SetTag("agentscope.railway.operation", operation);
        activity.SetTag("agentscope.result.is_success", isSuccess ? "true" : "false");
        if (!isSuccess && errorCode is not null)
            activity.SetTag("agentscope.error.code", errorCode);
        return activity;
    }
}
