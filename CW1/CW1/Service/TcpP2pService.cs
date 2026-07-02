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
                        Console.WriteLine($"Received: {messageJson}");
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
                    var newBlock = JsonSerializer.Deserialize<BlockChainP411NEW.Models.Block>(message.Data);
                    if (newBlock == null) return;
                    var lastBlock = _blockChainService.Chain[^1];
                    if (newBlock.Index == lastBlock.Index + 1 && newBlock.PreviousHash == lastBlock.Hash)
                    {
                        _blockChainService.Chain.Add(newBlock);
                        Console.WriteLine($"New block added: {newBlock.Hash}");
                    }
                    else
                    {
                        Console.WriteLine($"Invalid block received: {newBlock.Hash}");
                    }
                    break;

                case MessageType.SyncChain:
                    var receivedChain = JsonSerializer.Deserialize<List<BlockChainP411NEW.Models.Block>>(message.Data);
                    if (receivedChain == null) return;
                    if (receivedChain.Count > _blockChainService.Chain.Count)
                    {
                        _blockChainService.Chain = receivedChain;
                        Console.WriteLine($"Blockchain synchronized. New length: {receivedChain.Count}");
                    }
                    break;

                default:
                    Console.WriteLine($"Unknown message type: {message.Type}");
                    break;
            }
        }

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
                    // Використовуємо leaveOpen: true, щоб не закривати потік
                    var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                    writer.Write(messageLengthBytes, 0, messageLengthBytes.Length);
                    writer.Write(messageBytes, 0, messageBytes.Length);
                    writer.Flush();
                    Console.WriteLine($"Broadcasted: {messageJson}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to client: {ex.Message}");
                }
            }
        }

        public void BroadcastNewBlock(BlockChainP411NEW.Models.Block newBlock)
        {
            var message = new P2pMessage(MessageType.NewBlock, JsonSerializer.Serialize(newBlock));
            BroadcastMessage(message);
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
                Task.Run(() => HandleClientAsync(client, stream));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to peer: {ex.Message}");
            }
        }
    }
}