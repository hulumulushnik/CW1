using BlockChainP411NEW.Models;
using BlockChainP411NEW.Services;
using CW1.Models;
using CW1.Services;
using System;
using System.Collections.Generic;
using System.Linq;



Console.WriteLine("Введіть порт для цієї ноди:");
var port = int.Parse(Console.ReadLine()!);

var displayService = new BlockChainDisplayService();
var hashingService = new HashingService();
var blockChain = new BlockChainService();
var walletService = new WalletService();
var vanityWalletService = new VanityWalletService(walletService);
var fileStorageService = new FileStorageService();


var wallets = new Dictionary<string, Wallet>(StringComparer.OrdinalIgnoreCase);
wallets["Alice"] = walletService.CreateWallet("Alice");
wallets["Bob"] = walletService.CreateWallet("Bob");

// P2P
var p2pService = new TcpP2pService(blockChain, port);
blockChain.SetP2pService(p2pService);
p2pService.Start();

Console.WriteLine("Введіть порт для підключення до іншого вузла (0 - пропустити):");
var peerPort = int.Parse(Console.ReadLine()!);
if (peerPort != 0)
{
    await p2pService.ConnectToPeerAsync("127.0.0.1", peerPort);
    Console.WriteLine("Підключено до іншого вузла.");
}


while (true)
{
    PrintMenu();
    Console.Write("Оберіть опцію: ");
    var input = Console.ReadLine();

    switch (input)
    {
        case "1": MineBlockMenu(); break;
        case "2": CreateTransactionMenu(); break;
        case "3": ShowBalanceMenu(); break;
        case "4": ValidateBlockchainMenu(); break;
        case "5": PrintBlockchainMenu(); break;
        case "6": ClearBlockchainMenu(); break;
        case "7": SyncWithPeersMenu(); break;
        case "8": MerkleTreeDemoMenu(); break;
        case "9": AttackSimulationMenu(); break;
        case "10": CreateWalletMenu(); break;
        case "11": ListWalletsMenu(); break;
        case "12": VanityWalletMenu(); break;
        case "13": SaveWalletsMenu(); break;
        case "14": LoadWalletsMenu(); break;
        case "15": HomeworkMerkleAttackDemo(); break;
        case "0":
            Console.WriteLine("Вихід...");
            return;
        default:
            Console.WriteLine("Невідома команда.");
            break;
    }
}

void PrintMenu()
{
    Console.WriteLine();
    Console.WriteLine("══════════════════════════════════════════");
    Console.WriteLine("  BlockChain Menu");
    Console.WriteLine("══════════════════════════════════════════");
    Console.WriteLine("  Блокчейн:");
    Console.WriteLine("   1. Замайнити блок");
    Console.WriteLine("   2. Створити транзакцію");
    Console.WriteLine("   3. Показати баланс гаманця");
    Console.WriteLine("   4. Перевірити валідність блокчейну");
    Console.WriteLine("   5. Показати весь блокчейн");
    Console.WriteLine("   6. Очистити блокчейн");
    Console.WriteLine("  Мережа (P2P):");
    Console.WriteLine("   7. Синхронізуватися з піррами");
    Console.WriteLine("   8. Демонстрація Merkle Tree / Proof");
    Console.WriteLine("   9. Імітація атаки (підмінений блок)");
    Console.WriteLine("  Гаманці:");
    Console.WriteLine("   10. Створити новий гаманець");
    Console.WriteLine("   11. Показати список гаманців");
    Console.WriteLine("   12. Намайнити vanity-гаманець (за префіксом)");
    Console.WriteLine("   13. Зберегти гаманці у файл");
    Console.WriteLine("   14. Завантажити гаманці з файлу");
    Console.WriteLine("  ДЗ:");
    Console.WriteLine("   15. Тест: Merkle Root і захист блоку від підміни");
    Console.WriteLine("   0. Вихід");
}

Wallet? SelectWallet(string prompt)
{
    if (wallets.Count == 0)
    {
        Console.WriteLine("Немає жодного гаманця. Спочатку створіть гаманець (пункт 10).");
        return null;
    }

    Console.WriteLine($"Доступні гаманці: {string.Join(", ", wallets.Keys)}");
    Console.Write(prompt);
    var name = Console.ReadLine();

    if (name != null && wallets.TryGetValue(name, out var wallet))
        return wallet;

    Console.WriteLine("Гаманець із таким ім'ям не знайдено.");
    return null;
}

