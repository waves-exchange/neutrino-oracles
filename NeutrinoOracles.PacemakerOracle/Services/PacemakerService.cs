using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeutrinoOracles.Common.Converters;
using NeutrinoOracles.Common.Helpers;
using NeutrinoOracles.Common.Models;
using NeutrinoOracles.PacemakerOracle.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WavesCS;

namespace NeutrinoOracles.PacemakerOracle.Services
{
    public class PacemakerService
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly long _deficitOffset;
        private readonly WavesHelper _wavesHelper;
        private readonly LeasingSettings _leasingSettings;
        private readonly NeutrinoSettings _neutrinoSettings;
        private readonly NeutrinoApi _neutrinoApi;

        private InitPacemakerInfo _initInfo;
        private NeutrinoAccountState _neutrinoAccountState;
        private ControlAccountState _controlAccountState;
        private AuctionAccountState _auctionControlState;
        private LiquidationAccountData _liquidationAccountState;
        
        public PacemakerService(WavesHelper wavesHelper, Node node, PrivateKeyAccount account,
            NeutrinoSettings neutrinoSettings, LeasingSettings leasingSettings, long deficitOffset = 5)
        {
            _neutrinoApi = new NeutrinoApi(neutrinoSettings, wavesHelper, node, account);

            _wavesHelper = wavesHelper;
            _neutrinoSettings = neutrinoSettings;
            _leasingSettings = leasingSettings;
            _deficitOffset = deficitOffset;

        }
        
        public async Task InitOrUpdate()
        {
            _initInfo = new InitPacemakerInfo();
            _neutrinoAccountState = AccountDataConverter.ToNeutrinoAccountData(await _wavesHelper.GetDataByAddress(_neutrinoSettings.NeutrinoAddress));
            _controlAccountState = AccountDataConverter.ToControlAccountData(await _wavesHelper.GetDataByAddress(_neutrinoSettings.ControlAddress));
            _auctionControlState = AccountDataConverter.ToAuctionAccountData(await _wavesHelper.GetDataByAddress(_neutrinoSettings.AuctionAddress));
            _liquidationAccountState = AccountDataConverter.ToLiquidationAccountData(await _wavesHelper.GetDataByAddress(_neutrinoSettings.LiquidationAddress));

            _initInfo.TotalNeutrinoSupply = await _wavesHelper.GetTotalSupply(_neutrinoAccountState.NeutrinoAssetId);
            
            _initInfo.NeutrinoBalance = await _wavesHelper.GetBalance(_neutrinoSettings.NeutrinoAddress, _neutrinoAccountState.NeutrinoAssetId);
            _initInfo.WavesBalance = await _wavesHelper.GetBalance(_neutrinoSettings.NeutrinoAddress);

            _initInfo.Height = await _wavesHelper.GetHeight();
            Logger.Info("New height: " + _initInfo.Height);
            Logger.Info($"Price:{_controlAccountState.Price}");

            _initInfo.LiquidationNeutrinoBalance = await _wavesHelper.GetBalance(_neutrinoSettings.LiquidationAddress, _neutrinoAccountState.NeutrinoAssetId);
            _initInfo.AuctionBondBalance = await _wavesHelper.GetBalance(_neutrinoSettings.AuctionAddress, _neutrinoAccountState.BondAssetId);

            _initInfo.Supply = _neutrinoAccountState.BalanceLockNeutrino + _initInfo.TotalNeutrinoSupply - _initInfo.NeutrinoBalance - _initInfo.LiquidationNeutrinoBalance;
            _initInfo.Reserve = _initInfo.WavesBalance - _neutrinoAccountState.BalanceLockWaves;

            _initInfo.Deficit = _initInfo.Supply - CurrencyConvert.WavesToNeutrino(_initInfo.Reserve, _controlAccountState.Price);
            
            Logger.Debug($"Init info: {JsonConvert.SerializeObject(_initInfo)}");
        }

