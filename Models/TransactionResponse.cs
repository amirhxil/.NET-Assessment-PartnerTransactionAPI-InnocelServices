namespace PartnerTransactionAPI.Models
{
    public class TransactionResponse
    {
        public int result { get; set; } // 1 = success, 0 = failed
        public long? totalamount { get; set; }
        public long? totaldiscount { get; set; }
        public long? finalamount { get; set; }
        public string resultmessage { get; set; }
    }

}
