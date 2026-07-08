using System;
using System.Security.Cryptography;
using System.Text;

namespace BlockChainP411NEW.Models
{
    public class Transaction
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; } // Комісія мережі
        public DateTime TimeStamp { get; set; } // Час транзакції
        public byte[] SenderPublicKey { get; set; }
        public byte[] Signature { get; set; }

        public Transaction() { }

        public Transaction(string from, string to, decimal amount, byte[] senderPublicKey)
        {
            Id = Guid.NewGuid().ToString();
            From = from;
            To = to;
            Amount = amount;
            TimeStamp = DateTime.UtcNow;
            this.SenderPublicKey = senderPublicKey;
        }

        public string ToRowString()
        {
            if (Signature != null)
            {
                return $"{Id} | {From} -> {To} | Amount: {Amount} | Time: {TimeStamp.ToString("O")} {Convert.ToHexString(Signature)}";
            }
            return $"{Id} | {From} -> {To} | Amount: {Amount} | Time: {TimeStamp.ToString("O")} | Fee: {Fee}";
        }

        public byte[] GetDataToSign()
        {
            string raw = $"{Id}{From}{To}{Amount}{TimeStamp:O}{Fee}"; // Форматування дати у стандартному ISO 8601 форматі для забезпечення консистентності
            return Encoding.UTF8.GetBytes(raw); // Конвертуємо строку в байтовий масив для подальшої підписки
        }

        public string ToRawString() => $"From:{From}|To:{To}|Amount:{Amount}|Fee:{Fee}";

        private string GenerateHashId()
        {
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(ToRawString()));
            return Convert.ToHexString(hashBytes);
        }
    }
}