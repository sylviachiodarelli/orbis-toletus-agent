using Orbis.ToletusAgent.Setup;

namespace Orbis.ToletusAgent.Tests.Setup;

public class SetupPasswordHasherTests
{
    [Fact]
    public void HashPassword_and_verify_roundtrip()
    {
        var hash = SetupPasswordHasher.HashPassword("strong-password-123");

        Assert.True(SetupPasswordHasher.VerifyPassword("strong-password-123", hash));
        Assert.False(SetupPasswordHasher.VerifyPassword("wrong-password", hash));
    }
}
