using System.Text.Json;
using FluentAssertions;
using Moq;
using QFace.Sdk.Extensions;
using QimErp.Proxy.Mobile.Shared.Clients;
using QimErp.Proxy.Mobile.WebApi.Features.Security;

namespace QimErp.Proxy.Mobile.WebApi.Tests;

public class MobileSecurityPassthroughTests
{
    [Fact]
    public async Task GetSessions_delegates_to_iam()
    {
        var platform = new Mock<IPlatformDownstreamClient>();
        platform.Setup(x => x.GetSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse(
                """[{"sessionId":"s1","deviceType":"Mobile","isCurrent":true}]""").RootElement));

        var handler = new GetMobileSessions.Handler(platform.Object);

        var result = await handler.Handle(new GetMobileSessions.Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.GetArrayLength().Should().Be(1);
        platform.Verify(x => x.GetSessionsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeSession_forwards_session_id()
    {
        var platform = new Mock<IPlatformDownstreamClient>();
        platform.Setup(x => x.RevokeSessionAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse("""{"ok":true}""").RootElement));

        var handler = new RevokeMobileSession.Handler(platform.Object);

        var result = await handler.Handle(
            new RevokeMobileSession.Command { SessionId = "s1" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        platform.Verify(
            x => x.RevokeSessionAsync(
                It.Is<object>(b => b.ToString()!.Contains("s1")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAll_delegates()
    {
        var platform = new Mock<IPlatformDownstreamClient>();
        platform.Setup(x => x.RevokeAllSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse("""{"ok":true}""").RootElement));

        var handler = new RevokeAllMobileSessions.Handler(platform.Object);

        var result = await handler.Handle(new RevokeAllMobileSessions.Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        platform.Verify(x => x.RevokeAllSessionsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_forwards_current_and_new()
    {
        var platform = new Mock<IPlatformDownstreamClient>();
        platform.Setup(x => x.ChangePasswordAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.WithSuccess(JsonDocument.Parse("\"Password changed.\"").RootElement));

        var handler = new ChangeMobilePassword.Handler(platform.Object);

        var result = await handler.Handle(
            new ChangeMobilePassword.Command { CurrentPassword = "old", NewPassword = "newPass123" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        platform.Verify(
            x => x.ChangePasswordAsync(
                It.Is<object>(b =>
                    b.ToString()!.Contains("old") && b.ToString()!.Contains("newPass123")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
