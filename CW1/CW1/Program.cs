using BlockChainP411NEW.Models;
using BlockChainP411NEW.Services;
using System;
using System.Linq;

var blockChainService = new BlockChainService();
var walletService = blockChainService._walletService;

var alice = walletService.CreateWallet("Alice");
var miner = walletService.CreateWallet("Miner");

Console.WriteLine("=== Тест 1: Халвінг ===\n");

int minedBlocks = 0;
bool aliceFunded = false;

while (blockChainService.GetCurrentReward() > 0)
{
    if (!aliceFunded && blockChainService.GetPendingBalance(miner.Address) >= 30)
    {
        var fundingTx = blockChainService._transactionService.CreateTransaction(miner, alice.Address, 30, fee: 0);
        blockChainService.AddTransactionToMempool(fundingTx);
        aliceFunded = true;
        Console.WriteLine("[Інфо] Міну відправлено 30 монет Алісі (для наступного тесту).");
    }

    blockChainService.MineBlock(miner.Address);
    var last = blockChainService.Chain.Last();
    minedBlocks++;

    Console.WriteLine($"Блок #{last.Index} видобуто | Наступна нагорода: {blockChainService.GetCurrentReward()}");
}

Console.WriteLine($"\nЕмісію зупинено. Всього видобуто {minedBlocks} блоків.");
Console.WriteLine($"Баланс Аліси: {blockChainService.GetBalance(alice.Address)}");
Console.WriteLine($"Баланс Майнера: {blockChainService.GetBalance(miner.Address)}");
Console.WriteLine($"Загальна емісія (TotalMinted): {blockChainService.TotalMinted}");

Console.WriteLine("\n=== Тест 2: Дилема майнера (порожній мемпул, нагорода = 0) ===\n");
try
{
    blockChainService.MineBlock(miner.Address);
    Console.WriteLine("[Помилка тесту] Блок був видобутий, хоча цього не мало статись!");
}
catch (InvalidOperationException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(ex.Message);
    Console.ResetColor();
}

Console.WriteLine("\n=== Тест 3: Спалювання комісій (EIP-1559) ===\n");

decimal minerBalanceBefore = blockChainService.GetBalance(miner.Address);
Console.WriteLine($"Баланс майнера ДО блоку: {minerBalanceBefore}");

var feeTx = blockChainService._transactionService.CreateTransaction(alice, miner.Address, amount: 5, fee: 10);
blockChainService.AddTransactionToMempool(feeTx);

blockChainService.MineBlock(miner.Address);

decimal minerBalanceAfter = blockChainService.GetBalance(miner.Address);
decimal minerGain = minerBalanceAfter - minerBalanceBefore;

Console.WriteLine($"Баланс майнера ПІСЛЯ блоку: {minerBalanceAfter}");
Console.WriteLine($"Приріст балансу майнера: {minerGain} " +
    $"(5 монет — переказ від Аліси + 5 монет — половина комісії; інші 5 монет комісії спалено назавжди)");