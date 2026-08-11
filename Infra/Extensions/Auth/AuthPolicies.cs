namespace Infra.Extensions.Auth;

/// <summary>
/// Central registry of authorization policy names. Add new policies here alongside their
/// registration in <see cref="AuthExtension.AddInfraAuth"/> so callers (e.g. <c>[Authorize(Policy = ...)]</c>
/// in Api) never hand-roll a policy name string.
/// </summary>
public static class AuthPolicies
{
    public const string RequireAdmin = "RequireAdmin";
}
