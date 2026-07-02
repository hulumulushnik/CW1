using BlockChainP411NEW.Models;
using System;
using System.Security.Cryptography;
using System.Text;

namespace BlockChainP411NEW.Services
{
    public class HashingService
    {
        public string ComputeHash(Block block)
        {
            return ComputeHash(GetBaseBlockData(block) + $"{block.Nonce}{block.Difficulty}");
        }

        // Виділили підготовку базових даних в окремий метод, щоб наш надшвидкий майнер 
        // міг обчислити їх ОДИН раз і не гальмувати
        public string GetBaseBlockData(Block block)
        {
            var totalTxHash = "";
            foreach (var item in block.Transactions)
            {
                totalTxHash += ComputeHash(item.ToRawString());
            }

            // Використовуємо форматування "O" (Round-trip) для часу, як у вашому скріншоті
            return $"{block.Index}{block.TimeStamp.ToString("O")}{totalTxHash}{block.PreviousHash}";
        }

        public string ComputeHash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}