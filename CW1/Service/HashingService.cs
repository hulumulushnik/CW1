using BlockChainP411NEW.Models;
using System;
using System.Collections.Generic;
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

        public string GetBaseBlockData(Block block)
        {

            string merkleRoot = GetMerkleRoot(block.Transactions);
            return $"{block.Index}{block.TimeStamp.ToString("O")}{merkleRoot}{block.PreviousHash}";
        }

        public string ComputeHash(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes);
        }

        public string GetMerkleRoot(List<Transaction> transactions, bool verbose = false)
        {
            if (transactions == null || transactions.Count == 0)
                return ComputeHash("EMPTY");

            var currentLevel = new List<string>();
            foreach (var tx in transactions)
                currentLevel.Add(ComputeHash(tx.ToRowString()));


            var leafSet = new HashSet<string>();
            foreach (var h in currentLevel)
            {
                if (!leafSet.Add(h))
                    throw new Exception(
                        "Виявлено спробу атаки CVE-2012-2459: дублювання транзакцій у дереві!");
            }

            if (verbose)
                Console.WriteLine($"  Level 0 (Листя): {currentLevel.Count} хешів");

            int level = 1;
            while (currentLevel.Count > 1)
            {
                var nextLevel = new List<string>();

                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    string left = currentLevel[i];
                    string right = (i + 1 < currentLevel.Count)
                        ? currentLevel[i + 1]
                        : currentLevel[i];

                    nextLevel.Add(ComputeHash(left + right));
                }

                if (verbose)
                {
                    string levelName = nextLevel.Count == 1 ? "Корінь" : "Гілки";
                    Console.WriteLine($"  Level {level} ({levelName}): {nextLevel.Count} хешів");
                }

                currentLevel = nextLevel;
                level++;
            }

            return currentLevel[0];
        }

        public List<(string Hash, bool IsLeft)> GetMerkleProof(
            List<Transaction> transactions, string targetTransactionId)
        {
            if (transactions == null || transactions.Count == 0)
                throw new ArgumentException("Список транзакцій порожній.");

            var levels = new List<List<string>>();

            var leaves = new List<string>();
            foreach (var tx in transactions)
                leaves.Add(ComputeHash(tx.ToRowString()));
            levels.Add(leaves);

            var current = new List<string>(leaves);
            while (current.Count > 1)
            {
                var next = new List<string>();
                for (int i = 0; i < current.Count; i += 2)
                {
                    string left = current[i];
                    string right = (i + 1 < current.Count) ? current[i + 1] : current[i];
                    next.Add(ComputeHash(left + right));
                }
                levels.Add(next);
                current = next;
            }

            string targetHash = null;
            int targetIndex = -1;
            for (int i = 0; i < transactions.Count; i++)
            {
                if (transactions[i].Id == targetTransactionId)
                {
                    targetHash = leaves[i];
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex == -1)
                throw new ArgumentException($"Транзакцію з ID '{targetTransactionId}' не знайдено.");

            var proof = new List<(string Hash, bool IsLeft)>();
            int index = targetIndex;

            for (int lvl = 0; lvl < levels.Count - 1; lvl++)
            {
                var levelHashes = levels[lvl];
                bool isRightChild = (index % 2 == 1);

                if (isRightChild)
                {
                    proof.Add((levelHashes[index - 1], IsLeft: true));
                }
                else
                {
                    int siblingIndex = (index + 1 < levelHashes.Count) ? index + 1 : index;
                    proof.Add((levelHashes[siblingIndex], IsLeft: false));
                }

                index /= 2;
            }

            return proof;
        }


        public bool VerifyMerkleProof(
            string targetTxHash,
            string expectedRoot,
            List<(string Hash, bool IsLeft)> proof)
        {
            string computedHash = targetTxHash;

            foreach (var (siblingHash, isLeft) in proof)
            {
                computedHash = isLeft
                    ? ComputeHash(siblingHash + computedHash)
                    : ComputeHash(computedHash + siblingHash);
            }

            return computedHash == expectedRoot;
        }
    }
}