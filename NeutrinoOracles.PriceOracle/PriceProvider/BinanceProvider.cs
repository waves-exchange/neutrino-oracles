using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NeutrinoOracles.PriceOracle.PriceProvider.Interfaces;
using Newtonsoft.Json.Linq;

namespace NeutrinoOracles.PriceOracle.PriceProvider
{
    public class BinanceProvider : IPriceProvider
    {
        public int Weight { get; } = 3;

        public async Task<decimal> GetPrice()
        {
            var url = new UriBuilder("https://api.binance.com/api/v3/ticker/price?symbol=WAVESUSDT");
            var client = new WebClient();
            client.Headers.Add("Accepts", "application/json");
            var json = JObject.Parse(await client.DownloadStringTaskAsync(url.ToString()));
            return (decimal) json["price"];
        }
    }
}