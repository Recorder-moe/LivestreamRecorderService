using LivestreamRecorder.DB.Models;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace LivestreamRecorderBackend.Helper;

internal static class AESHelper
{
    private static readonly byte[] _key = Convert.FromBase64String(Environment.GetEnvironmentVariable("AES_Key")!);

    /// <summary>
    /// 產生被推薦人(B)的推薦碼
    /// </summary>
    /// <param name="referrerId">推薦人id(A)</param>
    /// <param name="user">被推薦人(B)</param>
    /// <returns>被推薦人(B)的推薦碼</returns>
    internal static string GenerateReferralCode(string referrerId, User user)
    {
        string A_id = referrerId;
        byte[] IV = GetIVFromGuidString(user.id);
        string B_code = EncryptString_Aes(A_id, _key, IV);
        user.Referral ??= new Referral();
        user.Referral.Code = B_code;
        return B_code;
    }

    /// <summary>
    /// 從被推薦人(B)取得推薦人(A)id
    /// </summary>
    /// <param name="user">被推薦人(B)</param>
    /// <returns>推薦人(A)id</returns>
    internal static string? GetReferrerIdFromReferee(User user)
    {
        var B_code = user.Referral?.Code;
        if (B_code == null) return null;

        byte[] IV = GetIVFromGuidString(user.id);

        string A_id = DecryptString_Aes(B_code, _key, IV);
        return A_id == Guid.Empty.ToString() ? null : A_id;
    }

    /// <summary>
    /// 將Guid string轉換為IV的byte[]
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    private static byte[] GetIVFromGuidString(string guid)
    {
        var guidCleaned = guid.ToString().Replace("-", "");
        byte[] IV = new byte[guidCleaned.Length / 2];
        for (int i = 0; i < guidCleaned.Length; i += 2)
        {
            IV[i / 2] = Convert.ToByte(guidCleaned.Substring(i, 2), 16);
        }

        return IV;
    }

    /// <summary>
    /// 加密字串
    /// </summary>
    /// <param name="plainText"></param>
    /// <param name="Key"></param>
    /// <param name="IV"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static string EncryptString_Aes(string plainText, byte[] Key, byte[] IV)
    {
        // Check arguments.
        if (plainText == null || plainText.Length <= 0)
            throw new ArgumentNullException(nameof(plainText));
        if (Key == null || Key.Length <= 0)
            throw new ArgumentNullException(nameof(Key));
        if (IV == null || IV.Length <= 0)
            throw new ArgumentNullException(nameof(IV));
        string encrypted;

        // Create an Aes object
        // with the specified key and IV.
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Mode = CipherMode.CFB;
            aesAlg.Padding = PaddingMode.Zeros;
            aesAlg.Key = Key;
            aesAlg.IV = IV;

            // Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            // Create the streams used for encryption.
            using MemoryStream msEncrypt = new();
            using CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (StreamWriter swEncrypt = new(csEncrypt))
            {
                //Write all data to the stream.
                swEncrypt.Write(plainText);
            }
            encrypted = WebEncoders.Base64UrlEncode(msEncrypt.ToArray());
        }

        // Return the encrypted bytes from the memory stream.
        return encrypted;
    }

    /// <summary>
    /// 解密字串
    /// </summary>
    /// <param name="cipherText"></param>
    /// <param name="Key"></param>
    /// <param name="IV"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    internal static string DecryptString_Aes(string cipherText, byte[] Key, byte[] IV)
    {
        // Check arguments.
        if (cipherText == null || cipherText.Length <= 0)
            throw new ArgumentNullException(nameof(cipherText));
        if (Key == null || Key.Length <= 0)
            throw new ArgumentNullException(nameof(Key));
        if (IV == null || IV.Length <= 0)
            throw new ArgumentNullException(nameof(IV));

        // Declare the string used to hold
        // the decrypted text.
        string plaintext;

        // Create an Aes object
        // with the specified key and IV.
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Mode = CipherMode.CFB;
            aesAlg.Padding = PaddingMode.Zeros;
            aesAlg.Key = Key;
            aesAlg.IV = IV;

            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            // Create the streams used for decryption.
            using MemoryStream msDecrypt = new(WebEncoders.Base64UrlDecode(cipherText));
            using CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new(csDecrypt);

            // Read the decrypted bytes from the decrypting stream
            // and place them in a string.
            plaintext = srDecrypt.ReadToEnd();
        }

        return plaintext;
    }

}
