using System.Security.Cryptography;
using System.Text;

namespace Shared;

public static class OpcodeVerifier {
    /// <summary>
    /// Calculates a hash of all the <c>Opcode</c>s for warning on incompatibilities.
    /// </summary>
    /// <returns>A string representation of the hash</returns>
    public static string GetOpcodeHash() {
        Array allOpcodes = Enum.GetValues(typeof(Opcode));
        StringBuilder inputBuilder = new StringBuilder();

        foreach (var opcode in allOpcodes) {
            inputBuilder.Append(opcode.ToString());
            inputBuilder.Append((byte)opcode);
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(inputBuilder.ToString());
        byte[] hashBytes = SHA256.HashData(inputBytes);

        return Convert.ToHexString(hashBytes);
    }
}
