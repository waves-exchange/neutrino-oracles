using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NeutrinoOracles.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeutrinoOracles.Common.Helpers
{
    // WavesCS not support Net Core
    public class WavesHelper
    {
        private const int MaxWaitSec = 60;
        private readonly string _nodeUrl;
        private readonly HttpClient _httpClient = new HttpClient();
        
        public WavesHelper(string nodeUrl)
        {
            _nodeUrl = nodeUrl;
        }
        
        public string GetTxId(string json)
        {
            return (string)JObject.Parse(json)["id"];
        }
        public async Task<long> GetTotalSupply(string assetId)
        { 
            var response = JObject.Parse(await SendRequest($"{_nodeUrl}/assets/details/{assetId}?full=true"));
            return (long)response["quantity"];
        }
        public async Task<long> GetBalance(string address, string assetId = null)
        {
            var url = _nodeUrl + (assetId == null
                          ? $"/addresses/balance/{address}"
                          : $"/assets/balance/{address}/{assetId}");
            var response = JObject.Parse(await SendRequest(url));
            return (long)response["balance"];
        }
        public async Task<List<AccountDataResponse>> GetDataByAddress(string address)
        {
            var response = await SendRequest(_nodeUrl + "/addresses/data/" + address);
            var result = JsonConvert.DeserializeObject<List<AccountDataResponse>>(response);
            return result;
        }
        public async Task<int> GetHeight()
        {
            var response = JObject.Parse(await SendRequest(_nodeUrl + "/blocks/height"));
            return (int)response["height"];
        }
        public async Task<string> GetTxById(string txId)
        {
            return await SendRequest(_nodeUrl + "/transactions/info/" + txId);
        }
        public async Task<string> WaitTxAndGetId(string tx)
        {
            var txId = GetTxId(tx);
            for(var i = 0; i < MaxWaitSec; i++)
            {
                try
                {
                    await GetTxById(txId);
                    return txId;
                }
                catch (HttpRequestException e)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            throw new Exception("Tx not found");
        }
        private async Task<string> SendRequest(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}