using System.Security.Cryptography;

namespace FamilyHub.Api.Households;

/// <summary>Generates short, human-typeable Join Codes for a Household.</summary>
public static class JoinCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // excludes ambiguous chars (I, O, 0, 1)
    private const int Length = 6;

    public static string Generate()
    {
        Span<char> code = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
