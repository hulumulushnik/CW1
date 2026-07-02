using System;
using System.Collections.Generic;

namespace BlockChainP411NEW.Models
{
    public class Block
    {
        public int Index { get; set; }
        public DateTime TimeStamp { get; set; }

        // Тепер блок містить список транзакцій замість одного рядка
        public List<Transaction> Transactions { get; set; }

        public string PreviousHash { get; set; }
        public long Nonce { get; set; }
        public string Hash { get; set; }
        public double MiningDuration { get; set; }
        public int Difficulty { get; set; }

        public Block(int index, DateTime timeStamp, List<Transaction> transactions, string previousHash, int difficulty)
        {
            Index = index;
            TimeStamp = timeStamp;
            Transactions = transactions; // Зберігаємо список
            PreviousHash = previousHash;
            Difficulty = difficulty;
            Hash = string.Empty;
            Nonce = 0;
            MiningDuration = 0;
        }
    }
}