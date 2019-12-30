using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NeutrinoOracles.Common.Converters;
using NeutrinoOracles.Common.Helpers;
using NeutrinoOracles.Common.Keys;
using NeutrinoOracles.Common.Models;
using NeutrinoOracles.PriceOracle.Models;
using NeutrinoOracles.PriceOracle.PriceProvider;
using NeutrinoOracles.PriceOracle.PriceProvider.Interfaces;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Fluent;
using WavesCS;

namespace NeutrinoOracles.PriceOracle
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly BinanceProvider BinanceProvider = new BinanceProvider();
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
            
            var seed = args[0];
            var settings = configuration.Get<Settings>();
            var account = PrivateKeyAccount.CreateFromSeed(seed, settings.ChainId);
            var nodeApi = new Node(settings.NodeUrl, settings.ChainId);
            var wavesHelper = new WavesHelper(settings.NodeUrl, settings.ChainId);
            
            Logger.Info("Start price oracle");
            
            var oracles = ((string)(await wavesHelper.GetDataByAddressAndKey(settings.ContractAddress, ControlKeys.Oracles)).Value).Split(",");
            var coefficientValue = (int)(await wavesHelper.GetDataByAddressAndKey(settings.ContractAddress, ControlKeys.Coefficient)).Value;
            
            while (true)
            {
                try
                {
                    var newPriceRequest = GetPrice();
                    var height = await wavesHelper.GetHeight();
                    Logger.Info($"Height:{height}");
                    
                    var priceKey = OracleKeys.GetPriceByHeight(height);
                    var oraclePriceRequest = wavesHelper.GetDataByAddress(account.Address, priceKey);
                    var oraclePriceRequests = oracles.Select(oracle => wavesHelper.GetDataByAddress(oracle, priceKey)).ToList();

                    var oraclePrices = oraclePriceRequest.Result;
                    var price = newPriceRequest.Result;
                    Logger.Info($"New price:{price}");
                    
                    if (!oraclePrices.Any())
                    {
                        var tx = nodeApi.PutData(account, new Dictionary<string, object>() { {"price_" + height, price }});
                        Logger.Info($"Tx set current price ({price}): {(string) JObject.Parse(tx)["id"]}");
                        Logger.Debug(tx);
                    }
                    else
                    {
                        Logger.Info($"My price:{(int)oraclePrices[0].Value}");
                    }
                    
                    var oraclePriceCount = oraclePriceRequests.Select(request => request.Result).Count(oraclePrice => oraclePrice.Any());
                    Logger.Debug($"Oracle price count:{oraclePriceCount}");
                    
                    if (oraclePriceCount >= coefficientValue && (!controlContractData.PriceByHeight?.ContainsKey(Convert.ToString(height)) ?? true))
                    {
                        var transaction = new InvokeScriptTransaction(settings.ChainId, account.PublicKey , settings.ContractAddress, "finalizeCurrentPrice", null, null, 0.005M, null);
                        transaction.Sign(account, 0);
                        var jsonTx = transaction.GetJsonWithSignature().ToJson();
                        await wavesHelper.Broadcast(jsonTx);
                        Logger.Info($"Tx finalize current price: {jsonTx}");
                        Logger.Debug(jsonTx);
                    }*/
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
                
                Logger.Info($"Sleep");
                await Task.Delay(TimeSpan.FromSeconds(settings.TimeoutSec));
            }
        }

        private static async Task<long> GetPrice()
        {
            var priceOne = await BinanceProvider.GetPrice("WAVESUSDT");
            var priceTwo = await BinanceProvider.GetPrice("WAVESBTC");
            var priceBtcUsdt = await BinanceProvider.GetPrice("BTCUSDT");
            var price = (priceOne + priceTwo*priceBtcUsdt)/2;
            return Convert.ToInt64(Math.Round(price, 2, MidpointRounding.AwayFromZero)*100);
        }
    }
}