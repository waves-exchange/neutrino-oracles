using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NeutrinoOracles.PriceOracle.PriceProvider.Interfaces;
using Newtonsoft.Json.Linq;

namespace NeutrinoOracles.PriceOracle.PriceProvider
{
    public class BinanceProvider
    {
        public async Task<decimal> GetPrice(string pair)
        {
            var url = new UriBuilder("https://api.binance.com/api/v3/ticker/price?symbol="+pair);
            var client = new WebClient();
            client.Headers.Add("Accepts", "application/json");
            var json = JObject.Parse(await client.DownloadStringTaskAsync(url.ToString()));
            return (decimal) json["price"];
        }

    }
}