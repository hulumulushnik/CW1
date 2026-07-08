using BlockChainP411NEW.Models;
using System;

namespace BlockChainP411NEW.Services
{
    public class TransactionService
    {
        private readonly WalletService _walletService;

        public BlockChainService BlockChain { get; set; }

        public TransactionService(WalletService walletService)
        {
            _walletService = walletService;
        }

        public Transaction CreateTransaction(
            Wallet walletFrom,
            string to,
            decimal amount,
            decimal fee = 0m,
            byte[] senderPublicKey = null,
            decimal currentBalance = 0,
            string currency = "BASE")
        {
            if (BlockChain != null)
            {
                decimal tokenBalance =
                    BlockChain.GetPendingBalance(walletFrom.Address, currency);

                decimal baseBalance =
                    BlockChain.GetPendingBalance(walletFrom.Address, "BASE");

                if (tokenBalance < amount)
                    throw new ArgumentException(
                        $"Недостатньо {currency}. Доступно: {tokenBalance}");

                if (baseBalance < fee)
                    throw new ArgumentException(
                        $"Недостатньо BASE для комісії.");
            }

            var tx = new Transaction(
                walletFrom.Address,
                to,
                amount,
                walletFrom.PublicKey);

            tx.Currency = currency;
            tx.Fee = fee;
            tx.Type = TransactionType.Transfer;

            tx.Signature = walletFrom.Sign(tx.GetDataToSign());

            var validation = ValidateTransaction(tx);

            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

            return tx;
        }

        public Transaction CreateToken(
            Wallet owner,
            string ticker,
            decimal supply)
        {
            if (BlockChain == null)
                throw new InvalidOperationException();

            decimal baseBalance =
                BlockChain.GetPendingBalance(owner.Address, "BASE");

            if (baseBalance < BlockChainService.ICOFee)
                throw new ArgumentException(
                    "Недостатньо BASE для створення токена.");

            if (BlockChain.TokenExists(ticker))
                throw new ArgumentException(
                    "Токен вже існує.");

            var tx = new Transaction(
                owner.Address,
                owner.Address,
                0,
                owner.PublicKey);

            tx.Type = TransactionType.CreateToken;

            tx.Currency = ticker.ToUpper();

            tx.TotalSupply = supply;

            tx.Fee = BlockChainService.ICOFee;

            tx.Signature = owner.Sign(tx.GetDataToSign());

            var validation = ValidateTransaction(tx);

            if (!validation.IsValid)
                throw new ArgumentException(validation.ErrorMessage);

            return tx;
        }

        public (bool IsValid, string ErrorMessage)
            ValidateTransaction(Transaction transaction)
        {
            if (transaction == null)
                return (false, "Transaction is null.");

            if (string.IsNullOrWhiteSpace(transaction.From))
                return (false, "Sender cannot be empty.");

            if (string.IsNullOrWhiteSpace(transaction.To))
                return (false, "Recipient cannot be empty.");

            if (transaction.Type == TransactionType.Transfer)
            {
                if (transaction.Amount <= 0)
                    return (false, "Amount must be greater than zero.");
            }

            if (transaction.Type == TransactionType.CreateToken)
            {
                if (transaction.TotalSupply <= 0)
                    return (false, "Invalid token supply.");

                if (string.IsNullOrWhiteSpace(transaction.Currency))
                    return (false, "Ticker cannot be empty.");

                if (BlockChain.TokenExists(transaction.Currency))
                    return (false,
                        "Token with this ticker already exists.");
            }

            if (transaction.From == "COINBASE")
                return (true, "");

            if (!IsValidCryptoAddress(transaction.From))
                return (false, "Invalid sender.");

            if (!IsValidCryptoAddress(transaction.To))
                return (false, "Invalid recipient.");

            if (transaction.Type == TransactionType.Transfer)
            {
                if (transaction.Currency != "BASE")
                {
                    if (!BlockChain.TokenExists(transaction.Currency))
                        return (false,
                            "Token doesn't exist.");
                }
            }

            if (transaction.Signature == null ||
                transaction.Signature.Length == 0)
            {
                return (false,
                    "Signature missing.");
            }

            if (transaction.SenderPublicKey == null ||
                transaction.SenderPublicKey.Length == 0)
            {
                return (false,
                    "Public key missing.");
            }

            string publicKeyBase64 =
                Convert.ToBase64String(transaction.SenderPublicKey);

            bool ok =
                _walletService.VerifySignature(
                    publicKeyBase64,
                    transaction.GetDataToSign(),
                    transaction.Signature);

            if (!ok)
            {
                return (false,
                    "Invalid digital signature.");
            }

            return (true, "");
        }

        private bool IsValidCryptoAddress(string address)
        {
            if (address.Length != 42)
                return false;

            if (!address.StartsWith("0x",
                StringComparison.OrdinalIgnoreCase))
                return false;

            for (int i = 2; i < address.Length; i++)
            {
                if (!char.IsLetterOrDigit(address[i]))
                    return false;
            }

            return true;
        }
    }
}