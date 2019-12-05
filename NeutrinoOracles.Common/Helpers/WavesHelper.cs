using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NeutrinoOracles.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WavesCS;

namespace NeutrinoOracles.Common.Helpers
{
    // WavesCS not support Net Core
    public class WavesHelper
    {
        private const int MaxWaitSec = 60;
        private readonly string _nodeUrl;
        private readonly char _chainId;
        private readonly HttpClient _httpClient = new HttpClient();
        
        public WavesHelper(string nodeUrl, char chainId)
        {
            _nodeUrl = nodeUrl;
            _chainId = chainId;
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

        public async Task<List<ActiveLeaseTx>> GetActiveLease(string address)
        {
            var response = await SendRequest($"{_nodeUrl}/leasing/active/{address}");
            var result = JsonConvert.DeserializeObject<List<ActiveLeaseTx>>(response);
            return result;
        }
        public async Task<DetailsBalance> GetDetailsBalance(string address)
        {
            var response = await SendRequest($"{_nodeUrl}/addresses/balance/details/{address}");
            var result = JsonConvert.DeserializeObject<DetailsBalance>(response);
            return result;
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

        public async Task<string> Broadcast(string data)
        {
            var httpContent = new StringContent(data, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_nodeUrl}/transactions/broadcast", httpContent);
            var result = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            return result;
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