using BlockChainP411NEW.Models;
using System;
using System.Collections.Generic;

namespace BlockChainP411NEW.Services
{
    public class BlockChainDisplayService
    {
        public void PrintBlockChain(List<Block> chain)
        {
            foreach (var block in chain)
            {
                Console.WriteLine($"Index: {block.Index}");
                Console.WriteLine($"TimeStamp: {block.TimeStamp}");
                Console.WriteLine($"PreviousHash: {block.PreviousHash}");
                Console.WriteLine($"Hash: {block.Hash}");
                Console.WriteLine($"Difficulty: {block.Difficulty}");
                Console.WriteLine($"Mining Duration: {block.MiningDuration:F3} seconds");

                Console.WriteLine("Transactions:");
                if (block.Transactions.Count == 0)
                {
                    Console.WriteLine("  [No Transactions - Genesis Block]");
                }
                else
                {
                    foreach (var tx in block.Transactions)
                    {
                        Console.WriteLine($"  - {tx.ToRawString()}");
                    }
                }

                Console.WriteLine(new string('-', 50));
            }
        }

        public void PrintValidationResult(bool isValid)
        {
            Console.WriteLine(isValid ? "The blockchain is valid." : "The blockchain is invalid.");
        }
    }
}