using BlockChainP411NEW.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BlockChainP411NEW.Services
{
    public class MiningService
    {
        private readonly HashingService _hashingService;

        public MiningService(HashingService hashingService)
        {
            _hashingService = hashingService;
        }

        // Синхронний метод для MineBlock у BlockChainService
        public void MineBlock(Block block, int difficulty)
        {
            string target = new string('0', difficulty);
            block.Nonce = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            do
            {
                block.Nonce++;
                block.Hash = _hashingService.ComputeHash(block);
            }
            while (!block.Hash.StartsWith(target));

            stopwatch.Stop();
            block.MiningDuration = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"[Успіх] Блок знайдено за {block.MiningDuration:F3} сек. Nonce: {block.Nonce}");
        }

        public async Task<bool> MineBlockAsync(Block block, int difficulty, CancellationToken cancellationToken)
        {
            int coreCount = Environment.ProcessorCount;
            Console.WriteLine($"\n[Система] Запуск майнінгу. Ядер: {coreCount} | Складність: {difficulty} | Транзакцій: {block.Transactions.Count}");

            using var internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int? foundNonce = null;
            string foundHash = string.Empty;
            object lockObj = new object();
            var tasks = new List<Task>();

            string baseData = _hashingService.GetBaseBlockData(block);
            byte[] baseBytes = System.Text.Encoding.UTF8.GetBytes(baseData);
            byte[] difficultyBytes = System.Text.Encoding.UTF8.GetBytes(block.Difficulty.ToString());

            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < coreCount; i++)
            {
                int threadOffset = i;
                tasks.Add(Task.Run(() =>
                {
                    int localNonce = threadOffset;
                    Span<byte> buffer = stackalloc byte[512];
                    Span<byte> hashBuffer = stackalloc byte[32];

                    baseBytes.CopyTo(buffer);

                    while (!internalCts.Token.IsCancellationRequested)
                    {
                        int offset = baseBytes.Length;
                        System.Buffers.Text.Utf8Formatter.TryFormat(localNonce, buffer.Slice(offset), out int nonceBytesWritten);
                        offset += nonceBytesWritten;
                        difficultyBytes.CopyTo(buffer.Slice(offset));
                        offset += difficultyBytes.Length;

                        var finalData = buffer.Slice(0, offset);
                        System.Security.Cryptography.SHA256.HashData(finalData, hashBuffer);

                        if (IsValidHash(hashBuffer, difficulty))
                        {
                            lock (lockObj)
                            {
                                if (!internalCts.Token.IsCancellationRequested)
                                {
                                    foundNonce = localNonce;
                                    foundHash = Convert.ToHexString(hashBuffer);
                                    internalCts.Cancel();
                                }
                            }
                            break;
                        }
                        localNonce += coreCount;
                    }
                }, internalCts.Token));
            }

            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) { }

            stopwatch.Stop();

            if (foundNonce.HasValue && !cancellationToken.IsCancellationRequested)
            {
                block.Nonce = foundNonce.Value;
                block.Hash = foundHash;
                block.MiningDuration = stopwatch.Elapsed.TotalSeconds;
                Console.WriteLine($"[Успіх] Блок знайдено за {block.MiningDuration:F3} сек.");
                return true;
            }

            return false;
        }

        private static bool IsValidHash(ReadOnlySpan<byte> hash, int difficulty)
        {
            int fullZeroBytes = difficulty / 2;
            for (int i = 0; i < fullZeroBytes; i++)
                if (hash[i] != 0) return false;

            if (difficulty % 2 != 0)
                if ((hash[fullZeroBytes] & 0xF0) != 0) return false;

            return true;
        }
    }
}