        public async Task WithdrawAllUser()
        {
            var addresses = new List<string>();

            if (_neutrinoAccountState.BalanceLockNeutrinoByUser != null)
                addresses.AddRange(_neutrinoAccountState.BalanceLockNeutrinoByUser.Where(x => x.Value > 0)
                    .Select(x => x.Key));

            if (_neutrinoAccountState.BalanceLockWavesByUser != null)
                addresses.AddRange(_neutrinoAccountState.BalanceLockWavesByUser?.Where(x => x.Value > 0)
                    .Select(x => x.Key));

            foreach (var address in addresses)
            {
                var withdrawBlock = _neutrinoAccountState.BalanceUnlockBlockByAddress.GetValueOrDefault(address);

                if (_initInfo.Height < withdrawBlock)
                    continue;

                var indexes = _controlAccountState.PriceHeightByIndex.Where(x => x.Value >= withdrawBlock).ToList();

                if (!indexes.Any())
                    continue;

                var indexString = indexes.Min(x => x.Key);
                var index = Convert.ToInt64(indexString);
                var heightByIndex = _controlAccountState.PriceHeightByIndex[indexString];
                var priceByHeight = _controlAccountState.PriceByHeight[Convert.ToString(heightByIndex)];
                var withdrawNeutrinoAmount = _neutrinoAccountState.BalanceLockNeutrinoByUser.GetValueOrDefault(address);

                if (withdrawNeutrinoAmount > 0)
                {
                    var neutrinoContractBalance =
                        await _wavesHelper.GetDetailsBalance(_neutrinoSettings.NeutrinoAddress);
                    var wavesAmount = CurrencyConvert.NeutrinoToWaves(withdrawNeutrinoAmount, priceByHeight);
                    if (wavesAmount > neutrinoContractBalance.Available)
                    {
                        if (!_leasingSettings.IsLeasingProvider)
                            continue;

                        var totalLeasingCancelAmount = 0L;
                        var activeLeaseTxs = await _wavesHelper.GetActiveLease(_leasingSettings.NodeAddress);

                        var neededAmount = wavesAmount - neutrinoContractBalance.Available;
                        foreach (var leasingTx in activeLeaseTxs.OrderByDescending(x => x.Timestamp)
                            .Where(x => x.Sender == _neutrinoSettings.NeutrinoAddress))
                        {
                            if (totalLeasingCancelAmount >= neededAmount)
                                break;

                            totalLeasingCancelAmount += leasingTx.Amount;
                            var cancelLeaseTxId = await _neutrinoApi.CancelLease(leasingTx.Id);
                            Logger.Info($"Cancel lease tx:{cancelLeaseTxId} (LeaseId:{cancelLeaseTxId})");
                        }
                    }
                }

                var withdrawTxId = await _neutrinoApi.Withdraw(address, index);
                Logger.Info($"Withdraw tx id:{withdrawTxId} (Address:{address})");
            }
        }
        
        public async Task RebalanceLeasing()
        {
            var neutrinoContractBalance = await _wavesHelper.GetDetailsBalance(_neutrinoSettings.NeutrinoAddress);
            var minWaves = Convert.ToInt64((neutrinoContractBalance.Regular) / 100 * (100 - _leasingSettings.LeasingSharePercent));
            var activeLeaseTxs = await _wavesHelper.GetActiveLease(_leasingSettings.NodeAddress);
            var totalLeasingCancelAmount = 0L;
            
            if (minWaves > neutrinoContractBalance.Available)
            {
                var neededAmount = minWaves - neutrinoContractBalance.Available;
                foreach (var leasingTx in activeLeaseTxs.OrderByDescending(x => x.Timestamp)
                    .Where(x => x.Sender == _neutrinoSettings.NeutrinoAddress))
                {
                    if (totalLeasingCancelAmount >= neededAmount)
                        break;

                    totalLeasingCancelAmount += leasingTx.Amount;
                    
                    var cancelLeaseTxId = await _neutrinoApi.CancelLease(leasingTx.Id);
                    Logger.Info($"Cancel lease tx:{leasingTx.Id} (LeaseId:{cancelLeaseTxId})");
                }
            }
            else if (neutrinoContractBalance.Available > minWaves + (_leasingSettings.LeasingAmountForOneTx * CurrencyConvert.Wavelet))
            {
                var expectedLeasingBalance = Convert.ToInt64((neutrinoContractBalance.Regular) / 100 *
                                                             _leasingSettings.LeasingSharePercent);
                var leasingBalance = neutrinoContractBalance.Regular - neutrinoContractBalance.Available;
                var neededLeaseTx = expectedLeasingBalance - leasingBalance;


                while (neededLeaseTx >= _leasingSettings.LeasingAmountForOneTx)
                {
                    neededLeaseTx -= _leasingSettings.LeasingAmountForOneTx;
                    var leaseTxId = await _neutrinoApi.Lease(_leasingSettings.NodeAddress, _leasingSettings.LeasingAmountForOneTx);
                    Logger.Info($"Lease tx:{leaseTxId}");
                }
            }
        }

