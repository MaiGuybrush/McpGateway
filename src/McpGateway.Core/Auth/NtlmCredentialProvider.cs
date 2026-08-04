using Microsoft.Extensions.Logging;
using System;

namespace McpGateway.Core.Auth;

/// <summary>
/// Provides NTLM credentials from environment variables.
/// Reads NTLM_USERNAME and NTLM_PASSWORD from environment.
/// </summary>
public class NtlmCredentialProvider
{
    private readonly ILogger<NtlmCredentialProvider> _logger;
    private readonly string _username;
    private readonly string _password;

    public NtlmCredentialProvider(ILogger<NtlmCredentialProvider> logger)
    {
        _logger = logger;
        
        _username = Environment.GetEnvironmentVariable("NTLM_USERNAME") 
            ?? throw new InvalidOperationException(
                "NTLM authentication is enabled but NTLM_USERNAME environment variable is not set");
        
        _password = Environment.GetEnvironmentVariable("NTLM_PASSWORD") 
            ?? throw new InvalidOperationException(
                "NTLM authentication is enabled but NTLM_PASSWORD environment variable is not set");
        
        if (string.IsNullOrWhiteSpace(_username))
        {
            throw new InvalidOperationException("NTLM_USERNAME environment variable cannot be empty");
        }
        
        if (string.IsNullOrWhiteSpace(_password))
        {
            throw new InvalidOperationException("NTLM_PASSWORD environment variable cannot be empty");
        }
        
        _logger.LogInformation("NTLM credentials loaded from environment variables for user: {Username}", _username);
    }

    public string Username => _username;
    public string Password => _password;
    
    public (string Username, string Password) GetCredentials()
    {
        return (_username, _password);
    }
}