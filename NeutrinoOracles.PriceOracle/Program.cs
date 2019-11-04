using System;
using System.Collections.Generic;
using System.IO;
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
            var wavesHelper = new WavesHelper(settings.NodeUrl);

            var priceProviders = new List<IPriceProvider>()
            {
                new BinanceProvider(),
                new KrakenProvider(),
                new TidexProvider()
            };
            
            Logger.Info("Start price oracle");
            while (true)
            {
                try
                {
                    var controlContractData = AccountDataConverter.ToControlAccountData(await wavesHelper.GetDataByAddress(settings.ContractAddress));

                    var newPrice = await GetPrice(priceProviders);
                    var height = await wavesHelper.GetHeight();

                    Logger.Info($"Height:{height}");
                    
                    if (height > controlContractData.ProvidingExpireBlock && controlContractData.IsPendingPrice)
                    {
                        var tx = nodeApi.InvokeScript(account, settings.ContractAddress, "finalizeCurrentPrice", null);
                        Logger.Info($"Tx finalize current price: {(string) JObject.Parse(tx)["id"]}");
                        Logger.Debug(tx);
                    }
                    
                    Logger.Info($"Сurrent price:{controlContractData.Price}");
                    Logger.Info($"New price:{newPrice}");
                    
                    if (controlContractData.Price == newPrice)
                        continue;
                    
                    if ((!controlContractData.IsProvidedByOracle?.GetValueOrDefault(account.Address) ?? true) ||
                        height > controlContractData.ProvidingExpireBlock && !controlContractData.IsPendingPrice)
                    {
                        var tx = nodeApi.InvokeScript(account, settings.ContractAddress, "setCurrentPrice",
                            new List<object> {newPrice});
                        Logger.Info($"Tx set current price ({newPrice}): {(string) JObject.Parse(tx)["id"]}");
                        Logger.Debug(tx);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }

                Logger.Info($"Sleep");
                await Task.Delay(TimeSpan.FromSeconds(settings.TimeoutSec));
            }
        }

        private static async Task<long> GetPrice(IEnumerable<IPriceProvider> priceProviders)
        {
            var totalPrice = 0;
            var totalWeight = 0;
            foreach (var priceProvider in priceProviders)
            {
                totalPrice += Convert.ToInt32(await priceProvider.GetPrice() * 100) * priceProvider.Weight;
                totalWeight += priceProvider.Weight;
            }

            return totalPrice/totalWeight;
        }
    }
}