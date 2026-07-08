using BlockChainP411NEW.Models;
using CW1.Models;
using CW1.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlockChainP411NEW.Services
{
    public class BlockChainService
    {
        public List<Block> Chain { get; set; } = new List<Block>();
        public List<Transaction> PendingTransactions { get; } = new List<Transaction>();
        public const string BaseCurrency = "BASE";

        public const decimal ICOFee = 100m;
        public HashSet<string> Nodes { get; } = new HashSet<string>();

        private readonly HashingService _hashingService = new();
        private readonly MiningService _miningService;
        public readonly TransactionService _transactionService;
        public readonly WalletService _walletService = new();
        private readonly FileStorageService _fileStorageService = new();
        private TcpP2pService _tcpP2pService;

        private readonly int maxTransactionAmount = 10;
        private readonly decimal _rewardAmount = 50;
        private readonly int _adjustmentInterval = 10;
        private readonly double _targetBlockTime = 10;

        public int Difficulty { get; private set; } = 4;
        public decimal MaxSupply { get; } = 1000;
        public decimal TotalMinted { get; private set; } = 0;
        public int MaxMempoolSize { get; } = 5;

        public BlockChainService()
        {
            _miningService = new MiningService(_hashingService);

            var loadedChain = _fileStorageService.LoadBlockchain();

            if (loadedChain != null && loadedChain.Count > 0)
            {
                Chain = loadedChain;
                _transactionService = new TransactionService(_walletService) { BlockChain = this };

                TotalMinted = Chain
                    .SelectMany(b => b.Transactions)
                    .Where(t => t.From == "COINBASE")
                    .Sum(t => t.Amount);
                Difficulty = Chain[^1].Difficulty;

                Console.WriteLine($"[Сховище] Завантажено ланцюг із {Chain.Count} блоків. TotalMinted: {TotalMinted}, Difficulty: {Difficulty}.");
            }
            else
            {
                _transactionService = new TransactionService(_walletService) { BlockChain = this };
                CreateGenesisBlock();
                _fileStorageService.SaveBlockchain(Chain);
                Console.WriteLine("[Сховище] Новий ланцюг створено та збережено.");
            }
        }

        public void SetP2pService(TcpP2pService p2pService)
        {
            _tcpP2pService = p2pService;
        }

        public void RegisterNode(string address) => Nodes.Add(address);

        public void ClearBlockchain()
        {
            PendingTransactions.Clear();
            TotalMinted = 0;
            Chain = new List<Block>();
            CreateGenesisBlock();
            _fileStorageService.SaveBlockchain(Chain);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Очищення] Блокчейн скинуто до нового генезис-блока. Мемпул очищено.");
            Console.ResetColor();
        }

        public async Task<bool> ResolveConflicts()
        {
            bool replaced = false;
            foreach (var node in Nodes)
            {
                try
                {
                    using var client = new HttpClient();
                    var response = await client.GetAsync($"{node}/blocks");
                    if (response.IsSuccessStatusCode)
                    {
                        var newChain = await response.Content.ReadFromJsonAsync<List<Block>>();
                        if (newChain != null && newChain.Count > Chain.Count)
                        {
                            Chain = newChain;
                            replaced = true;
                        }
                    }
                }
                catch { }
            }
            return replaced;
        }

        public void AddTransactionToMempool(Transaction transaction)
        {
            var validationResult = _transactionService.ValidateTransaction(transaction);
            if (!validationResult.IsValid)
                throw new InvalidOperationException($"Invalid transaction: {validationResult.ErrorMessage}");

            if (transaction.From != "COINBASE")
            {
                decimal tokenBalance =
     GetPendingBalance(transaction.From,
                       transaction.Currency);

                decimal baseBalance =
                    GetPendingBalance(transaction.From,
                                      BaseCurrency);

                if (transaction.Type == TransactionType.CreateToken)
                {
                    if (TokenExists(transaction.Currency))
                        throw new InvalidOperationException(
                            "Токен з такою назвою вже існує.");

                    if (baseBalance < ICOFee)
                        throw new InvalidOperationException(
                            "Недостатньо BASE.");
                }
                else
                {
                    if (tokenBalance < transaction.Amount)
                        throw new InvalidOperationException(
                            "Недостатньо токенів.");

                    if (baseBalance < transaction.Fee)
                        throw new InvalidOperationException(
                            "Недостатньо BASE для комісії.");
                }
            }

            var duplicate = PendingTransactions.FirstOrDefault(t =>
                t.From == transaction.From &&
                t.To == transaction.To &&
                t.Amount == transaction.Amount);

            if (duplicate != null)
            {
                if (transaction.Fee > duplicate.Fee)
                {
                    PendingTransactions.Remove(duplicate);
                    PendingTransactions.Add(transaction);
                    Console.WriteLine("Транзакцію успішно оновлено з вищою комісією!");
                    return;
                }
                else
                {
                    throw new InvalidOperationException("A similar transaction already exists. Increase fee to replace.");
                }
            }

            if (PendingTransactions.Count < MaxMempoolSize)
            {
                PendingTransactions.Add(transaction);
            }
            else
            {
                var cheapest = PendingTransactions.OrderBy(t => t.Fee).First();
                if (transaction.Fee > cheapest.Fee)
                {
                    PendingTransactions.Remove(cheapest);
                    PendingTransactions.Add(transaction);
                }
                else
                {
                    throw new InvalidOperationException("Mempool is full. Fee is too low.");
                }
            }
        }

        public decimal GetCurrentReward()
        {
            decimal baseReward = 50;
            int halvings = Chain.Count / 3;

            decimal reward = baseReward;
            for (int i = 0; i < halvings; i++)
            {
                reward /= 2;
                if (reward < 1)
                    return 0;
            }

            return reward;
        }

        public void MineBlock(string minerAddress)
        {

            decimal currentReward = GetCurrentReward();
            decimal pendingFees = PendingTransactions.Sum(t => t.Fee);

            if (currentReward == 0 && pendingFees == 0)
            {
                throw new InvalidOperationException("Майнінг скасовано: блок нерентабельний (немає нагороди та комісій).");
            }

            var sortedTransactions = PendingTransactions
                .OrderByDescending(t => t.Fee)
                .Take(maxTransactionAmount)
                .ToList();
            List<Transaction> issuedTokens = new();

            foreach (var tx in sortedTransactions)
            {
                if (tx.Type == TransactionType.CreateToken)
                {
                    issuedTokens.Add(

                        new Transaction(
                            "COINBASE",
                            tx.From,
                            tx.TotalSupply,
                            Array.Empty<byte>())
                        {
                            Currency = tx.Currency,
                            Fee = 0,
                            Type = TransactionType.Transfer
                        }

                    );
                }
            }

            decimal totalFeesInBlock = sortedTransactions.Sum(t => t.Fee);
            decimal burnedFees = totalFeesInBlock * 0.5m;
            decimal minerFeeShare = totalFeesInBlock - burnedFees;

            if (TotalMinted + currentReward > MaxSupply)
                currentReward = Math.Max(0, MaxSupply - TotalMinted);

            var totalReward = currentReward + minerFeeShare;

            var rewardTransaction = new Transaction("COINBASE", minerAddress, totalReward, Array.Empty<byte>());
            rewardTransaction.Currency = BaseCurrency;
            rewardTransaction.Type = TransactionType.Transfer;
            sortedTransactions.Insert(0, rewardTransaction);

            foreach (var token in issuedTokens)
            {
                sortedTransactions.Add(token);
            }

            TotalMinted += currentReward;

            Block previousBlock = Chain.Last();
            Block newBlock = new Block(
                previousBlock.Index + 1,
                DateTime.UtcNow,
                sortedTransactions,
                previousBlock.Hash,
                Difficulty);

            _miningService.MineBlock(newBlock, Difficulty);

            Chain.Add(newBlock);

            _fileStorageService.SaveBlockchain(Chain);
            Console.WriteLine($"[Сховище] Ланцюг успішно збережено у файл після майнінгу блоку №{newBlock.Index}.");

            _tcpP2pService?.BroadcastNewBlock(newBlock);

            PendingTransactions.RemoveAll(t => sortedTransactions.Contains(t));

            if (newBlock.Index % _adjustmentInterval == 0 && newBlock.Index > 0)
            {
                AdjustDifficulty(newBlock);
            }
        }

        public async Task<bool> AddBlockAsync(List<Transaction> txs, string miner, CancellationToken ct)
        {
            decimal totalFees = txs.Sum(t => t.Fee);
            decimal reward = Math.Min(_rewardAmount + totalFees, MaxSupply - TotalMinted);
            TotalMinted += reward;

            var rewardTx = new Transaction("COINBASE", miner, reward, Array.Empty<byte>());
            rewardTx.Currency = BaseCurrency;
            rewardTx.Type = TransactionType.Transfer;
            var allTxs = new List<Transaction> { rewardTx };
            allTxs.AddRange(txs);

            var newBlock = new Block(Chain.Count, DateTime.UtcNow, allTxs, Chain.Last().Hash, Difficulty);

            if (await _miningService.MineBlockAsync(newBlock, Difficulty, ct))
            {
                Chain.Add(newBlock);
                _fileStorageService.SaveBlockchain(Chain);
                return true;
            }

            TotalMinted -= reward;
            return false;
        }

        private void CreateGenesisBlock()
        {
            var genesisBlock = new Block(0, DateTime.UtcNow, new List<Transaction>(), "0", Difficulty);
            genesisBlock.Hash = _hashingService.ComputeHash(genesisBlock);
            Chain.Add(genesisBlock);
        }

        public bool TokenExists(string ticker)
        {
            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.Type == TransactionType.CreateToken &&
                        tx.Currency.Equals(ticker, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public decimal GetBalance(string address, string currency)
        {
            decimal balance = 0;

            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.Currency != currency)
                        continue;

                    if (tx.From == address)
                        balance -= tx.Amount;

                    if (tx.To == address)
                        balance += tx.Amount;

                    if (tx.From == address &&
                        currency == BaseCurrency)
                    {
                        balance -= tx.Fee;
                    }
                }
            }

            return balance;
        }

        public Dictionary<string, decimal> GetPortfolio(string address)
        {
            Dictionary<string, decimal> portfolio = new();

            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (!portfolio.ContainsKey(tx.Currency))
                        portfolio[tx.Currency] = 0;

                    if (tx.From == address)
                        portfolio[tx.Currency] -= tx.Amount;

                    if (tx.To == address)
                        portfolio[tx.Currency] += tx.Amount;

                    if (tx.From == address)
                    {
                        if (!portfolio.ContainsKey(BaseCurrency))
                            portfolio[BaseCurrency] = 0;

                        portfolio[BaseCurrency] -= tx.Fee;
                    }
                }
            }

            return portfolio;
        }

        public decimal GetPendingBalance(string address, string currency)
        {
            decimal balance = GetBalance(address, currency);

            foreach (var tx in PendingTransactions)
            {
                if (tx.Currency == currency &&
                    tx.From == address)
                {
                    balance -= tx.Amount;
                }

                if (currency == BaseCurrency &&
                    tx.From == address)
                {
                    balance -= tx.Fee;
                }
            }

            return balance;
        }

        public bool IsValid()
        {
            for (int i = 1; i < Chain.Count; i++)
            {
                var current = Chain[i];
                var previous = Chain[i - 1];

                if (current.Hash != _hashingService.ComputeHash(current))
                    return false;

                if (current.PreviousHash != previous.Hash)
                    return false;

                foreach (var tx in current.Transactions)
                {
                    if (tx.From == "COINBASE") continue;

                    string publicKeyBase64 = Convert.ToBase64String(tx.SenderPublicKey);
                    bool isValidSignature = _walletService.VerifySignature(publicKeyBase64, tx.GetDataToSign(), tx.Signature);

                    if (!isValidSignature)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[КРИТИЧНА ЗАГРОЗА]: Виявлено підроблену транзакцію в блоці {current.Index}!");
                        Console.ResetColor();
                        return false;
                    }
                }
            }
            return true;
        }

        public double GetChainWeight(List<Block> chain)
        {
            double weight = 0;
            foreach (var block in chain)
            {
                weight += Math.Pow(2, block.Difficulty);
            }
            return weight;
        }

        public bool IsChainValid(List<Block> externalChain)
        {
            for (int i = 1; i < externalChain.Count; i++)
            {
                Block currentBlock = externalChain[i];
                Block previousBlock = externalChain[i - 1];

                if (currentBlock.Hash != _hashingService.ComputeHash(currentBlock) ||
                    currentBlock.PreviousHash != previousBlock.Hash)
                {
                    return false;
                }
            }
            return true;
        }

        public bool ResolveConflicts(List<Block> externalChain)
        {
            if (externalChain == null || externalChain.Count == 0)
                return false;

            if (!IsChainValid(externalChain))
                return false;

            double currentChainWeight = GetChainWeight(Chain);
            double externalChainWeight = GetChainWeight(externalChain);

            if (externalChainWeight <= currentChainWeight)
                return false;

            Chain = externalChain;

            TotalMinted = Chain
    .SelectMany(b => b.Transactions)
    .Where(t =>
        t.From == "COINBASE" &&
        t.Currency == BaseCurrency)
    .Sum(t => t.Amount);
            Difficulty = Chain[^1].Difficulty;

            _fileStorageService.SaveBlockchain(Chain);
            Console.WriteLine($"[Consensus] Ланцюг замінено. Нова вага: {externalChainWeight} (була: {currentChainWeight}). TotalMinted: {TotalMinted}.");
            return true;
        }

        public bool ValidateEconomy()
        {
            decimal mintedBase = Chain
                .SelectMany(b => b.Transactions)
                .Where(t => t.From == "COINBASE" && t.Currency == BaseCurrency)
                .Sum(t => t.Amount);

            if (mintedBase > MaxSupply)
                return false;

            foreach (var block in Chain)
            {
                foreach (var tx in block.Transactions)
                {
                    if (tx.Type != TransactionType.CreateToken)
                        continue;

                    decimal mintedToken = Chain
                        .SelectMany(b => b.Transactions)
                        .Where(t => t.From == "COINBASE" &&
                                    t.Currency == tx.Currency)
                        .Sum(t => t.Amount);

                    if (mintedToken > tx.TotalSupply)
                        return false;
                }
            }

            return true;
        }

        private void AdjustDifficulty(Block latestBlock)
        {
            var windowStart = Chain[^_adjustmentInterval];
            double elapsed = (latestBlock.TimeStamp - windowStart.TimeStamp).TotalSeconds;
            double average = elapsed / _adjustmentInterval;

            if (average < _targetBlockTime * 0.8 && Difficulty < 8)
            {
                Difficulty++;
                Console.WriteLine($"[Difficulty] Підвищено до {Difficulty}");
            }
            else if (average > _targetBlockTime * 1.2 && Difficulty > 1)
            {
                Difficulty--;
                Console.WriteLine($"[Difficulty] Знижено до {Difficulty}");
            }
        }
    }
}