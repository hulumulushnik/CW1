using BlockChainP411NEW.Models;
using BlockChainP411NEW.Services;
using CW1.Models;
using CW1.Services;
using System;
using System.Collections.Generic;

Console.WriteLine("Введіть порт для цієї ноди:");
var port = int.Parse(Console.ReadLine()!);

// Ініціалізація сервісів
var displayService = new BlockChainDisplayService();
var hashingService = new HashingService();
var blockChain = new BlockChainService();
var walletService = new WalletService();

// Ініціалізація P2P сервісу
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

// ════════════════════════════════════════════════════════════════
//  ДЕМОНСТРАЦІЯ MERKLE TREE
//  (незалежно від стану блокчейну — транзакції створюються напряму)
// ════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("══════════════════════════════════════════");
Console.WriteLine("  ДЕМОНСТРАЦІЯ MERKLE TREE");
Console.WriteLine("══════════════════════════════════════════");

// Створюємо 5 тестових транзакцій напряму, без перевірки балансу
Console.WriteLine("\n[Крок 1] Створюємо 5 тестових транзакцій...");
var demoTxs = new List<Transaction>();
for (int i = 1; i <= 5; i++)
{
    var tx = new Transaction($"0xSender{i:D2}", $"0xRecipient{i:D2}", i * 10m, Array.Empty<byte>());
    tx.Fee = i * 0.1m;
    demoTxs.Add(tx);
    Console.WriteLine($"  TX{i}: {tx.Id[..8]}... | {tx.From} -> {tx.To} | {tx.Amount}");
}

// ── Крок 2: Будуємо Merkle Root — консоль покаже піраміду ──
Console.WriteLine("\n[Крок 2] Побудова Merkle Tree (піраміда):");
Console.WriteLine(new string('─', 42));
string merkleRoot = hashingService.GetMerkleRoot(demoTxs);
Console.WriteLine(new string('─', 42));
Console.WriteLine($"  Merkle Root: {merkleRoot[..32]}...");

// ── Крок 3: Беремо 3-тю транзакцію (індекс 2) ──
var targetTx = demoTxs[2];
string targetHash = hashingService.ComputeHash(targetTx.ToRowString());

Console.WriteLine($"\n[Крок 3] Цільова транзакція (індекс 2):");
Console.WriteLine($"  ID:   {targetTx.Id[..16]}...");
Console.WriteLine($"  Hash: {targetHash[..32]}...");

// ── Крок 4: Генеруємо доказ ──
Console.WriteLine("\n[Крок 4] Merkle Proof (сусіди для відновлення кореня):");
var proof = hashingService.GetMerkleProof(demoTxs, targetTx.Id);
for (int i = 0; i < proof.Count; i++)
{
    string side = proof[i].IsLeft ? "← Лівий сусід" : "→ Правий сусід";
    Console.WriteLine($"  [{i}] {side}: {proof[i].Hash[..32]}...");
}

// ── Крок 5: Перевірка — має повернути true ──
bool isValid = hashingService.VerifyMerkleProof(targetHash, merkleRoot, proof);
Console.WriteLine($"\n[Крок 5] VerifyMerkleProof (оригінальний доказ)  → {(isValid ? "✅ TRUE" : "❌ FALSE")}");

// ── Крок 6: Підміна одного байта → false ──
if (proof.Count > 0)
{
    var tamperedProof = new List<(string Hash, bool IsLeft)>(proof);
    var (origHash, origIsLeft) = tamperedProof[0];
    char tampered = origHash[0] == 'A' ? 'B' : 'A';
    tamperedProof[0] = (tampered + origHash[1..], origIsLeft);

    bool isValidTampered = hashingService.VerifyMerkleProof(targetHash, merkleRoot, tamperedProof);
    Console.WriteLine($"[Крок 6] VerifyMerkleProof (підроблений доказ) → {(isValidTampered ? "✅ TRUE" : "❌ FALSE — атаку виявлено!")}");
}

// ── Крок 7 (⭐): CVE-2012-2459 ──
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

// ════════════════════════════════════════════════════════════════
//  ГОЛОВНЕ МЕНЮ
// ════════════════════════════════════════════════════════════════
var menuAlice = walletService.CreateWallet("Alice");
var menuBob = walletService.CreateWallet("Bob");

while (true)
{
    Console.WriteLine();
    Console.WriteLine("BlockChain Menu:");
    Console.WriteLine("  '1'. Mine Block;");
    Console.WriteLine("  '2'. Create Transaction;");
    Console.WriteLine("  '3'. Show Alice Balance;");
    Console.WriteLine("  '4'. Show Bob Balance;");
    Console.WriteLine("  '5'. Validate BlockChain;");
    Console.WriteLine("  '6'. Print BlockChain;");
    Console.WriteLine("  '7'. Exit;");
    Console.WriteLine("  '10'. Імітація атаки (підмінений блок);");
    Console.Write("Оберіть опцію: ");

    var input = Console.ReadLine();

    switch (input)
    {
        case "1":
            try
            {
                Console.WriteLine("Майнінг блоку...");
                blockChain.MineBlock(menuAlice.Address);
                Console.WriteLine("Блок успішно замайнено!");
            }
            catch (Exception ex) { Console.WriteLine($"Помилка майнінгу: {ex.Message}"); }
            break;

        case "2":
            try
            {
                Console.Write("Сума транзакції: ");
                var amount = decimal.Parse(Console.ReadLine()!);
                Console.Write("Комісія: ");
                var fee = decimal.Parse(Console.ReadLine()!);
                var tx = blockChain._transactionService.CreateTransaction(menuAlice, menuBob.Address, amount, fee);
                blockChain.AddTransactionToMempool(tx);
                Console.WriteLine($"Транзакцію додано: {tx.Id}");
            }
            catch (Exception ex) { Console.WriteLine($"Помилка: {ex.Message}"); }
            break;

        case "3":
            Console.WriteLine($"Баланс Alice: {blockChain.GetBalance(menuAlice.Address)}");
            break;

        case "4":
            Console.WriteLine($"Баланс Bob: {blockChain.GetBalance(menuBob.Address)}");
            break;

        case "5":
            displayService.PrintValidationResult(blockChain.IsValid());
            break;

        case "6":
            displayService.PrintBlockChain(blockChain.Chain);
            break;

        case "7":
            Console.WriteLine("Вихід...");
            return;


        case "10":
            // ── Імітація атаки: підмінюємо транзакцію в копії блоку ──
            try
            {
                var originalBlock = blockChain.Chain[^1];

                // Глибока копія через JSON серіалізацію
                var blockJson = System.Text.Json.JsonSerializer.Serialize(originalBlock);
                var tamperedBlock = System.Text.Json.JsonSerializer.Deserialize<BlockChainP411NEW.Models.Block>(blockJson)!;

                if (tamperedBlock.Transactions.Count == 0)
                {
                    Console.WriteLine("Останній блок не має транзакцій для підміни.");
                    break;
                }

                decimal originalAmount = tamperedBlock.Transactions[0].Amount;
                tamperedBlock.Transactions[0].Amount = 999999m; // підміна суми
                // Hash блоку залишаємо СТАРИМ — як справжній хакер

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
            break;

        default:
            Console.WriteLine("Невідома команда.");
            break;
    }
}