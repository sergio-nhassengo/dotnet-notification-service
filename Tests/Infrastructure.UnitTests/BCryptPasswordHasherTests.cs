using Infrastructure.Services.Auth;

namespace Infrastructure.UnitTests;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_Verify_roundtrips_to_true()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_with_the_wrong_password_returns_false()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Hashing_the_same_password_twice_produces_different_hashes()
    {
        var first = _hasher.Hash("correct horse battery staple");
        var second = _hasher.Hash("correct horse battery staple");

        Assert.NotEqual(first, second);
    }
}
