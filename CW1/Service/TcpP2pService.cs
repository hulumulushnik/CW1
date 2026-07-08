using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlockChainP411NEW.Models;
using BlockChainP411NEW.Services;
using CW1.Models;

namespace CW1.Services
{
    public class TcpP2pService
    {
        public readonly TcpListener _listener;
        private readonly ConcurrentDictionary<TcpClient, NetworkStream> _clients = new();
        private readonly BlockChainService _blockChainService;
        private readonly HashingService _hashingService = new();

        public TcpP2pService(BlockChainService blockChainService, int port)
        {
            _blockChainService = blockChainService;
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"P2P Server started on port {((IPEndPoint)_listener.LocalEndpoint).Port}");
            Task.Run(AcceptClientsAsync);
        }

        private async Task AcceptClientsAsync()
        {
            while (true)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var stream = client.GetStream();
                    _clients.TryAdd(client, stream);
                    Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
                    Task.Run(() => HandleClientAsync(client, stream));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, NetworkStream stream)
        {
            var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            while (client.Connected)
            {
                try
                {
                    int messageLength = reader.ReadInt32();
                    byte[] messageBytes = reader.ReadBytes(messageLength);
                    string messageJson = Encoding.UTF8.GetString(messageBytes);

                    if (!string.IsNullOrEmpty(messageJson))
                    {
                        ProcessMessage(messageJson);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling client: {ex.Message}");
                    break;
                }
            }

            _clients.TryRemove(client, out _);
            client.Dispose();
        }

        private void ProcessMessage(string messageJson)
        {
            var message = JsonSerializer.Deserialize<P2pMessage>(messageJson);
            if (message == null) return;

            switch (message.Type)
            {
                case MessageType.NewBlock:
                    var newBlock = JsonSerializer.Deserialize<Block>(message.Data);
                    if (newBlock == null) return;

                    string recomputedHash = _hashingService.ComputeHash(newBlock);
                    if (recomputedHash != newBlock.Hash)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[SECURITY] 🚨 Обнаружен фейковый блок!");
                        Console.WriteLine($"           Заявлений хеш:     {newBlock.Hash[..32]}...");
                        Console.WriteLine($"           Перерахований хеш: {recomputedHash[..32]}...");
                        Console.ResetColor();
                        return;
                    }

                    int requiredDifficulty = _blockChainService.Difficulty;
                    if (!HasRequiredDifficulty(newBlock.Hash, requiredDifficulty))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[SECURITY] 🚨 Обнаружен фейковый блок!");
                        Console.WriteLine($"           Хеш не відповідає складності мережі (Difficulty = {requiredDifficulty}).");
                        Console.WriteLine($"           Отриманий хеш: {newBlock.Hash[..32]}...");
                        Console.ResetColor();
                        return;
                    }

                    var lastBlock = _blockChainService.Chain[^1];
                    if (newBlock.Index == lastBlock.Index + 1 && newBlock.PreviousHash == lastBlock.Hash)
                    {
                        _blockChainService.Chain.Add(newBlock);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[P2P] ✅ Новий блок #{newBlock.Index} прийнято. Hash: {newBlock.Hash[..16]}...");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"[P2P] ⚠️  Блок #{newBlock.Index} не вписується в ланцюг (Index/PreviousHash mismatch).");
                    }
                    break;

                case MessageType.SyncChain:
                    var receivedChain = JsonSerializer.Deserialize<List<Block>>(message.Data);
                    if (receivedChain == null) return;

                    bool consensusReached = _blockChainService.ResolveConflicts(receivedChain);
                    if (consensusReached)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[P2P] ⚖️  Consensus: отриманий ланцюг важчий, синхронізовано. New length: {receivedChain.Count}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"[P2P] Отриманий ланцюг не важчий за наш — залишаємось на своєму.");
                    }
                    break;

                default:
                    Console.WriteLine($"[P2P] Unknown message type: {message.Type}");
                    break;
            }
        }

        private static bool HasRequiredDifficulty(string hash, int difficulty)
        {
            if (string.IsNullOrEmpty(hash) || difficulty <= 0)
                return difficulty <= 0;

            string target = new string('0', difficulty);
            return hash.StartsWith(target, StringComparison.Ordinal);
        }

        private readonly object _writeLock = new();

        private void BroadcastMessage(P2pMessage message)
        {
            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);

            foreach (var (client, stream) in _clients)
            {
                if (!client.Connected) continue;
                try
                {
                    lock (_writeLock)
                    {
                        var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                        writer.Write(messageLengthBytes, 0, messageLengthBytes.Length);
                        writer.Write(messageBytes, 0, messageBytes.Length);
                        writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to client: {ex.Message}");
                }
            }
        }

        public void BroadcastNewBlock(Block newBlock)
        {
            var message = new P2pMessage(MessageType.NewBlock, JsonSerializer.Serialize(newBlock));
            BroadcastMessage(message);
        }

        private void BroadcastSync()
        {
            var message = new P2pMessage(MessageType.SyncChain, JsonSerializer.Serialize(_blockChainService.Chain));
            BroadcastMessage(message);
        }

        public void SyncWithPeers()
        {
            if (_clients.IsEmpty)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Sync] Немає підключених пірів для синхронізації.");
                Console.ResetColor();
                return;
            }

            BroadcastSync();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[Sync] Ланцюг розіслано {_clients.Count} підключеним пірам для перевірки консенсусу.");
            Console.ResetColor();
        }

        public async Task ConnectToPeerAsync(string ipAddress, int port)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);
                var stream = client.GetStream();
                _clients.TryAdd(client, stream);
                Console.WriteLine($"Connected to peer: {ipAddress}:{port}");
                BroadcastSync();
                Task.Run(() => HandleClientAsync(client, stream));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to peer: {ex.Message}");
            }
        }
    }
}