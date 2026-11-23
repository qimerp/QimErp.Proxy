using QimErp.Proxy.Mobile.Shared.Contracts;
using QimErp.Shared.Common.Extensions;

namespace QimErp.Proxy.Mobile.Shared.Clients;

public interface IPlatformDownstreamClient
{
    Task<Result<JsonElement>> LoginAsync(object body, CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> RefreshAsync(object body, CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> GetMeAsync(CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> GetTenantConfigAsync(string domain, CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> CompleteTwoFactorLoginAsync(object body, CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> RevokeSessionAsync(object body, CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> RevokeAllSessionsAsync(CancellationToken cancellationToken = default);
    Task<Result<JsonElement>> ChangePasswordAsync(object body, CancellationToken cancellationToken = default);
    Task<Result<PaginatedList<AuditActivityResponse>>> GetMyActivitiesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}

public sealed class PlatformDownstreamClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<PlatformDownstreamClient> logger)
    : DownstreamHttpClientBase(httpClientFactory, httpContextAccessor, logger), IPlatformDownstreamClient
{
    protected override string ClientName => DownstreamClientNames.Platform;

    public Task<Result<JsonElement>> LoginAsync(object body, CancellationToken cancellationToken = default)
        => PostRawAsync(MobileApiConstants.Downstream.PlatformLogin, body, cancellationToken);

    public Task<Result<JsonElement>> RefreshAsync(object body, CancellationToken cancellationToken = default)
        => PostRawAsync(MobileApiConstants.Downstream.PlatformRefresh, body, cancellationToken);

    public Task<Result<JsonElement>> GetMeAsync(CancellationToken cancellationToken = default)
        => GetRawAsync(MobileApiConstants.Downstream.PlatformMe, cancellationToken);

    public Task<Result<JsonElement>> GetTenantConfigAsync(string domain, CancellationToken cancellationToken = default)
        => GetRawAsync(string.Format(MobileApiConstants.Downstream.PlatformTenantConfig, domain), cancellationToken);

    public Task<Result<JsonElement>> CompleteTwoFactorLoginAsync(object body, CancellationToken cancellationToken = default)
        => PostRawAsync(MobileApiConstants.Downstream.PlatformCompleteTwoFactor, body, cancellationToken);

    public Task<Result<JsonElement>> GetSessionsAsync(CancellationToken cancellationToken = default)
        => GetRawAsync(MobileApiConstants.Downstream.PlatformSessions, cancellationToken);

    public Task<Result<JsonElement>> RevokeSessionAsync(object body, CancellationToken cancellationToken = default)
        => PostRawAsync(MobileApiConstants.Downstream.PlatformSessionsRevoke, body, cancellationToken);

    public Task<Result<JsonElement>> RevokeAllSessionsAsync(CancellationToken cancellationToken = default)
        => PostRawAsync(MobileApiConstants.Downstream.PlatformSessionsRevokeAll, null, cancellationToken);

    public Task<Result<JsonElement>> ChangePasswordAsync(object body, CancellationToken cancellationToken = default)
        => PostRawAsync(MobileApiConstants.Downstream.PlatformChangePassword, body, cancellationToken);

    public Task<Result<PaginatedList<AuditActivityResponse>>> GetMyActivitiesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => GetAsync<PaginatedList<AuditActivityResponse>>(
            $"{MobileApiConstants.Downstream.PlatformMyActivities}?pageNumber={pageNumber}&pageSize={pageSize}",
            cancellationToken);
}
