using System.Security.Claims;
using Infrastructure.Adapters.Auth;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Tests.Infrastructure.Adapters.Auth;

public class HttpContextCurrentUserTests
{
    private static HttpContextCurrentUser BuildWithContext(HttpContext? context)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(context);
        return new HttpContextCurrentUser(accessor.Object);
    }

    private static HttpContext BuildAuthenticatedContext(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var context = new DefaultHttpContext { User = principal };
        return context;
    }

    [Fact]
    public void IsAuthenticated_NoHttpContext_ReturnsFalse()
    {
        var user = BuildWithContext(null);
        Assert.False(user.IsAuthenticated);
    }

    [Fact]
    public void UserId_NoHttpContext_ReturnsNull()
    {
        var user = BuildWithContext(null);
        Assert.Null(user.UserId);
    }

    [Fact]
    public void OpenProjectInstanceUrl_NoHttpContext_ReturnsNull()
    {
        var user = BuildWithContext(null);
        Assert.Null(user.OpenProjectInstanceUrl);
    }

    [Fact]
    public void IsAuthenticated_AnonymousUser_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        var user = BuildWithContext(context);
        Assert.False(user.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_AuthenticatedUser_ReturnsTrue()
    {
        var context = BuildAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "user-123"));
        var user = BuildWithContext(context);
        Assert.True(user.IsAuthenticated);
    }

    [Fact]
    public void UserId_AuthenticatedUser_ReturnsNameIdentifierClaim()
    {
        const string expectedId = "user-abc-123";
        var context = BuildAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, expectedId));
        var user = BuildWithContext(context);
        Assert.Equal(expectedId, user.UserId);
    }

    [Fact]
    public void OpenProjectInstanceUrl_ClaimPresent_ReturnsUrl()
    {
        const string expectedUrl = "http://openproject.example.com";
        var context = BuildAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim("OpenProjectInstanceBaseUrl", expectedUrl));
        var user = BuildWithContext(context);
        Assert.Equal(expectedUrl, user.OpenProjectInstanceUrl);
    }

    [Fact]
    public void OpenProjectInstanceUrl_ClaimAbsent_ReturnsNull()
    {
        var context = BuildAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "user-123"));
        var user = BuildWithContext(context);
        Assert.Null(user.OpenProjectInstanceUrl);
    }

    [Fact]
    public void OpenProjectUserId_ClaimPresent_ReturnsId()
    {
        var context = BuildAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim("OpenProjectUserId", "42"));
        var user = BuildWithContext(context);
        Assert.Equal(42, user.OpenProjectUserId);
    }

    [Fact]
    public void OpenProjectUserId_ClaimAbsent_ReturnsNull()
    {
        var context = BuildAuthenticatedContext(
            new Claim(ClaimTypes.NameIdentifier, "user-123"));
        var user = BuildWithContext(context);
        Assert.Null(user.OpenProjectUserId);
    }

    [Fact]
    public void OpenProjectUserId_NoHttpContext_ReturnsNull()
    {
        var user = BuildWithContext(null);
        Assert.Null(user.OpenProjectUserId);
    }
}
