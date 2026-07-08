using BlockChainP411NEW.Models;

namespace BlockChainP411NEW.Services
{
    public class VanityWalletService
    {
        private readonly WalletService _walletService;

        public VanityWalletService(WalletService walletService)
        {
            _walletService = walletService;
        }

        public (Wallet wallet, int attempts) MineWallet(string desiredPrefix)
        {
            int attempts = 0;
            string targetPrefix = "0x" + desiredPrefix.ToLower();

            while (true)
            {
                attempts++;

                var wallet = _walletService.CreateWallet("Vanity");

                if (wallet.Address.StartsWith(targetPrefix))
                {
                    wallet.Name = "MyVanityWallet";
                    return (wallet, attempts);
                }
            }
        }
    }
}