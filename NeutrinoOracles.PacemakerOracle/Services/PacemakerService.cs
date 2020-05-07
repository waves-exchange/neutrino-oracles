using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeutrinoOracles.Common.Converters;
using NeutrinoOracles.Common.Helpers;
using NeutrinoOracles.Common.Keys;
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
            _neutrinoAccountState =
                AccountDataConverter.ToNeutrinoAccountData(
                    await _wavesHelper.GetDataByAddress(_neutrinoSettings.NeutrinoAddress));

            _auctionControlState =
                AccountDataConverter.ToAuctionAccountData(
                    await _wavesHelper.GetDataByAddress(_neutrinoSettings.AuctionAddress));
            _liquidationAccountState =
                AccountDataConverter.ToLiquidationAccountData(
                    await _wavesHelper.GetDataByAddress(_neutrinoSettings.LiquidationAddress));

            _initInfo.TotalNeutrinoSupply = await _wavesHelper.GetTotalSupply(_neutrinoAccountState.NeutrinoAssetId);

            _initInfo.NeutrinoBalance = await _wavesHelper.GetBalance(_neutrinoSettings.NeutrinoAddress,
                _neutrinoAccountState.NeutrinoAssetId);
            _initInfo.WavesBalance = await _wavesHelper.GetBalance(_neutrinoSettings.NeutrinoAddress);

            _initInfo.Height = await _wavesHelper.GetHeight();

            var price = await _wavesHelper.GetDataByAddressAndKey(_neutrinoSettings.ControlAddress, "price");
            Logger.Info("New height: " + _initInfo.Height);
            Logger.Info($"Price:{price.Value}");

            _initInfo.LiquidationNeutrinoBalance = await _wavesHelper.GetBalance(_neutrinoSettings.LiquidationAddress,
                _neutrinoAccountState.NeutrinoAssetId);
            _initInfo.AuctionBondBalance =
                await _wavesHelper.GetBalance(_neutrinoSettings.AuctionAddress, _neutrinoSettings.BondAssetId);

            _initInfo.Supply = _neutrinoAccountState.BalanceLockNeutrino + _initInfo.TotalNeutrinoSupply -
                               _initInfo.NeutrinoBalance - _initInfo.LiquidationNeutrinoBalance;
            _initInfo.Reserve = _initInfo.WavesBalance - _neutrinoAccountState.BalanceLockWaves;

            _initInfo.Deficit =
                _initInfo.Supply - CurrencyConvert.WavesToNeutrino(_initInfo.Reserve, (long) price.Value);

            Logger.Debug($"Init info: {JsonConvert.SerializeObject(_initInfo)}");
        }

        public async Task WithdrawAllUser()
        {
            var currentPriceIndex = (long)(await _wavesHelper.GetDataByAddressAndKey(_neutrinoSettings.ControlAddress, "price_index")).Value;

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

                var priceHeight = (long) (await _wavesHelper.GetDataByAddressAndKey(_neutrinoSettings.ControlAddress,
                    "price_index_" + currentPriceIndex)).Value;
                
                if (withdrawBlock >= priceHeight)
                    continue;
                
                long index = 0;
                for (var i = currentPriceIndex; i > 0; i--)
                {
                    priceHeight = (long) (await _wavesHelper.GetDataByAddressAndKey(_neutrinoSettings.ControlAddress,
                        "price_index_" + i)).Value;
                    if (withdrawBlock == priceHeight)
                    {
                        index = currentPriceIndex;
                        break;
                    } 
                    
                    if (withdrawBlock > priceHeight)
                    {
                        index = currentPriceIndex-1;   
                        break;
                    }
                }
                
                var foundHeight = (long) (await _wavesHelper.GetDataByAddressAndKey(_neutrinoSettings.ControlAddress, "price_index_" + index)).Value;
                var priceByHeight = (long) (await _wavesHelper.GetDataByAddressAndKey(_neutrinoSettings.ControlAddress, "price_" + foundHeight)).Value; 
                var withdrawNeutrinoAmount = _neutrinoAccountState.BalanceLockNeutrinoByUser?.GetValueOrDefault(address) ?? 0;

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
            var minWaves = Convert.ToInt64((neutrinoContractBalance.Regular) / 100 *
                                           (100 - _leasingSettings.LeasingSharePercent));
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
            else if (neutrinoContractBalance.Available >
                     minWaves + (_leasingSettings.LeasingAmountForOneTx * CurrencyConvert.Wavelet))
            {
                var expectedLeasingBalance = Convert.ToInt64((neutrinoContractBalance.Regular) / 100 *
                                                             _leasingSettings.LeasingSharePercent);
                var leasingBalance = neutrinoContractBalance.Regular - neutrinoContractBalance.Available;
                var neededLeaseTx = expectedLeasingBalance - leasingBalance;


                while (neededLeaseTx >= _leasingSettings.LeasingAmountForOneTx)
                {
                    neededLeaseTx -= _leasingSettings.LeasingAmountForOneTx;
                    var leaseTxId = await _neutrinoApi.Lease(_leasingSettings.NodeAddress,
                        _leasingSettings.LeasingAmountForOneTx);
                    Logger.Info($"Lease tx:{leaseTxId}");
                }
            }
        }

        public async Task TransferToAuction()
        {
            var auctionNBAmount = _initInfo.Supply - _initInfo.AuctionBondBalance;

            var deficitInBonds = CurrencyConvert.NeutrinoToBond(_initInfo.Deficit);

            var surplus = deficitInBonds * -1;
            var liquidationContractBalanceInBonds =
                CurrencyConvert.NeutrinoToBond(_initInfo.LiquidationNeutrinoBalance);
            var requireSurplusBonds = surplus - liquidationContractBalanceInBonds;

            if (auctionNBAmount >= 1 || requireSurplusBonds > 1)
            {
                Logger.Info("Transfer to auction");
                var transferTxId = await _neutrinoApi.TransferToAuction();
                Logger.Info($"Transfer to auction tx id:{transferTxId}");
            }
        }

        public async Task ExecuteOrderLiquidation()
        {
            var surplusInBonds = -1 * CurrencyConvert.NeutrinoToBond(_initInfo.Deficit);
            var liquidationContractBalanceInBonds =
                CurrencyConvert.NeutrinoToBond(_initInfo.LiquidationNeutrinoBalance);

            if (surplusInBonds > 0 && !string.IsNullOrEmpty(_liquidationAccountState.OrderFirst) &&
                liquidationContractBalanceInBonds > 1)
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
            else if (surplusInBonds <= 0 && !string.IsNullOrEmpty(_liquidationAccountState.OrderFirst) &&
                     liquidationContractBalanceInBonds > 1)
            {
                var exTxId = await _neutrinoApi.ExecuteOrderLiquidation();
                Logger.Info($"Return liquidation balance tx id:{exTxId}");
            }
        }

        public async Task ExecuteOrderAuction()
        {
            var roiEquals = _initInfo.Deficit * 100 / _initInfo.Supply;
            if (_initInfo.AuctionBondBalance > 0 && !string.IsNullOrEmpty(_auctionControlState.OrderFirst))
            {
                Logger.Info("Execute order for auction");

                long totalExecute = 0;

                var nextOrder = _auctionControlState.OrderFirst;
                while (!string.IsNullOrEmpty(nextOrder))
                {
                    var roi = _auctionControlState.RoiByOrder[nextOrder];
                    if (roiEquals < roi)
                    {
                        return;
                    }

                    var total = _auctionControlState.TotalByOrder[nextOrder];
                    var totalFilled = _auctionControlState.FilledTotalByOrder?.GetValueOrDefault(nextOrder) ?? 0;
                    var amount = CurrencyConvert.NeutrinoToBond(total - totalFilled) * 100 /
                                 _auctionControlState.PriceByOrder[nextOrder];

                    if (totalExecute >= _initInfo.AuctionBondBalance)
                        break;

                    totalExecute += amount;

                    var exTxId = await _neutrinoApi.ExecuteOrderAuction();
                    Logger.Info($"Execute auction order tx id:{exTxId}");
                    nextOrder = _auctionControlState.NextOrderByOrder?.GetValueOrDefault(nextOrder);
                }
            }
            else if (_initInfo.AuctionBondBalance > _initInfo.Supply)
            {
                var exTxId = await _neutrinoApi.ExecuteOrderAuction();
                Logger.Info($"Execute auction order tx id:{exTxId}");
            }

        }
    }
}
