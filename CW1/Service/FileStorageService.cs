using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using BlockChainP411NEW.Models;

namespace BlockChainP411NEW.Services
{
    public class FileStorageService
    {
        private readonly string _blockchainFilePath = "blockchain.json";
        private readonly string _blockchainBackupPath = "blockchain_backup.json";
        private readonly string _walletsFilePath = "wallets.json";

        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public void SaveBlockchain(List<Block> blockchain)
        {
            if (File.Exists(_blockchainFilePath))
            {
                File.Copy(_blockchainFilePath, _blockchainBackupPath, true);
            }

            string json = JsonSerializer.Serialize(blockchain, _jsonSerializerOptions);
            File.WriteAllText(_blockchainFilePath, json);
        }

        public List<Block> LoadBlockchain()
        {
            if (!File.Exists(_blockchainFilePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(_blockchainFilePath);
                return JsonSerializer.Deserialize<List<Block>>(json);
            }
            catch (JsonException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("КРИТИЧНА ПОМИЛКА: Файл блокчейну пошкоджено!");
                Console.ResetColor();

                try
                {
                    File.Move(_blockchainFilePath, "blockchain_corrupted.json", true);
                }
                catch { }

                Console.WriteLine("Спроба відновлення бази даних з резервної копії...");

                if (File.Exists(_blockchainBackupPath))
                {
                    try
                    {
                        string backupJson = File.ReadAllText(_blockchainBackupPath);
                        return JsonSerializer.Deserialize<List<Block>>(backupJson);
                    }
                    catch (JsonException)
                    {
                        return null;
                    }
                }

                return null;
            }
        }

        public void SaveWallets(List<Wallet> wallets)
        {
            string json = JsonSerializer.Serialize(wallets, _jsonSerializerOptions);
            File.WriteAllText(_walletsFilePath, json);
        }

        public List<Wallet> LoadWallets()
        {
            if (!File.Exists(_walletsFilePath))
                return new List<Wallet>();

            string json = File.ReadAllText(_walletsFilePath);
            return JsonSerializer.Deserialize<List<Wallet>>(json) ?? new List<Wallet>();
        }
    }
}