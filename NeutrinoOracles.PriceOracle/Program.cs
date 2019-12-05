using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NeutrinoOracles.Common.Converters;
using NeutrinoOracles.Common.Helpers;
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
        private static BinanceProvider _binanceProvider = new BinanceProvider();
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

            var nodeApi = new Node(settings.NodeUrl, settings.ChainId);
            var account = PrivateKeyAccount.CreateFromSeed(seed, settings.ChainId);
            var wavesHelper = new WavesHelper(settings.NodeUrl, settings.ChainId);
            
            Logger.Info("Start price oracle");
            while (true)
            {
                try
                {
                    var height = await wavesHelper.GetHeight();
                    Logger.Info($"Height:{height}");
                    var controlContractData = AccountDataConverter.ToControlAccountData(await wavesHelper.GetDataByAddress(settings.ContractAddress));

                    var newPrice = await GetPrice();
                    
                    var oracleData = AccountDataConverter.ToOracleAccountData(await wavesHelper.GetDataByAddress(account.Address));
                    if (!oracleData.PriceByHeight?.ContainsKey(Convert.ToString(height)) ?? true)
                    {
                        var tx = nodeApi.PutData(account, new Dictionary<string, object>() { {"price_" + height, newPrice }});
                        Logger.Info($"Tx set current price ({newPrice}): {(string) JObject.Parse(tx)["id"]}");
                        Logger.Debug(tx);
                    }

                    var oraclePriceCount = 0;
                    foreach (var oracle in controlContractData.Oracles.Split(","))
                    {
                        var anyOracleData = AccountDataConverter.ToOracleAccountData(await wavesHelper.GetDataByAddress(oracle));
                        if (anyOracleData.PriceByHeight?.ContainsKey(Convert.ToString(height)) ?? false)
                            oraclePriceCount++;
                    }

                    Logger.Debug("Oracle price count:" + oraclePriceCount);
                    if (oraclePriceCount >= controlContractData.BftCoefficientOracle && (!controlContractData.PriceByHeight?.ContainsKey(Convert.ToString(height)) ?? true))
                    {
                        var tx = nodeApi.InvokeScript(account, settings.ContractAddress, "finalizeCurrentPrice", null);
                        Logger.Info($"Tx finalize current price: {(string) JObject.Parse(tx)["id"]}");
                        Logger.Debug(tx);
                    }
                    
                    Logger.Info($"Сurrent price:{controlContractData.Price}");
                    Logger.Info($"New price:{newPrice}");
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
            var priceOne = await _binanceProvider.GetPrice("WAVESUSDT");
            var priceTwo = await _binanceProvider.GetPrice("WAVESBTC");
            var priceBtcUsdt = await _binanceProvider.GetPrice("BTCUSDT");
            var price = (priceOne + priceTwo*priceBtcUsdt)/2;
            return Convert.ToInt64(Math.Round(price, 2, MidpointRounding.AwayFromZero)*100);
        }
    }
}