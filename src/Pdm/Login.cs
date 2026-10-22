using System.IdentityModel.Tokens.Jwt;
using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;

namespace Pdm;

// A logged-in user: who they are and short-lived AWS credentials for their role.
public sealed class Session
{
    public required string Email { get; init; }
    public required string Name { get; init; }
    public required bool SharedEditor { get; init; }
    public required AWSCredentials Credentials { get; init; }
}

public static class Login
{
    // Email and password login (SRP, so the password never leaves the PC).
    // askNewPassword is called when Cognito requires a new password (first login).
    public static async Task<Session> RunAsync(Settings s, string email, string password, Func<string> askNewPassword)
    {
        var region = RegionEndpoint.GetBySystemName(s.Region);
        var provider = new AmazonCognitoIdentityProviderClient(new AnonymousAWSCredentials(), region);
        var pool = new CognitoUserPool(s.UserPoolId, s.AppClientId, provider);
        var user = new CognitoUser(email, s.AppClientId, pool, provider);

        var response = await user.StartWithSrpAuthAsync(new InitiateSrpAuthRequest { Password = password });
        if (response.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
        {
            response = await user.RespondToNewPasswordRequiredAsync(new RespondToNewPasswordRequiredRequest
            {
                SessionID = response.SessionID,
                NewPassword = askNewPassword()
            });
        }
        if (response.AuthenticationResult == null)
            throw new InvalidOperationException($"Login needs a step this app doesn't handle yet: {response.ChallengeName}");

        var idToken = response.AuthenticationResult.IdToken;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(idToken).Claims.ToList();
        var groups = claims.Where(c => c.Type == "cognito:groups").Select(c => c.Value).ToList();

        var credentials = new CognitoAWSCredentials(s.IdentityPoolId, region);
        credentials.AddLogin($"cognito-idp.{s.Region}.amazonaws.com/{s.UserPoolId}", idToken);

        return new Session
        {
            Email = claims.FirstOrDefault(c => c.Type == "email")?.Value ?? email,
            Name = claims.FirstOrDefault(c => c.Type == "name")?.Value ?? email,
            SharedEditor = groups.Contains("shared-editors"),
            Credentials = credentials
        };
    }
}
