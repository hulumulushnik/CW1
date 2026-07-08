using BlockChainP411NEW.Models;
using System;
using System.Security.Cryptography;
using System.Text;

namespace BlockChainP411NEW.Services
{
    public class WalletService
    {
        public Wallet CreateWallet(string name)
        {
            using var ecdsa = ECDsa.Create();
            byte[] privateKey = ecdsa.ExportECPrivateKey();
            byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();

            string address = GenerateAddress(publicKey);
            return new Wallet(name, address, publicKey, privateKey);
        }

        public string GenerateAddress(byte[] publicKey)
        {
            byte[] hashBytes = SHA256.HashData(publicKey);
            string hexKey = Convert.ToHexString(hashBytes).ToLower();
            return "0x" + hexKey.Substring(0, 40);
        }

        public bool VerifySignature(string publicKeyBase64, byte[] data, byte[] signature)
        {
            try
            {
                using var ecdsa = ECDsa.Create();
                byte[] publicKey = Convert.FromBase64String(publicKeyBase64);
                ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
            }
            catch { return false; }
        }

        public byte[] SignMessage(Wallet wallet, string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            return wallet.Sign(messageBytes);
        }

        public bool VerifyMessage(string claimedAddress, byte[] publicKey, string message, byte[] signature)
        {
            string expectedAddress = GenerateAddress(publicKey);

            if (expectedAddress != claimedAddress)
            {
                Console.WriteLine("Авторизація провалена: публічний ключ не відповідає заявленій адресі!");
                return false;
            }

            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);

                bool isValid = ecdsa.VerifyData(messageBytes, signature, HashAlgorithmName.SHA256);
                if (!isValid)
                {
                    Console.WriteLine("Авторизація провалена: невірний криптографічний підпис!");
                }
                return isValid;
            }
            catch
            {
                Console.WriteLine("Авторизація провалена: помилка обробки ключів.");
                return false;
            }
        }
    }
}