string? ResolveRecipientAddress(string input)
{
    if (wallets.TryGetValue(input, out var wallet))
        return wallet.Address;

    return input; 
}

void MineBlockMenu()
{
    try
    {
        var miner = SelectWallet("Ім'я гаманця майнера: ");
        if (miner == null) return;

        Console.WriteLine("Майнінг блоку...");
        blockChain.MineBlock(miner.Address);
        Console.WriteLine("Блок успішно замайнено!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка майнінгу: {ex.Message}");
    }
}

void CreateTransactionMenu()
{
    try
    {
        var from = SelectWallet("Ім'я гаманця відправника: ");
        if (from == null) return;

        Console.Write("Отримувач (ім'я гаманця або адреса 0x...): ");
        var recipientInput = Console.ReadLine() ?? string.Empty;
        var to = ResolveRecipientAddress(recipientInput);

        Console.Write("Сума транзакції: ");
        var amount = decimal.Parse(Console.ReadLine()!);
        Console.Write("Комісія: ");
        var fee = decimal.Parse(Console.ReadLine()!);

        var tx = blockChain._transactionService.CreateTransaction(from, to, amount, fee);
        blockChain.AddTransactionToMempool(tx);
        Console.WriteLine($"Транзакцію додано: {tx.Id}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка: {ex.Message}");
    }
}

void ShowBalanceMenu()
{
    var wallet = SelectWallet("Ім'я гаманця: ");
    if (wallet == null) return;

    Console.WriteLine($"Баланс {wallet.Name}: {blockChain.GetBalance(wallet.Address)}");
    Console.WriteLine($"Баланс з урахуванням мемпулу: {blockChain.GetPendingBalance(wallet.Address)}");
}

void ValidateBlockchainMenu()
{
    displayService.PrintValidationResult(blockChain.IsValid());
}

void PrintBlockchainMenu()
{
    displayService.PrintBlockChain(blockChain.Chain);
}

void ClearBlockchainMenu()
{
    Console.Write("Ви впевнені? Всі блоки та транзакції буде видалено (y/n): ");
    if (Console.ReadLine()?.Trim().ToLower() == "y")
    {
        blockChain.ClearBlockchain();
    }
}

void SyncWithPeersMenu()
{
    p2pService.SyncWithPeers();
}

void MerkleTreeDemoMenu()
{
    Console.WriteLine();
    Console.WriteLine("══════════════════════════════════════════");
    Console.WriteLine("  ДЕМОНСТРАЦІЯ MERKLE TREE");
    Console.WriteLine("══════════════════════════════════════════");

    Console.WriteLine("\n[Крок 1] Створюємо 5 тестових транзакцій...");
    var demoTxs = new List<Transaction>();
    for (int i = 1; i <= 5; i++)
    {
        var tx = new Transaction($"0xSender{i:D2}", $"0xRecipient{i:D2}", i * 10m, Array.Empty<byte>());
        tx.Fee = i * 0.1m;
        demoTxs.Add(tx);
        Console.WriteLine($"  TX{i}: {tx.Id[..8]}... | {tx.From} -> {tx.To} | {tx.Amount}");
    }

    Console.WriteLine("\n[Крок 2] Побудова Merkle Tree (піраміда):");
    Console.WriteLine(new string('─', 42));
    string merkleRoot = hashingService.GetMerkleRoot(demoTxs, verbose: true);
    Console.WriteLine(new string('─', 42));
    Console.WriteLine($"  Merkle Root: {merkleRoot[..32]}...");

    var targetTx = demoTxs[2];
    string targetHash = hashingService.ComputeHash(targetTx.ToRowString());

    Console.WriteLine($"\n[Крок 3] Цільова транзакція (індекс 2):");
    Console.WriteLine($"  ID:   {targetTx.Id[..16]}...");
    Console.WriteLine($"  Hash: {targetHash[..32]}...");

    Console.WriteLine("\n[Крок 4] Merkle Proof (сусіди для відновлення кореня):");
    var proof = hashingService.GetMerkleProof(demoTxs, targetTx.Id);
    for (int i = 0; i < proof.Count; i++)
    {
        string side = proof[i].IsLeft ? "← Лівий сусід" : "→ Правий сусід";
        Console.WriteLine($"  [{i}] {side}: {proof[i].Hash[..32]}...");
    }

    bool isValid = hashingService.VerifyMerkleProof(targetHash, merkleRoot, proof);
    Console.WriteLine($"\n[Крок 5] VerifyMerkleProof (оригінальний доказ)  → {(isValid ? "✅ TRUE" : "❌ FALSE")}");

    if (proof.Count > 0)
    {
        var tamperedProof = new List<(string Hash, bool IsLeft)>(proof);
        var (origHash, origIsLeft) = tamperedProof[0];
        char tampered = origHash[0] == 'A' ? 'B' : 'A';
        tamperedProof[0] = (tampered + origHash[1..], origIsLeft);

        bool isValidTampered = hashingService.VerifyMerkleProof(targetHash, merkleRoot, tamperedProof);
        Console.WriteLine($"[Крок 6] VerifyMerkleProof (підроблений доказ) → {(isValidTampered ? "✅ TRUE" : "❌ FALSE — атаку виявлено!")}");
    }

    Console.WriteLine("\n[Крок 7 ⭐] Захист від CVE-2012-2459:");
    Console.WriteLine("  Додаємо дублікат останньої транзакції...");
    var attackTxs = new List<Transaction>(demoTxs) { demoTxs[^1] };
    try
    {
        hashingService.GetMerkleRoot(attackTxs);
        Console.WriteLine("  ⚠️  Атаку НЕ виявлено (помилка захисту)!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✅ Захист спрацював: {ex.Message}");
    }
}

