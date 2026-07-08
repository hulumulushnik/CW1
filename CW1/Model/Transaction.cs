using System;
using System.Security.Cryptography;
using System.Text;

namespace BlockChainP411NEW.Models
{
    public enum TransactionType
    {
        Transfer,
        CreateToken
    }

    public class Transaction
    {
        public string Id { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public decimal Amount { get; set; }

        // Комісія ЗАВЖДИ сплачується в BASE
        public decimal Fee { get; set; }

        // Валюта транзакції
        public string Currency { get; set; } = "BASE";

        // Тип транзакції
        public TransactionType Type { get; set; } = TransactionType.Transfer;

        // Використовується лише для ICO
        public decimal TotalSupply { get; set; }

        public DateTime TimeStamp { get; set; }

        public byte[] SenderPublicKey { get; set; }

        public byte[] Signature { get; set; }

        public Transaction()
        {

        }

        public Transaction(
            string from,
            string to,
            decimal amount,
            byte[] senderPublicKey)
        {
            Id = Guid.NewGuid().ToString();

            From = from;
            To = to;
            Amount = amount;

            Fee = 0;

            Currency = "BASE";

            Type = TransactionType.Transfer;

            TotalSupply = 0;

            TimeStamp = DateTime.UtcNow;

            SenderPublicKey = senderPublicKey;
        }

        public string ToRowString()
        {
            if (Signature != null)
            {
                return $"{Id} | {Type} | {Currency} | {From}->{To} | Amount:{Amount} | Supply:{TotalSupply} | Fee:{Fee} | {TimeStamp:O} | {Convert.ToHexString(Signature)}";
            }

            return $"{Id} | {Type} | {Currency} | {From}->{To} | Amount:{Amount} | Supply:{TotalSupply} | Fee:{Fee} | {TimeStamp:O}";
        }

        public byte[] GetDataToSign()
        {
            string raw =
                $"{Id}" +
                $"{From}" +
                $"{To}" +
                $"{Amount}" +
                $"{Fee}" +
                $"{Currency}" +
                $"{Type}" +
                $"{TotalSupply}" +
                $"{TimeStamp:O}";

            return Encoding.UTF8.GetBytes(raw);
        }

        public string ToRawString()
        {
            return $"[{Type}] {Currency} | {From} -> {To} | Amount={Amount} | Fee(BASE)={Fee} | Supply={TotalSupply}";
        }

        private string GenerateHashId()
        {
            byte[] hashBytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(ToRawString()));

            return Convert.ToHexString(hashBytes);
        }
    }
}