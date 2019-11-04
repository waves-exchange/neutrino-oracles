using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NeutrinoOracles.PriceOracle.PriceProvider.Interfaces;
using Newtonsoft.Json.Linq;

namespace NeutrinoOracles.PriceOracle.PriceProvider
{
    public class TidexProvider : IPriceProvider
    {
        public int Weight { get; } = 3;

        public async Task<decimal> GetPrice()
        {
            var url = new UriBuilder("https://api.tidex.com/api/3/ticker/waves_usdt");
            var client = new WebClient();
            client.Headers.Add("Accepts", "application/json");
            var json = JObject.Parse(await client.DownloadStringTaskAsync(url.ToString()));
            return (decimal) json["waves_usdt"]["last"];
        }
    }
}