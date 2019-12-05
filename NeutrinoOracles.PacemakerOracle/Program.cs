using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NeutrinoOracles.Common.Converters;
using NeutrinoOracles.Common.Helpers;
using NeutrinoOracles.Common.Models;
using NeutrinoOracles.PacemakerOracle.Models;
using Newtonsoft.Json.Linq;
using WavesCS;

namespace NeutrinoOracles.PacemakerOracle
{
    class Program
    {
        private const int DeficitOffset = 5;
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
            
            var wavesHelper = new WavesHelper(settings.NodeUrl, settings.ChainId);
            var account = PrivateKeyAccount.CreateFromSeed(seed, settings.ChainId);
            var node = new Node(settings.NodeUrl, settings.ChainId);
            var contractPubKey = Base58.Decode((settings.ContractPubKey));
            var contractAddress = AddressEncoding.GetAddressFromPublicKey(contractPubKey, settings.ChainId);
            while (true)
            {
                try
                {
                    var height = await wavesHelper.GetHeight();

                    Logger.Info("New height: " + height);
                    Logger.Info("Init");

                    var neutrinoContractData = AccountDataConverter.ToNeutrinoAccountData(
                        await wavesHelper.GetDataByAddress(contractAddress));
                    var controlContractData = AccountDataConverter.ToControlAccountData(
                        await wavesHelper.GetDataByAddress(neutrinoContractData.ControlContractAddress));
                    var auctionControlData = AccountDataConverter.ToAuctionAccountData(
                        await wavesHelper.GetDataByAddress(neutrinoContractData.AuctionContractAddress));
                    var liquidationControlData = AccountDataConverter.ToLiquidationAccountData(
                        await wavesHelper.GetDataByAddress(neutrinoContractData.LiquidationContractAddress));
                    
                    var totalNeutrinoSupply = await wavesHelper.GetTotalSupply(neutrinoContractData.NeutrinoAssetId);
                    var neutrinoBalance = await wavesHelper.GetBalance(contractAddress, neutrinoContractData.NeutrinoAssetId);
                    var wavesBalance = await wavesHelper.GetBalance(contractAddress);

                    Logger.Info($"Price:{controlContractData.Price}");
                    
                    var neutrinoContractBalance = await wavesHelper.GetDetailsBalance(contractAddress);

                    var addresses = new List<string>();
                    if(neutrinoContractData.BalanceLockNeutrinoByUser != null)
                        addresses.AddRange(neutrinoContractData.BalanceLockNeutrinoByUser.Where(x=>x.Value > 0).Select(x=>x.Key));
                    if(neutrinoContractData.BalanceLockWavesByUser != null)
                        addresses.AddRange(neutrinoContractData.BalanceLockWavesByUser?.Where(x=>x.Value > 0).Select(x=>x.Key));

                    long totalWithdraw = 0;
                    foreach (var address in addresses)
                    {
                        var withdrawBlock = neutrinoContractData.BalanceUnlockBlockByAddress.GetValueOrDefault(address);
                        if (height < withdrawBlock)
                            continue;

                        var indexes = controlContractData.PriceHeightByIndex.Where(x => x.Value >= withdrawBlock).ToList();
                        
                        if(!indexes.Any())
                            continue;
                        
                        var indexString = indexes.Min(x => x.Key);
                        var index = Convert.ToInt64(indexString);
                        var heightByIndex = controlContractData.PriceHeightByIndex[indexString];
                        var priceByHeight = controlContractData.PriceByHeight[Convert.ToString(heightByIndex)];
                        var withdrawNeutrinoAmount = neutrinoContractData.BalanceLockNeutrinoByUser.GetValueOrDefault(address);

                        if (withdrawNeutrinoAmount > 0)
                        {
                            var availableBalance = neutrinoContractBalance.Available - totalWithdraw;
                            var wavesAmount = CurrencyConvert.NeutrinoToWaves(withdrawNeutrinoAmount, priceByHeight);

                            if (wavesAmount > availableBalance)
                            {
                                if (!settings.Leasing.IsLeasingProvider)
                                    continue;

                                var totalLeasingCancelAmount = 0L;
                                var activeLeaseTxs = await wavesHelper.GetActiveLease(settings.Leasing.NodeAddress);
                                foreach (var leasingTx in activeLeaseTxs.OrderBy(x=>x.Amount))
                                {
                                    if(totalLeasingCancelAmount >= wavesAmount)
                                        break;
                                    
                                    totalLeasingCancelAmount += leasingTx.Amount;
                                    var cancelLease = new CancelLeasingTransaction(settings.ChainId, contractPubKey, leasingTx.Id, 0.005m);
                                    cancelLease.Sign(account);
                                    // shit code. Bug in wavesCs
                                    var json = JObject.Parse(cancelLease.GetJsonWithSignature().ToJson());
                                    json.Add("proofs", new JArray { cancelLease.Proofs.Take(Array.FindLastIndex(cancelLease.Proofs, p => p != null && p.Length > 0) + 1)
                                        .Select(p => p == null ? "" : p.ToBase58())
                                        .ToArray()
                                    });
                                    json.Add("version", 2);
                                    json.Add("chainId", settings.ChainId);
                                    var id = await wavesHelper.WaitTxAndGetId(await wavesHelper.Broadcast(json.ToString()));
                                    Logger.Info($"Cancel lease tx:{id} (LeaseId:{cancelLease.LeaseId})");
                                }
                            }

                            totalWithdraw += wavesAmount;
                        }

                        var withdrawTx = node.InvokeScript(account, contractAddress, "withdraw",
                            new List<object>() {address, index});
                        var txId = await wavesHelper.WaitTxAndGetId(withdrawTx);
                        Logger.Info($"Withdraw tx id:{txId} (Address:{address})");
                    }
                    
                    if (settings.Leasing.IsLeasingProvider)
                    {
                        var leasingAmountForOneTxInWavelet = settings.Leasing.LeasingAmountForOneTx * CurrencyConvert.Wavelet;
                        
                        var minWaves = Convert.ToInt64((neutrinoContractBalance.Regular-totalWithdraw)/100*(100-settings.Leasing.LeasingSharePercent));
                        var availableBalance = neutrinoContractBalance.Available - totalWithdraw;
                        var neededAmount = minWaves - availableBalance;
                        var activeLeaseTxs = await wavesHelper.GetActiveLease(settings.Leasing.NodeAddress);
                        var totalLeasingCancelAmount = 0L;
                        if (neededAmount > leasingAmountForOneTxInWavelet)
                        {
                            foreach (var leasingTx in activeLeaseTxs.OrderBy(x => x.Amount).Where(x=>x.Sender == contractAddress))
                            {
                                if (totalLeasingCancelAmount >= neededAmount)
                                    break;

                                totalLeasingCancelAmount += leasingTx.Amount;
                                var cancelLease = new CancelLeasingTransaction(settings.ChainId, contractPubKey,
                                    leasingTx.Id, 0.005m);
                                cancelLease.Sign(account);
                                // shit code. Bug in wavesCs
                                var json = JObject.Parse(cancelLease.GetJsonWithSignature().ToJson());
                                json.Add("proofs", new JArray
                                {
                                    cancelLease.Proofs
                                        .Take(Array.FindLastIndex(cancelLease.Proofs, p => p != null && p.Length > 0) +
                                              1)
                                        .Select(p => p == null ? "" : p.ToBase58())
                                        .ToArray()
                                });
                                json.Add("version", 2);
                                json.Add("chainId", settings.ChainId);
                                var id = await wavesHelper.WaitTxAndGetId(await wavesHelper.Broadcast(json.ToString()));
                                Logger.Info($"Cancel lease tx:{id} (LeaseId:{cancelLease.LeaseId})");
                            }
                        }

                        var expectedLeasingBalance = Convert.ToInt64((neutrinoContractBalance.Regular-totalWithdraw)/100*settings.Leasing.LeasingSharePercent);
                        var leasingBalance = neutrinoContractBalance.Regular - neutrinoContractBalance.Available;
                        var neededLeaseTx = expectedLeasingBalance - leasingBalance;

                       
                        while(neededLeaseTx >= leasingAmountForOneTxInWavelet)
                        {
                            neededLeaseTx -= leasingAmountForOneTxInWavelet;
                            var leaseTx = new LeaseTransaction(settings.ChainId, contractPubKey, settings.Leasing.NodeAddress, settings.Leasing.LeasingAmountForOneTx, 0.005m);
                            leaseTx.Sign(account);
                            // shit code. Bug in wavesCs
                            var json = JObject.Parse(leaseTx.GetJsonWithSignature().ToJson());
                            json.Add("proofs", new JArray { leaseTx.Proofs.Take(Array.FindLastIndex(leaseTx.Proofs, p => p != null && p.Length > 0) + 1)
                                .Select(p => p == null ? "" : p.ToBase58())
                                .ToArray()
                            });
                            json.Add("version", 2);
                            var id = await wavesHelper.WaitTxAndGetId(await wavesHelper.Broadcast(json.ToString()));
                            Logger.Info($"Lease tx:{id}");
                        }
                    }
                    
                    var supply = totalNeutrinoSupply - neutrinoBalance + neutrinoContractData.BalanceLockNeutrino;
                    var reserve = wavesBalance - neutrinoContractData.BalanceLockWaves;

                    Logger.Info($"Supply:{supply}");
                    Logger.Info($"Reserve:{reserve}");
                    
                    var bondAuctionBalance = await wavesHelper.GetBalance(neutrinoContractData.AuctionContractAddress,
                        neutrinoContractData.BondAssetId);
                    var deficit = CurrencyConvert.NeutrinoToBond(supply - CurrencyConvert.WavesToNeutrino(reserve, controlContractData.Price));
                    var liquidationContractBalance = CurrencyConvert.NeutrinoToBond(await wavesHelper.GetBalance(neutrinoContractData.LiquidationContractAddress, neutrinoContractData.NeutrinoAssetId));
                    
                    Logger.Info($"Deficit:{deficit}");
                    Logger.Info($"BondBalance:{bondAuctionBalance}");

                    if ((deficit > 0 && deficit - bondAuctionBalance >= CurrencyConvert.NeutrinoToBond(supply)*DeficitOffset/100) || (deficit*-1) - liquidationContractBalance > 0)
                    {
                        Logger.Info("Transfer to auction");
                        var generateBondTx = node.InvokeScript(account, contractAddress,
                            "transferToAuction", null);
                        var txId = await wavesHelper.WaitTxAndGetId(generateBondTx);
                        Logger.Info($"Transfer to auction tx id:{txId}");
                    }
                    var surplusWithoutLiquidation =  CurrencyConvert.NeutrinoToBond(CurrencyConvert.WavesToNeutrino(reserve, controlContractData.Price) - supply - CurrencyConvert.BondToNeutrino(liquidationContractBalance));
                    if (surplusWithoutLiquidation > 0 && liquidationContractBalance > 0 &&  !string.IsNullOrEmpty(liquidationControlData.Orderbook))
                    {
                        Logger.Info("Execute order for liquidation");

                        var orders = liquidationControlData.Orderbook.Split("_");
                        long totalExecute = 0;
                        var surplus = Math.Abs(deficit);
                        foreach (var order in orders.Where(x => !string.IsNullOrEmpty(x)))
                        {
                            var total = liquidationControlData.TotalByOrder[order];
                            var totalFilled = liquidationControlData.FilledTotalByOrder?.GetValueOrDefault(order) ?? 0;
                            var amount = total - totalFilled;
                            if (totalExecute >= surplus)
                                break;

                            totalExecute += amount;

                            var exTx = node.InvokeScript(account, neutrinoContractData.LiquidationContractAddress, "liquidateBond",
                                null);
                            var txId = await wavesHelper.WaitTxAndGetId(exTx);
                            Logger.Info($"Execute order tx id:{txId}");
                        }
                    } 
                    else if (surplusWithoutLiquidation <= 0 && liquidationContractBalance > 0)
                    {
                        var exTx = node.InvokeScript(account, neutrinoContractData.LiquidationContractAddress,
                            "liquidateBond",
                            null);
                        var txId = await wavesHelper.WaitTxAndGetId(exTx);
                        Logger.Info($"Execute order tx id:{txId}");
                    }
                    else  if (deficit > 0 && bondAuctionBalance > 0 && !string.IsNullOrEmpty(auctionControlData.Orderbook))
                    {
                        Logger.Info("Execute order for auction");

                        var orders = auctionControlData.Orderbook.Split("_");
                        long totalExecute = 0;
                        foreach (var order in orders.Where(x => !string.IsNullOrEmpty(x)))
                        {
                            var total = auctionControlData.TotalByOrder[order];
                            var totalFilled = auctionControlData.FilledTotalByOrder?.GetValueOrDefault(order) ?? 0;
                            var amount = CurrencyConvert.NeutrinoToBond(total - totalFilled) * 100 / auctionControlData.PriceByOrder[order];
                            
                            if (totalExecute >= deficit)
                                break;

                            totalExecute += amount;

                            var exTx = node.InvokeScript(account, neutrinoContractData.AuctionContractAddress,
                                "sellBond", null);
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