using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeutrinoOracles.Common.Helpers;
using NeutrinoOracles.PacemakerOracle.Models;
using Newtonsoft.Json.Linq;
using NLog;
using WavesCS;

namespace NeutrinoOracles.PacemakerOracle.Services
{
    public class NeutrinoApi
    {
        private readonly WavesHelper _wavesHelper;
        private readonly Node _node;
        private readonly PrivateKeyAccount _account;
        private readonly NeutrinoSettings _neutrinoSettings;

        public NeutrinoApi(NeutrinoSettings neutrinoSettings, WavesHelper wavesHelper, Node node,
            PrivateKeyAccount account)
        {
            _wavesHelper = wavesHelper;
            _node = node;
            _account = account;
            _neutrinoSettings = neutrinoSettings;
        }

        public async Task<string> Withdraw(string address, long priceIndex)
        {
            var withdrawTx = _node.InvokeScript(_account, _neutrinoSettings.NeutrinoAddress, "withdraw",
                new List<object>() {address, priceIndex});
            return await _wavesHelper.WaitTxAndGetId(withdrawTx);
        }

        public async Task<string> CancelLease(string leaseTx)
        {
            var neutrinoContractPubKey = Base58.Decode(_neutrinoSettings.NeutrinoPubKeyBase58);
            var cancelLease = new CancelLeasingTransaction(_node.ChainId, neutrinoContractPubKey, leaseTx, 0.005m);
            cancelLease.Sign(_account);
            // shit code. Bug in wavesCs
            var json = JObject.Parse(cancelLease.GetJsonWithSignature().ToJson());
            json.Add("proofs", new JArray
            {
                cancelLease.Proofs
                    .Take(Array.FindLastIndex(cancelLease.Proofs, p => p != null && p.Length > 0) + 1)
                    .Select(p => p == null ? "" : p.ToBase58())
                    .ToArray()
            });
            json.Add("version", 2);
            json.Add("chainId", _node.ChainId);
            return await _wavesHelper.WaitTxAndGetId(await _wavesHelper.Broadcast(json.ToString()));
        }

        public async Task<string> Lease(string nodeAddress, long amount)
        {
            var neutrinoContractPubKey = Base58.Decode(_neutrinoSettings.NeutrinoPubKeyBase58);
            var leaseTx = new LeaseTransaction(_node.ChainId, neutrinoContractPubKey, nodeAddress, amount, 0.005m);
            leaseTx.Sign(_account);
            // shit code. Bug in wavesCs
            var json = JObject.Parse(leaseTx.GetJsonWithSignature().ToJson());
            json.Add("proofs", new JArray
            {
                leaseTx.Proofs.Take(Array.FindLastIndex(leaseTx.Proofs, p => p != null && p.Length > 0) + 1)
                    .Select(p => p == null ? "" : p.ToBase58())
                    .ToArray()
            });
            json.Add("version", 2);
            return await _wavesHelper.WaitTxAndGetId(await _wavesHelper.Broadcast(json.ToString()));
        }

        public async Task<string> TransferToAuction()
        {
            var withdrawTx = _node.InvokeScript(_account, _neutrinoSettings.NeutrinoAddress, "transferToAuction", null);
            return await _wavesHelper.WaitTxAndGetId(withdrawTx);
        }

        public async Task<string> ExecuteOrderAuction()
        {
            var exTx = _node.InvokeScript(_account, _neutrinoSettings.AuctionAddress, "sellBond", null);
            return await _wavesHelper.WaitTxAndGetId(exTx);
        }

        public async Task<string> ExecuteOrderLiquidation()
        {
            var exTx = _node.InvokeScript(_account, _neutrinoSettings.AuctionAddress,"liquidateBond",null);
            return await _wavesHelper.WaitTxAndGetId(exTx);
        }
    }
}