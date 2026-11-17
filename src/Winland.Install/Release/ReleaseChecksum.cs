using System.Security.Cryptography;

namespace Winland.Install;

public static class ReleaseChecksum
{
    public static bool Matches(ReadOnlySpan<byte> archive, string sums)
    {
        ArgumentNullException.ThrowIfNull(sums);
        var expected = HashFor(sums, ReleaseAssets.ArchiveName);
        if (expected is null)
            return false;

        var actual = Convert.ToHexString(SHA256.HashData(archive));
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string? HashFor(string sums, string name)
    {
        var text = sums.TrimStart('\uFEFF');
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length < 64)
                continue;

            var hash = line[..64];
            if (!IsHex(hash))
                continue;

            var rest = line[64..].TrimStart();
            if (rest.StartsWith('*'))
                rest = rest[1..];

            if (rest.Equals(name, StringComparison.Ordinal))
                return hash;
        }

        return null;
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            var hex = character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!hex)
                return false;
        }

        return true;
    }
}
