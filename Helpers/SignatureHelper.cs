using System.Security.Cryptography;
using System.Text;

namespace PartnerTransactionAPI.Helpers
{
    public static class SignatureHelper
    {
        public static string GenerateSignature(string sigtimestamp, string partnerkey, string partnerrefno, long totalamount, string partnerpasswordBase64)
        {
            string input = $"{sigtimestamp}{partnerkey}{partnerrefno}{totalamount}{partnerpasswordBase64}";

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Convert to hex string
            var sb = new StringBuilder();
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));

            string hexString = sb.ToString();

            // Base64 encode the hex string
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(hexString));
        }
    }
}
