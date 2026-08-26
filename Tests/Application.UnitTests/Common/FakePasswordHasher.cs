using Application.Common.Security;

namespace Application.UnitTests.Common;

// A trivial fake so Application.UnitTests doesn't need a reference to Infrastructure/BCrypt
// just to exercise handlers that depend on IPasswordHasher.
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
}
