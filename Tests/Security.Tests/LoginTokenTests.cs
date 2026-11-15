using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace Security.Tests;

/// <summary>
/// S3/S6: the one-time login token (`!requesttoken`, "generate login token") that either login path accepts
/// in place of a password. Exercises Application/Misc/TokenAuthentication.cs in-process only.
/// </summary>
public class LoginTokenTests
{
    private static ITokenAuthentication NewAuthenticator() =>
        (ITokenAuthentication)Activator.CreateInstance(
            typeof(IW4MAdmin.Application.Program).Assembly.GetType("IW4MAdmin.Application.Misc.TokenAuthentication", true)!,
            nonPublic: true)!;

    [Fact]
    public void Token_is_single_use()
    {
        var authenticator = NewAuthenticator();
        var state = authenticator.GenerateNextToken(new TokenIdentifier { ClientId = 7 });

        Assert.True(authenticator.AuthorizeToken(new TokenIdentifier { ClientId = 7, Token = state.Token }));
        Assert.False(authenticator.AuthorizeToken(new TokenIdentifier { ClientId = 7, Token = state.Token }));
    }

    [Fact]
    public void Token_is_bound_to_the_client_it_was_issued_for()
    {
        var authenticator = NewAuthenticator();
        var state = authenticator.GenerateNextToken(new TokenIdentifier { ClientId = 7 });

        Assert.False(authenticator.AuthorizeToken(new TokenIdentifier { ClientId = 8, Token = state.Token }));
    }

    [Fact]
    public void Token_is_rejected_after_its_advertised_lifetime()
    {
        var authenticator = NewAuthenticator();
        var state = authenticator.GenerateNextToken(new TokenIdentifier { ClientId = 7 });

        // The token is advertised as valid for TokenDuration (two minutes). Age it well past that.
        state.RequestTime = DateTime.Now - state.TokenDuration - TimeSpan.FromHours(6);

        Assert.False(authenticator.AuthorizeToken(new TokenIdentifier { ClientId = 7, Token = state.Token }),
            $"a login token issued {DateTime.Now - state.RequestTime:g} ago (lifetime {state.TokenDuration:g}) was still accepted");
    }

    [Fact]
    public void Token_does_not_survive_unlimited_wrong_guesses()
    {
        var authenticator = NewAuthenticator();
        var state = authenticator.GenerateNextToken(new TokenIdentifier { ClientId = 7 });

        const int wrongGuesses = 10_000;
        for (var i = 0; i < wrongGuesses; i++)
        {
            authenticator.AuthorizeToken(new TokenIdentifier { ClientId = 7, Token = "!" + i });
        }

        Assert.False(authenticator.AuthorizeToken(new TokenIdentifier { ClientId = 7, Token = state.Token }),
            $"a {state.Token.Length}-character login token was still accepted after {wrongGuesses} wrong guesses for the same client");
    }
}