void AttackSimulationMenu()
{
    try
    {
        var originalBlock = blockChain.Chain[^1];

        // Глибока копія через JSON серіалізацію
        var blockJson = System.Text.Json.JsonSerializer.Serialize(originalBlock);
        var tamperedBlock = System.Text.Json.JsonSerializer.Deserialize<Block>(blockJson)!;

        if (tamperedBlock.Transactions.Count == 0)
        {
            Console.WriteLine("Останній блок не має транзакцій для підміни.");
            return;
        }

        decimal originalAmount = tamperedBlock.Transactions[0].Amount;
        tamperedBlock.Transactions[0].Amount = 999999m; 

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[ATTACK] Надсилаємо отруєний блок #{tamperedBlock.Index}:");
        Console.WriteLine($"         Оригінальна сума TX[0]: {originalAmount}");
        Console.WriteLine($"         Підмінена сума TX[0]:   999999");
        Console.WriteLine($"         Хеш блоку (старий): {tamperedBlock.Hash[..32]}...");
        Console.ResetColor();

        p2pService.BroadcastNewBlock(tamperedBlock);
        Console.WriteLine("[ATTACK] Отруєний блок відправлено. Дивіться консоль Ноди Б!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка: {ex.Message}");
    }
}

void CreateWalletMenu()
{
    Console.Write("Ім'я нового гаманця: ");
    var name = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(name))
    {
        Console.WriteLine("Ім'я не може бути порожнім.");
        return;
    }

    if (wallets.ContainsKey(name))
    {
        Console.WriteLine("Гаманець із таким ім'ям вже існує.");
        return;
    }

    var wallet = walletService.CreateWallet(name);
    wallets[name] = wallet;
    Console.WriteLine($"Гаманець '{name}' створено. Адреса: {wallet.Address}");
}

void ListWalletsMenu()
{
    if (wallets.Count == 0)
    {
        Console.WriteLine("Гаманців ще немає.");
        return;
    }

    Console.WriteLine("Список гаманців:");
    foreach (var (name, wallet) in wallets)
    {
        Console.WriteLine($"  {name,-15} {wallet.Address}   баланс: {blockChain.GetBalance(wallet.Address)}");
    }
}

void VanityWalletMenu()
{
    Console.Write("Бажаний hex-префікс адреси (напр. abc): ");
    var prefix = Console.ReadLine() ?? string.Empty;

    Console.WriteLine("Майнінг vanity-гаманця... (може зайняти час залежно від довжини префіксу)");
    var (wallet, attempts) = vanityWalletService.MineWallet(prefix);

    var name = wallet.Name;
    int suffix = 1;
    while (wallets.ContainsKey(name))
        name = $"{wallet.Name}{suffix++}";

    wallets[name] = wallet;
    Console.WriteLine($"Знайдено за {attempts} спроб(и). Гаманець '{name}': {wallet.Address}");
}

