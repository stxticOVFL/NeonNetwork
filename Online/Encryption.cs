using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections.LowLevel.Unsafe;

namespace NeonNetwork.Online
{
    internal static class Encryption
    {
        static bool setup = false;
        static readonly RSACryptoServiceProvider rsa = new();

        public static void Setup()
        {
            try
            {
                static byte[] db64(string str) => Convert.FromBase64String(str);
                var key = Encoding.UTF8.GetString(Resources.r.pubkey).Split().Last().Split('|');
                var param = new RSAParameters
                {
                    Modulus = db64(key[0]),
                    Exponent = db64(key[1]),
                };

                rsa.ImportParameters(param);
            }
            catch (Exception ex)
            {
                NeonNetwork.Logger.Error(ex);
            }
        }

        public static byte[] Encrypt(string pass)
        {
            try
            {
                var data = new byte[0x100 + 1];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(data, 1, 0x100);
                BinaryWriter writer = new(new MemoryStream(data));
                var intbuf = new byte[sizeof(int)];
                rng.GetBytes(intbuf);
                var rand = BitConverter.ToUInt32(intbuf, 0) % (0x100 - (pass.Length + 1));
                writer.Seek((int)(rand + 1), SeekOrigin.Begin);
                writer.Write((byte)pass.Length);
                writer.Write(pass);
                data[0] = (byte)rand;

                data = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA1);
                var sesBytes = BitConverter.GetBytes(Online.sessionID);
                for (int i = 0; i < data.Length; ++i)
                    data[i] ^= sesBytes[i % sesBytes.Length];

                return data;
            }
            catch (Exception e)
            {
                NeonNetwork.Logger.Error($"Failed to encrypt: {e}");
                return [];
            }
        }
    }
}
