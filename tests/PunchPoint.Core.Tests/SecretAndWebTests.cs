using PunchPoint.Core.Models;
using PunchPoint.Core.Security;
using PunchPoint.Core.Storage;

namespace PunchPoint.Core.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void Plain_text_is_left_alone_when_encryption_is_off()
    {
        Assert.Equal("hunter2", SecretProtector.Protect("hunter2", encrypt: false));
        Assert.Equal("hunter2", SecretProtector.Unprotect("hunter2"));
    }

    [Fact]
    public void Protected_values_round_trip_and_are_not_readable()
    {
        string stored = SecretProtector.Protect("app-password-123", encrypt: true);

        Assert.True(SecretProtector.IsProtected(stored));
        Assert.DoesNotContain("app-password-123", stored);
        Assert.Equal("app-password-123", SecretProtector.Unprotect(stored));
    }

    [Fact]
    public void Empty_values_stay_empty()
    {
        Assert.Equal("", SecretProtector.Protect("", encrypt: true));
        Assert.Equal("", SecretProtector.Unprotect(""));
        Assert.False(SecretProtector.IsProtected(""));
    }
}