void SaveWalletsMenu()
{
    fileStorageService.SaveWallets(wallets.Values.ToList());
    Console.WriteLine($"Збережено {wallets.Count} гаманців у файл.");
}

void LoadWalletsMenu()
{
    var loaded = fileStorageService.LoadWallets();
    if (loaded.Count == 0)
    {
        Console.WriteLine("Файл гаманців порожній або не знайдений.");
        return;
    }

    foreach (var wallet in loaded)
    {
        var name = wallet.Name;
        int suffix = 1;
        while (wallets.ContainsKey(name))
            name = $"{wallet.Name}_{suffix++}";

        wallets[name] = wallet;
    }

    Console.WriteLine($"Завантажено {loaded.Count} гаманців із файлу.");
}


void HomeworkMerkleAttackDemo()
{
    Console.WriteLine();
    Console.WriteLine("══════════════════════════════════════════");
    Console.WriteLine("  ДЗ: Merkle Root у блоці — тест на підміну транзакції");
    Console.WriteLine("══════════════════════════════════════════");

    try
    {        if (!wallets.ContainsKey("HW_Alice")) wallets["HW_Alice"] = walletService.CreateWallet("HW_Alice");
        if (!wallets.ContainsKey("HW_Bob")) wallets["HW_Bob"] = walletService.CreateWallet("HW_Bob");
        var alice = wallets["HW_Alice"];
        var bob = wallets["HW_Bob"];

        Console.WriteLine("\n[Крок 1] Майнимо блок, щоб Alice отримала винагороду (потрібні кошти для переказів)...");
        blockChain.MineBlock(alice.Address);
        Console.WriteLine($"  Баланс Alice: {blockChain.GetBalance(alice.Address)}");

        Console.WriteLine("\n[Крок 2] Створюємо транзакції Alice -> Bob...");
        var tx1 = blockChain._transactionService.CreateTransaction(alice, bob.Address, 1m, 0.1m);
        blockChain.AddTransactionToMempool(tx1);
        Console.WriteLine($"  TX1: {tx1.Id[..8]}... сума {tx1.Amount}, комісія {tx1.Fee}");

        var tx2 = blockChain._transactionService.CreateTransaction(alice, bob.Address, 2m, 0.2m);
        blockChain.AddTransactionToMempool(tx2);
        Console.WriteLine($"  TX2: {tx2.Id[..8]}... сума {tx2.Amount}, комісія {tx2.Fee}");

        Console.WriteLine("\n[Крок 3] Майнимо блок з транзакціями...");
        blockChain.MineBlock(alice.Address);

        var minedBlock = blockChain.Chain[^1];
        Console.WriteLine($"  ✅ Блок #{minedBlock.Index} успішно замайнено. Транзакцій у блоці: {minedBlock.Transactions.Count}");
        Console.WriteLine($"     Hash: {minedBlock.Hash[..32]}...");

        Console.WriteLine("\n[Крок 4] Перевірка валідності ДО атаки:");
        displayService.PrintValidationResult(blockChain.IsValid());

        int targetIndex = minedBlock.Transactions.Count > 1 ? 1 : 0;
        decimal originalAmount = minedBlock.Transactions[targetIndex].Amount;

        Console.WriteLine("\n[Крок 5 - АТАКА] Хакер напряму підміняє Amount транзакції в blockChain.Chain[...]:");
        Console.WriteLine($"  blockChain.Chain[{minedBlock.Index}].Transactions[{targetIndex}].Amount = 999;");
        minedBlock.Transactions[targetIndex].Amount = 999m;
        Console.WriteLine($"  Було: {originalAmount}  →  Стало: {minedBlock.Transactions[targetIndex].Amount}");

        Console.WriteLine("\n[Крок 6] Перевірка валідності ПІСЛЯ атаки:");
        bool isValidAfterAttack = blockChain.IsValid();
        displayService.PrintValidationResult(isValidAfterAttack);

        Console.ForegroundColor = isValidAfterAttack ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine(isValidAfterAttack
            ? "⚠️  ПОМИЛКА ЗАХИСТУ: підміну не виявлено!"
            : "✅ Захист спрацював: зміна суми змінила лист дерева → Merkle Root → Hash блоку. IsValid() = false.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка виконання демонстрації: {ex.Message}");
    }
}