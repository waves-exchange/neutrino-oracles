using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeutrinoOracles.Common.Attributes;
using NeutrinoOracles.Common.Models;

namespace NeutrinoOracles.Common.Converters
{
    public class AccountDataConverter
    {
        public static NeutrinoAccountState ToNeutrinoAccountData(IReadOnlyCollection<AccountDataResponse> keyValuePairs) =>
            To<NeutrinoAccountState>(keyValuePairs);
        
        public static LiquidationAccountData ToLiquidationAccountData(IReadOnlyCollection<AccountDataResponse> keyValuePairs) =>
            To<LiquidationAccountData>(keyValuePairs);
        
        public static AuctionAccountState ToAuctionAccountData(IReadOnlyCollection<AccountDataResponse> keyValuePairs) =>
            To<AuctionAccountState>(keyValuePairs);
        
        public static ControlAccountState ToControlAccountData(IReadOnlyCollection<AccountDataResponse> keyValuePairs) =>
            To<ControlAccountState>(keyValuePairs);

        private static T To<T>(IReadOnlyCollection<AccountDataResponse> keyValuePairs)
        {
            var accountData = Activator.CreateInstance(typeof(T));
            var props = typeof(T).GetProperties();

            foreach (var prop in props)
            {
                var accountDataParseInfo =
                    (AccountDataConvertInfo) prop.GetCustomAttribute(typeof(AccountDataConvertInfo));

                var genericArgs = prop.PropertyType.GetGenericArguments();
                var isDictionary = prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() ==
                                   typeof(Dictionary<,>) && genericArgs[0] == typeof(string);

                var pairs = isDictionary
                    ? keyValuePairs.Where(x => x.Key.StartsWith(accountDataParseInfo.Key)).ToList()
                    : keyValuePairs.Where(x => x.Key == accountDataParseInfo.Key).ToList();

                if (!pairs.Any())
                    continue;

                object value = null;

                if (isDictionary)
                {
                    foreach (var pair in pairs)
                    {
                        if (prop.GetValue(accountData) == null && value == null)
                            value = Activator.CreateInstance(prop.PropertyType);
                        
                        if(genericArgs[1].IsInstanceOfType(pair.Value))
                            ((IDictionary) value)?.Add(pair.Key.Replace(accountDataParseInfo.Key, ""), pair.Value);
                    }
                }
                else if(prop.PropertyType.IsInstanceOfType(pairs[0].Value))
                    value = pairs[0].Value;

                prop.SetValue(accountData, value, null);
            }

            return (T) accountData;
        }
    }
}