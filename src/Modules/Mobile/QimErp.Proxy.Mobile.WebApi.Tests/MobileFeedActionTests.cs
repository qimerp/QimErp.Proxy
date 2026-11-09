using System.Text.Json;
using FluentAssertions;
using Moq;
using QFace.Sdk.Extensions;
using QimErp.Proxy.Mobile.Shared.Clients;
using QimErp.Proxy.Mobile.WebApi.Features.Feed;

namespace QimErp.Proxy.Mobile.WebApi.Tests;

public class MobileFeedActionTests
{
    private static readonly Guid NewsId = Guid.NewGuid();

    [Fact]
    public async Task MarkRead_forwards_newsId()
    {
        var people = new Mock<IPeopleDownstreamClient>();
        people.Setup(x => x.MarkNewsReadAsync(NewsId, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse("""true""").RootElement));

        var handler = new MobileFeedActions.MarkReadHandler(people.Object);

        var result = await handler.Handle(new MobileFeedActions.MarkReadQuery(NewsId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        people.Verify(
            x => x.MarkNewsReadAsync(NewsId, It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleReaction_forwards_key()
    {
        var people = new Mock<IPeopleDownstreamClient>();
        people.Setup(x => x.ToggleNewsReactionAsync(NewsId, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse("""{"total":1}""").RootElement));

        var handler = new MobileFeedActions.ToggleReactionHandler(people.Object);

        var result = await handler.Handle(
            new MobileFeedActions.ToggleReactionQuery(NewsId, "Like"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        people.Verify(
            x => x.ToggleNewsReactionAsync(
                NewsId,
                It.Is<object>(b => b.ToString()!.Contains("Like")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListComments_maps_paged_comments_to_the_list_the_app_reads()
    {
        var commentId = Guid.NewGuid();
        var people = new Mock<IPeopleDownstreamClient>();
        people.Setup(x => x.GetNewsCommentsAsync(NewsId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse(
                $$"""{"items":[{"id":"{{commentId}}","authorName":"Ama","body":"Nice update!","created":"2026-11-09T01:00:00Z"}],"totalCount":1}""").RootElement));

        var handler = new MobileFeedActions.ListCommentsHandler(people.Object);

        var result = await handler.Handle(new MobileFeedActions.ListCommentsQuery(NewsId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.Should().Be(
            new MobileFeedActions.MobileComment(commentId, "Ama", "Nice update!", "2026-11-09T01:00:00Z"));
        people.Verify(x => x.GetNewsCommentsAsync(NewsId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateComment_rejects_empty(string body)
    {
        var people = new Mock<IPeopleDownstreamClient>();
        var handler = new MobileFeedActions.CreateCommentHandler(people.Object);

        var result = await handler.Handle(
            new MobileFeedActions.CreateCommentQuery(NewsId, body),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        people.Verify(x => x.CreateNewsCommentAsync(It.IsAny<Guid>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateComment_forwards_trimmed_body_to_the_comments_endpoint()
    {
        var people = new Mock<IPeopleDownstreamClient>();
        people.Setup(x => x.CreateNewsCommentAsync(NewsId, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse(
                """{"id":"00000000-0000-0000-0000-000000000001","authorName":"Ama","body":"Nice update!","created":"2026-11-09T01:00:00Z"}""").RootElement));

        var handler = new MobileFeedActions.CreateCommentHandler(people.Object);

        var result = await handler.Handle(
            new MobileFeedActions.CreateCommentQuery(NewsId, "  Nice update!  "),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Description.Should().Be("Nice update!");
        people.Verify(
            x => x.CreateNewsCommentAsync(
                NewsId,
                It.Is<object>(b => b.ToString()!.Contains("Nice update!")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
