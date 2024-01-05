using ComponentAce.Compression.Libs.zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using System.Buffers;

namespace TestProject
{
    internal class Program
    {
        private static byte[] DecryptBytes(byte[] cipherText, int cipherBytesLength, byte[] key, byte[] iv)
        {
            byte[] decryptedData = null;

            using (var aesAlg = Aes.Create())
            {
                aesAlg.Padding = PaddingMode.Zeros;
                aesAlg.Key = key;
                aesAlg.IV = iv;

                var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (var msDecrypt = new MemoryStream())
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Write))
                    {
                        csDecrypt.Write(cipherText, 0, cipherBytesLength);
                        csDecrypt.Close();
                    }

                    decryptedData = msDecrypt.ToArray();
                }
            }

            return decryptedData;
        }

        private static byte[] ProcessData(byte[] key, byte[] iv, byte[] data)
        {
            var actualDataLength = data.Length - iv.Length;
            var actualData = ArrayPool<byte>.Shared.Rent(actualDataLength);

            Array.Copy(data, iv.Length, actualData, 0, actualDataLength);
            Array.Copy(data, iv, iv.Length);

            Console.WriteLine($"IV: {BitConverter.ToString(iv)}");

            var decryptedData = DecryptBytes(actualData, actualDataLength, key, iv);

            return decryptedData;

        }

        private static byte[] GetKey()
        {
            Console.WriteLine("Extracting key...");
            var assembly = typeof(EFT.Player).Assembly;
            var type = assembly.GetType("\uE2B8", throwOnError: true);
            var field = type.GetField("\uE001", BindingFlags.NonPublic | BindingFlags.Static);
            var date = (byte[])field.GetValue(null);
            Console.WriteLine($"Key: {BitConverter.ToString(date)}");
            return date;
        }

        private static byte[] GetIV()
        {
            Console.WriteLine("Extracting IV...");
            var assembly = typeof(EFT.Player).Assembly;
            var type = assembly.GetType("\uE2B8", throwOnError: true);
            var field = type.GetField("\uE002", BindingFlags.NonPublic | BindingFlags.Static);
            var instance = field.GetValue(null);
            var method = instance.GetType().GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
            var data = (byte[])method.Invoke(instance, null);
            Console.WriteLine($"IV length: {data.Length}");
            return data;
        }

        static void Main(string[] args)
        {
            var key = GetKey();
            var iv = GetIV();

            var client = new RestClient("https://prod.escapefromtarkov.com");

            var request = new RestRequest("/client/profile/view", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var payload = new { accountId = "98374398" };

            request.AddJsonBody(payload);

            var response = client.Execute(request);

            Console.WriteLine($"Status code: {response.StatusCode}");

            var responseBytes = response.RawBytes;
            File.WriteAllBytes("response_raw.bin", responseBytes);

            var decrypted = ProcessData(key, iv, responseBytes);
            File.WriteAllBytes("response_decrypted.bin", decrypted);

            var decompressed = SimpleZlib.Decompress(decrypted, null);

            Console.WriteLine($"Content: {decompressed}");
            Console.ReadLine();
        }
    }
}
