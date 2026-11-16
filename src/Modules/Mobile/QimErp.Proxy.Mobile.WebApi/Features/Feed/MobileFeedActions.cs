using System.Text.Json;

namespace QimErp.Proxy.Mobile.WebApi.Features.Feed;

/// <summary>
/// Mobile passthroughs for company-news engagement: mark a post read, toggle a
/// reaction (like), and list/create public comments — all forwarded to the
/// CoreHr People news module with the caller's JWT.
/// </summary>
public static class MobileFeedActions
{
    public record MarkReadQuery(Guid NewsId) : IRequest<Result<JsonElement>>;

    public class MarkReadHandler(IPeopleDownstreamClient peopleClient)
        : IRequestHandler<MarkReadQuery, Result<JsonElement>>
    {
        public Task<Result<JsonElement>> Handle(MarkReadQuery request, CancellationToken cancellationToken)
            => peopleClient.MarkNewsReadAsync(request.NewsId, new { readFrom = "mobile-ess" }, cancellationToken);
    }

    public record ToggleReactionQuery(Guid NewsId, string Key) : IRequest<Result<JsonElement>>;

    public class ToggleReactionHandler(IPeopleDownstreamClient peopleClient)
        : IRequestHandler<ToggleReactionQuery, Result<JsonElement>>
    {
        public Task<Result<JsonElement>> Handle(ToggleReactionQuery request, CancellationToken cancellationToken)
            => peopleClient.ToggleNewsReactionAsync(
                request.NewsId,
                new { key = request.Key },
                cancellationToken);
    }

    public sealed record MobileComment(Guid Id, string EmployeeName, string Description, string Created);

    public record ListCommentsQuery(Guid NewsId) : IRequest<Result<List<MobileComment>>>;

    public class ListCommentsHandler(IPeopleDownstreamClient peopleClient)
        : IRequestHandler<ListCommentsQuery, Result<List<MobileComment>>>
    {
        public async Task<Result<List<MobileComment>>> Handle(ListCommentsQuery request, CancellationToken cancellationToken)
        {
            var result = await peopleClient.GetNewsCommentsAsync(request.NewsId, cancellationToken);
            if (result.IsFailure)
            {
                return Result.WithFailure<List<MobileComment>>(result.Error ?? new Error("Feed.CommentsUnavailable", "Comments are unavailable."), result.Message, result.Code);
            }

            var items = result.Data.ValueKind == JsonValueKind.Object && result.Data.TryGetProperty("items", out var page)
                ? page
                : result.Data;
            if (items.ValueKind != JsonValueKind.Array)
            {
                return Result.WithSuccess(new List<MobileComment>());
            }

            return Result.WithSuccess(items.EnumerateArray().Select(ToComment).ToList());
        }
    }

    public record CreateCommentQuery(Guid NewsId, string Body) : IRequest<Result<MobileComment>>;

    public class CreateCommentHandler(IPeopleDownstreamClient peopleClient)
        : IRequestHandler<CreateCommentQuery, Result<MobileComment>>
    {
        public async Task<Result<MobileComment>> Handle(CreateCommentQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return Result.WithFailure<MobileComment>(
                    new Error("Feed.CommentEmpty", "Comment cannot be empty."));
            }

            var result = await peopleClient.CreateNewsCommentAsync(
                request.NewsId,
                new { body = request.Body.Trim() },
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.WithFailure<MobileComment>(result.Error ?? new Error("Feed.CommentsUnavailable", "Comments are unavailable."), result.Message, result.Code);
            }

            return Result.WithSuccess(ToComment(result.Data));
        }
    }

    private static MobileComment ToComment(JsonElement element) => new(
        element.TryGetProperty("id", out var id) && id.TryGetGuid(out var guid) ? guid : Guid.Empty,
        ReadString(element, "authorName"),
        ReadString(element, "body"),
        ReadString(element, "created"));

    private static string ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

public class MobileFeedActionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(MobileApiConstants.Url.FeedRead,
                [Authorize] async (Guid newsId, ISender sender) =>
                    (await sender.Send(new MobileFeedActions.MarkReadQuery(newsId))).ToIResult())
            .WithTags(MobileApiConstants.Tags.Me)
            .WithName("MobileFeedMarkRead")
            .WithSummary("Mark a company news item as read for the current employee");

        app.MapPost(MobileApiConstants.Url.FeedReactions,
                [Authorize] async (Guid newsId, ToggleReactionBody body, ISender sender) =>
                    (await sender.Send(new MobileFeedActions.ToggleReactionQuery(newsId, body.Key))).ToIResult())
            .WithTags(MobileApiConstants.Tags.Me)
            .WithName("MobileFeedToggleReaction")
            .WithSummary("Toggle a reaction (e.g. Like) on a company news item");

        app.MapGet(MobileApiConstants.Url.FeedComments,
                [Authorize] async (Guid newsId, ISender sender) =>
                    (await sender.Send(new MobileFeedActions.ListCommentsQuery(newsId))).ToIResult())
            .WithTags(MobileApiConstants.Tags.Me)
            .WithName("MobileFeedListComments")
            .WithSummary("List comments on a company news item");

        app.MapPost(MobileApiConstants.Url.FeedCommentCreate,
                [Authorize] async (Guid newsId, CreateCommentBody body, ISender sender) =>
                    (await sender.Send(new MobileFeedActions.CreateCommentQuery(newsId, body.Body))).ToIResult())
            .WithTags(MobileApiConstants.Tags.Me)
            .WithName("MobileFeedCreateComment")
            .WithSummary("Add a comment to a company news item");
    }

    public sealed record ToggleReactionBody(string Key);

    public sealed record CreateCommentBody(string Body);
}
