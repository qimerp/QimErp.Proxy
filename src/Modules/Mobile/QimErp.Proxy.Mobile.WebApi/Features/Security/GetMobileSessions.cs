using System.Text.Json;

namespace QimErp.Proxy.Mobile.WebApi.Features.Security;

public static class GetMobileSessions
{
    public class Query : IRequest<Result<JsonElement>> { }

    public class Handler(IPlatformDownstreamClient platformClient) : IRequestHandler<Query, Result<JsonElement>>
    {
        public Task<Result<JsonElement>> Handle(Query request, CancellationToken cancellationToken)
            => platformClient.GetSessionsAsync(cancellationToken);
    }
}

public class GetMobileSessionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(MobileApiConstants.Url.SecuritySessions,
                [Authorize] async (ISender sender) =>
                    (await sender.Send(new GetMobileSessions.Query())).ToIResult())
            .WithTags(MobileApiConstants.Tags.Security)
            .WithName("MobileSecuritySessions")
            .WithSummary("Mobile ESS active sessions");
    }
}
