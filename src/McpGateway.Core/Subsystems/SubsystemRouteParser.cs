using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace McpGateway.Core.Subsystems;

/// <summary>
/// Utility class for resolving subsystem identifiers from HTTP requests.
/// </summary>
public static class SubsystemRouteParser
{
    private static readonly Regex SubsystemRegex = new(
        @"^/(?:[a-zA-Z0-9_-]+/)?(?<system>[a-zA-Z0-9_-]+)/mcp(?:/.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts the subsystem identifier from the HTTP request context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="defaultDepartment">The configured gateway department, if any.</param>
    /// <returns>The normalized subsystem name, or null if the request is not directed at a subsystem.</returns>
    public static string? ExtractSubsystem(HttpContext context, string? defaultDepartment = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Try ASP.NET Core RouteValues (e.g. from /{dept}/{system}/mcp template)
        if (context.Request.RouteValues.TryGetValue("system", out var systemVal) &&
            systemVal is string systemStr &&
            !string.IsNullOrWhiteSpace(systemStr))
        {
            return systemStr.Trim().ToLowerInvariant();
        }

        // 2. Parse request path segments
        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;

        var mcpIndex = Array.FindLastIndex(segments, s => s.Equals("mcp", StringComparison.OrdinalIgnoreCase));
        if (mcpIndex > 0)
        {
            var candidate = segments[mcpIndex - 1].Trim();

            // If only one prefix segment before mcp (e.g. /mfg/mcp)
            if (mcpIndex == 1)
            {
                // If it matches the department name, it is a department root endpoint, not a subsystem
                if (!string.IsNullOrEmpty(defaultDepartment) &&
                    candidate.Equals(defaultDepartment.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return candidate.ToLowerInvariant();
            }

            // Two or more segments before mcp (e.g. /mfg/mes/mcp or /mfg/systems/mes/mcp)
            return candidate.ToLowerInvariant();
        }

        // 3. Fallback to regex
        var match = SubsystemRegex.Match(path);
        if (match.Success)
        {
            var matchedSys = match.Groups["system"].Value.Trim();
            if (!string.IsNullOrEmpty(defaultDepartment) &&
                matchedSys.Equals(defaultDepartment.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return matchedSys.ToLowerInvariant();
        }

        return null;
    }
}
