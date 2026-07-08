using System;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace BlockChainP411NEW.Models
{
    public class Wallet
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public byte[] PublicKey { get; set; }

        [JsonInclude]
        public byte[] PrivateKey { get; private set; }

        public Wallet(string name, string address, byte[] publicKey, byte[] privateKey)
        {
            Name = name;
            Address = address;
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        public byte[] Sign(byte[] data)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(PrivateKey, out _);
            return ecdsa.SignData(data, HashAlgorithmName.SHA256);
        }
    }
}