using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NeutrinoOracles.Common.Converters;
using NeutrinoOracles.Common.Helpers;
using NeutrinoOracles.Common.Models;
using NeutrinoOracles.PacemakerOracle.Models;
using WavesCS;

namespace NeutrinoOracles.PacemakerOracle
{
    class Program
    {
        private const int Wavelet = 100000000;
        private const int Pauli = 100;
        private const int MinBondGenerated = 10;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
#if DEBUG
                .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
#elif RELEASE
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
#endif
                .AddCommandLine(args)
                .Build();
            
            var settings = configuration.Get<Settings>();
            var seed = args[0];
            
            var wavesHelper = new WavesHelper(settings.NodeUrl);
            var account = PrivateKeyAccount.CreateFromSeed(seed, settings.ChainId);
            var node = new Node(settings.NodeUrl, settings.ChainId);

            while (true)
            {
                try
                {
                    var height = await wavesHelper.GetHeight();

                    Logger.Info("New height: " + height);
                    Logger.Info("Init");

                    var neutrinoContractData = AccountDataConverter.ToNeutrinoAccountData(
                        await wavesHelper.GetDataByAddress(settings.ContractAddress));
                    var controlContractData = AccountDataConverter.ToControlAccountData(
                        await wavesHelper.GetDataByAddress(neutrinoContractData.ControlContractAddress));
                    var auctionControlData = AccountDataConverter.ToAuctionAccountData(
                        await wavesHelper.GetDataByAddress(neutrinoContractData.AuctionContractAddress));

                    var totalNeutrinoSupply = await wavesHelper.GetTotalSupply(neutrinoContractData.NeutrinoAssetId);
                    var neutrinoBalance = await wavesHelper.GetBalance(settings.ContractAddress,
                        neutrinoContractData.NeutrinoAssetId);
                    var wavesBalance = await wavesHelper.GetBalance(settings.ContractAddress);

                    Logger.Info($"Price:{controlContractData.Price}");

                    foreach (var (address, _) in neutrinoContractData.WithdrawBalanceLockedByAddress.Where(x=>x.Value > 0))
                    {
                        var withdrawBlock = neutrinoContractData.WithdrawBalanceUnlockBlockByAddress.GetValueOrDefault(address);
                        if (height <= withdrawBlock)
                            continue;

                        var indexes = controlContractData.PriceHeightByIndex.Where(x=> x.Value <= withdrawBlock).ToArray();
                        
                        if(!indexes.Any())
                            break;
                        
                        var maxValue = indexes.Max(y => y.Value);
                        var index = Convert.ToInt64(indexes.FirstOrDefault(x=> x.Value == maxValue).Key);
                        
                        var withdrawTx = node.InvokeScript(account, settings.ContractAddress, "withdraw",
                            new List<object>() {address, index});
                        var txId = await wavesHelper.WaitTxAndGetId(withdrawTx);
                        Logger.Info($"Withdraw tx id:{txId} (Address:{address})");
                    }

                    var supply = totalNeutrinoSupply - neutrinoBalance +
                                 neutrinoContractData.SwapNeutrinoLockedBalance;
                    var reserve = wavesBalance - neutrinoContractData.SwapWavesLockedBalance;

                    Logger.Info($"Supply:{supply}");
                    Logger.Info($"Reserve:{reserve}");
                    
                    var bondAuctionBalance = await wavesHelper.GetBalance(neutrinoContractData.AuctionContractAddress,
                        neutrinoContractData.BondAssetId);
                    var deficit = (supply - reserve * controlContractData.Price / 100 * Pauli / Wavelet)/Pauli;

                    Logger.Info($"Deficit:{deficit}");
                    Logger.Info($"BondBalance:{bondAuctionBalance}");

                    if (deficit  - bondAuctionBalance >= MinBondGenerated)
                    {
                        Logger.Info("Generate bond");
                        var generateBondTx = node.InvokeScript(account, settings.ContractAddress,
                            "generateBond", null);
                        var txId = await wavesHelper.WaitTxAndGetId(generateBondTx);
                        Logger.Info($"Generate bond tx id:{txId}");
                    }
                    
                    if (deficit < 0 && !string.IsNullOrEmpty(neutrinoContractData.Orderbook))
                    {
                        Logger.Info("Execute order for liquidation");

                        var orders = neutrinoContractData.Orderbook.Split("_");
                        long totalExecute = 0;
                        var surplus = Math.Abs(deficit);
                        foreach (var order in orders.Where(x => !string.IsNullOrEmpty(x)))
                        {
                            var total = neutrinoContractData.TotalByOrder[order];
                            var totalFilled = neutrinoContractData.FilledTotalByOrder?.GetValueOrDefault(order) ?? 0;
                            var amount = total - totalFilled;
                            if (totalExecute >= surplus)
                                break;

                            totalExecute += amount;

                            var exTx = node.InvokeScript(account, settings.ContractAddress, "executeOrder",
                                null);
                            var txId = await wavesHelper.WaitTxAndGetId(exTx);
                            Logger.Info($"Execute order tx id:{txId}");
                        }
                    }else  if (deficit > 0 && bondAuctionBalance > 0 && !string.IsNullOrEmpty(auctionControlData.Orderbook))
                    {
                        Logger.Info("Execute order for auction");

                        var orders = auctionControlData.Orderbook.Split("_");
                        long totalExecute = 0;
                        foreach (var order in orders.Where(x => !string.IsNullOrEmpty(x)))
                        {
                            var total = auctionControlData.TotalByOrder[order];
                            var totalFilled = auctionControlData.FilledTotalByOrder?.GetValueOrDefault(order) ?? 0;
                            var amount = (total - totalFilled) / Pauli * 100 /
                                         auctionControlData.PriceByOrder[order];
                            if (totalExecute >= deficit)
                                break;

                            totalExecute += amount;

                            var exTx = node.InvokeScript(account, neutrinoContractData.AuctionContractAddress,
                                "executeOrder", null);
                            var exTxId = await wavesHelper.WaitTxAndGetId(exTx);
                            Logger.Info($"Execute order tx id:{exTxId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }

                await Task.Delay(TimeSpan.FromSeconds(settings.TimeoutSec));
            }
        }
    }
}