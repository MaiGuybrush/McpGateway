using System;
using System.Net;

namespace McpGateway.Core.Observability;

/// <summary>
/// Builds error responses with masked details for security.
/// Returns semantic messages + CorrelationId to clients while logging full details internally.
/// </summary>
public static class ErrorResponseBuilder
{
    /// <summary>
    /// Builds a masked error response for unexpected tool execution failures.
    /// </summary>
    public static ErrorResponse BuildToolExecutionError(string correlationId)
    {
        return new ErrorResponse(
            Message: $"工具執行失敗，請聯繫支援並提供 ID: {correlationId}",
            CorrelationId: correlationId,
            StatusCode: (int)HttpStatusCode.InternalServerError
        );
    }

    /// <summary>
    /// Builds a masked error response for downstream 5xx or timeout errors.
    /// </summary>
    public static ErrorResponse BuildDownstreamServiceError(string correlationId)
    {
        return new ErrorResponse(
            Message: "下游服務暫時無法使用，請稍後再試",
            CorrelationId: correlationId,
            StatusCode: (int)HttpStatusCode.ServiceUnavailable
        );
    }

    /// <summary>
    /// Builds a semantic error response for downstream 4xx errors (without exposing raw error).
    /// </summary>
    public static ErrorResponse BuildDownstreamClientError(string message, string correlationId)
    {
        return new ErrorResponse(
            Message: message,
            CorrelationId: correlationId,
            StatusCode: (int)HttpStatusCode.BadRequest
        );
    }

    /// <summary>
    /// Builds an error response for authentication failures.
    /// </summary>
    public static ErrorResponse BuildAuthenticationError(string correlationId)
    {
        return new ErrorResponse(
            Message: "認證失敗",
            CorrelationId: correlationId,
            StatusCode: (int)HttpStatusCode.Unauthorized
        );
    }

    /// <summary>
    /// Builds a full error response for validation failures (parameter validation).
    /// Validation errors are not masked to help LLM self-correct.
    /// </summary>
    public static ErrorResponse BuildValidationError(string message, string correlationId, object? details = null)
    {
        return new ErrorResponse(
            Message: message,
            CorrelationId: correlationId,
            StatusCode: (int)HttpStatusCode.BadRequest,
            Details: details
        );
    }
}

/// <summary>
/// Error response structure returned to clients.
/// </summary>
public record ErrorResponse(
    string Message,
    string CorrelationId,
    int StatusCode,
    object? Details = null
);