        public async Task TransferToAuction()
        {
            var deficitInBonds = CurrencyConvert.NeutrinoToBond(_initInfo.Deficit);
          
            var requireDeficitBonds = deficitInBonds - _initInfo.AuctionBondBalance;
            var minDeficitBonds = CurrencyConvert.NeutrinoToBond(_initInfo.Supply) * _deficitOffset / 100;

            var surplus = deficitInBonds * -1;
            var liquidationContractBalanceInBonds = CurrencyConvert.NeutrinoToBond(_initInfo.LiquidationNeutrinoBalance);
            var requireSurplusBonds = surplus - liquidationContractBalanceInBonds;
            
            if (deficitInBonds > 0 && requireDeficitBonds >= minDeficitBonds || requireSurplusBonds > 1)
            {
                Logger.Info("Transfer to auction");
                var transferTxId = await _neutrinoApi.TransferToAuction();
                Logger.Info($"Transfer to auction tx id:{transferTxId}");
            }
        }

        public async Task ExecuteOrderLiquidation()
        {
            var surplusInBonds = -1 * CurrencyConvert.NeutrinoToBond(_initInfo.Deficit);
            var liquidationContractBalanceInBonds = CurrencyConvert.NeutrinoToBond(_initInfo.LiquidationNeutrinoBalance);
            
            if (surplusInBonds > 0 && !string.IsNullOrEmpty(_liquidationAccountState.OrderFirst) && liquidationContractBalanceInBonds > 1)
            {
                Logger.Info("Execute order for liquidation");

                long totalExecute = 0;
                var nextOrder = _liquidationAccountState.OrderFirst;
                while (!string.IsNullOrEmpty(nextOrder))
                {
                    var total = _liquidationAccountState.TotalByOrder[nextOrder];
                    var totalFilled = _liquidationAccountState.FilledTotalByOrder?.GetValueOrDefault(nextOrder) ?? 0;
                    var amount = total - totalFilled;
                    if (totalExecute >= surplusInBonds)
                        break;

                    totalExecute += amount;

                    var exTxId = await _neutrinoApi.ExecuteOrderLiquidation();
                    Logger.Info($"Execute liquidation order tx id:{exTxId}");

                    nextOrder = _liquidationAccountState.NextOrderByOrder?.GetValueOrDefault(nextOrder);
                }
            }
            else if(surplusInBonds <= 0 && !string.IsNullOrEmpty(_liquidationAccountState.OrderFirst) && liquidationContractBalanceInBonds > 1)
            {
                var exTxId = await _neutrinoApi.ExecuteOrderLiquidation();
                Logger.Info($"Return liquidation balance tx id:{exTxId}");
            }
        }

        public async Task ExecuteOrderAuction()
        {
            var deficitInBonds = CurrencyConvert.NeutrinoToBond(_initInfo.Deficit);
            
            if (deficitInBonds > 0 && _initInfo.AuctionBondBalance > 0 && !string.IsNullOrEmpty(_auctionControlState.Orderbook))
            {
                Logger.Info("Execute order for auction");

                var orders = _auctionControlState.Orderbook.Split("_");
                long totalExecute = 0;
                foreach (var order in orders.Where(x => !string.IsNullOrEmpty(x)))
                {
                    var total = _auctionControlState.TotalByOrder[order];
                    var totalFilled = _auctionControlState.FilledTotalByOrder?.GetValueOrDefault(order) ?? 0;
                    var amount = CurrencyConvert.NeutrinoToBond(total - totalFilled) * 100 /
                                 _auctionControlState.PriceByOrder[order];

                    if (totalExecute >= deficitInBonds)
                        break;

                    totalExecute += amount;

                    var exTxId = await _neutrinoApi.ExecuteOrderAuction();
                    Logger.Info($"Execute auction order tx id:{exTxId}");
                }
            }
            else if(deficitInBonds <= 0 && _initInfo.AuctionBondBalance > 0)
            {
                var exTxId = await _neutrinoApi.ExecuteOrderAuction();
                Logger.Info($"Execute auction order tx id:{exTxId}");
            }
            
        }
    }
}
