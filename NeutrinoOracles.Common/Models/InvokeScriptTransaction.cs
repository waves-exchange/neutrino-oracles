using System;
using System.Collections.Generic;
using System.Linq;
using NeutrinoOracles.Common.Helpers;
using WavesCS;

namespace NeutrinoOracles.Common.Models
{
    public class InvokeScriptTransaction: WavesCS.InvokeScriptTransaction
    {
        public void CreateWithoutAsset(Dictionary<string, object> tx)
        {
            DappAddress = tx.GetString("dApp");
            FunctionHeader = tx.ContainsKey("call") ? tx.GetString("call.function") : null;

            FunctionCallArguments = tx.GetObjects("call.args")
                .Select(Node.DataValue)
                .ToList();

            Payment = tx.GetObjects("payment")
                .ToDictionary(o => new Asset(o.GetString("assetId"), "",0), o=> Convert.ToDecimal(o.GetLong("amount")));

            FeeAsset = tx.ContainsKey("feeAssetId") && tx.GetString("feeAssetId") != null ? new Asset(tx.GetString("feeAssetId"), "",0) : Assets.WAVES;
            Fee = FeeAsset.LongToAmount(tx.GetLong("fee"));
        }

        private InvokeScriptTransaction(Dictionary<string, object> tx) : base(tx)
        {
        }

        private InvokeScriptTransaction(char chainId, byte[] senderPublicKey, string dappAddress, string functionHeader, List<object> functionCallArguments, Dictionary<Asset, decimal> payment, decimal fee, Asset feeAsset) : base(chainId, senderPublicKey, dappAddress, functionHeader, functionCallArguments, payment, fee, feeAsset)
        {
        }

        private InvokeScriptTransaction(char chainId, byte[] senderPublicKey, string dappAddress, Dictionary<Asset, decimal> payment, decimal fee, Asset feeAsset) : base(chainId, senderPublicKey, dappAddress, payment, fee, feeAsset)
        {
        }
    }
}