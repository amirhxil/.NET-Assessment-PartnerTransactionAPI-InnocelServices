namespace PartnerTransactionAPI.Models
{
    public class TransactionRequest
    {
        public string partnerkey { get; set; }
        public string partnerrefno { get; set; }
        public string partnerpassword { get; set; } // Base64 encoded
        public long totalamount { get; set; }
        public List<ItemDetail> items { get; set; }
        public string timestamp { get; set; } // ISO 8601 format
        public string sig { get; set; }
    }

}
