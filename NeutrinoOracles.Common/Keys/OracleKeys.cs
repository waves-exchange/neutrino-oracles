namespace NeutrinoOracles.Common.Keys
{
    public static class OracleKeys
    {
        public static string Price = "price_";

        public static string GetPriceByHeight(int height) => Price + height;
    }
}