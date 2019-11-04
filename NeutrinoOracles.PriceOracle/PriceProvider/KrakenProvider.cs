using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NeutrinoOracles.PriceOracle.PriceProvider.Interfaces;
using Newtonsoft.Json.Linq;

namespace NeutrinoOracles.PriceOracle.PriceProvider
{
    public class KrakenProvider : IPriceProvider
    {
        public int Weight { get; } = 1;
    
        public async Task<decimal> GetPrice()
        {
            var url = new UriBuilder("https://api.kraken.com/0/public/Ticker?pair=WAVESUSD");
            var client = new WebClient();
            client.Headers.Add("Accepts", "application/json");
            var json = JObject.Parse(await client.DownloadStringTaskAsync(url.ToString()));
            return (decimal) json["result"]["WAVESUSD"]["c"].First();
        }
    }
}