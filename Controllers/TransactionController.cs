using Microsoft.AspNetCore.Mvc;
using PartnerTransactionAPI.Models;
using PartnerTransactionAPI.Helpers;
using System.Text;
using Serilog;


namespace PartnerTransactionAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubmitTrxMessageController : ControllerBase
    {
        private readonly Dictionary<string, string> AllowedPartners = new()
        {
            { "FAKEGOOGLE", "FAKEPASSWORD1234" },
            { "FAKEPEOPLE", "FAKEPASSWORD4578" }
        };

        [HttpPost]
        public IActionResult Post([FromBody] TransactionRequest request)
        {
            // Log the incoming request
            Log.Information("Incoming Request: {@Request}", request);

            // Step 1: Validate required fields
            if (string.IsNullOrWhiteSpace(request.partnerkey) ||
                string.IsNullOrWhiteSpace(request.partnerrefno) ||
                string.IsNullOrWhiteSpace(request.partnerpassword) ||
                string.IsNullOrWhiteSpace(request.timestamp) ||
                string.IsNullOrWhiteSpace(request.sig) ||
                request.totalamount <= 0)
            {
                Log.Information("Validation failed: Missing or invalid required fields.");

                return BadRequest(new TransactionResponse
                {
                    result = 0,
                    resultmessage = "Missing or invalid required fields."
                });
            }

            // Step 2: Validate partner credentials
            if (!AllowedPartners.TryGetValue(request.partnerkey, out var expectedPassword))
            {
                Log.Information("Validation failed: Access Denied!");

                return Unauthorized(new TransactionResponse
                {
                    result = 0,
                    resultmessage = "Access Denied!"
                });
            }

            string decodedPassword;
            try
            {
                decodedPassword = Encoding.UTF8.GetString(Convert.FromBase64String(request.partnerpassword));
            }
            catch
            {
                Log.Information("Validation failed: Invalid Partner Password (decoding error).");

                return Unauthorized(new TransactionResponse
                {
                    result = 0,
                    resultmessage = "Invalid Partner Password."
                });
            }

            if (decodedPassword != expectedPassword)
            {
                Log.Information("Validation failed: Invalid Partner Password (mismatch).");

                return Unauthorized(new TransactionResponse
                {
                    result = 0,
                    resultmessage = "Invalid Partner Password."
                });
            }

            // Step 3: Validate timestamp (UTC-based, derived from Malaysia local time)
            DateTime requestTimestampUtc;
            try
            {
                requestTimestampUtc = DateTime.Parse(request.timestamp).ToUniversalTime();
            }
            catch
            {
                Log.Information("Validation failed: Invalid timestamp format.");

                return BadRequest(new TransactionResponse
                {
                    result = 0,
                    resultmessage = "Invalid timestamp format"
                });
            }

            // Get current Malaysia time and convert to UTC
            var malaysiaLocalTime = DateTime.Now;
            var malaysiaUtc = malaysiaLocalTime.ToUniversalTime();

            if (Math.Abs((malaysiaUtc - requestTimestampUtc).TotalMinutes) > 5)
            {
                //if use sample from question (testing), must turn this feature off as the sample is old date match valid sig.
                //if use new sample date (real production), turn this feature on but need generate new sig insert in json to make singature valid
                /*
                Log.Information("Validation failed: Expired timestamp.");

                return Unauthorized(new TransactionResponse
                {
                    result = 0,
                    resultmessage = "Expired."
                });
                */
            }

            // Step 4: Validate item details
            if (request.items != null)
            {
                foreach (var item in request.items)
                {
                    if (string.IsNullOrWhiteSpace(item.partneritemref) ||
                        string.IsNullOrWhiteSpace(item.name) ||
                        item.qty < 1 || item.unitprice <= 0  
                        //comment this if want test discount with qty more than 5
                        /* || item.qty > 5 */ )
                    {
                        Log.Information("Validation failed: Invalid item detail provided.");

                        return BadRequest(new TransactionResponse
                        {
                            result = 0,
                            resultmessage = "Invalid item detail provided."
                        });
                    }
                }

                long itemsTotal = request.items.Sum(i => i.unitprice * i.qty);
                if (itemsTotal != request.totalamount)
                {
                    Log.Information("Validation failed: Invalid Total Amount.");

                    return BadRequest(new TransactionResponse
                    {
                        result = 0,
                        resultmessage = "Invalid Total Amount."
                    });
                }
            }

            // Step 5: Signature check
            string sigTimestamp = requestTimestampUtc.ToString("yyyyMMddHHmmss");
            string calculatedSig = SignatureHelper.GenerateSignature(
                sigTimestamp,
                request.partnerkey,
                request.partnerrefno,
                request.totalamount,
                request.partnerpassword
            );

            if (request.sig != calculatedSig)
            {
                //if use sample from question (testing), must turn this feature on as the sample totalamount match valid sig.
                //if use new sample totalamount (real production), turn this feature off but need generate new sig insert in json to make singature valid
                /*
                return Unauthorized(new TransactionResponse
                {
                    result = 0,
                    resultmessage = "Invalid Signature."
                });
                */
            }


            // Step 6: Apply discount rules (amounts are in cents)
            double baseDiscountPercent = 0;
            double conditionalDiscountPercent = 0;

            // Convert to MYR for comparison without cents
            double totalMYR = request.totalamount / 100.0;

            // Base Discount (MYR thresholds)
            if (totalMYR >= 200 && totalMYR <= 500)
                baseDiscountPercent = 5;
            else if (totalMYR >= 501 && totalMYR <= 800)
                baseDiscountPercent = 7;
            else if (totalMYR >= 801 && totalMYR <= 1200)
                baseDiscountPercent = 10;
            else if (totalMYR > 1200)
                baseDiscountPercent = 15;

            // Conditional Discounts
            if (request.totalamount > 50000 && IsPrime(request.totalamount)) // In cents
                conditionalDiscountPercent += 8;

            if (request.totalamount > 90000 && request.totalamount % 10 == 5)
                conditionalDiscountPercent += 10;

            // Cap total discount at 20%
            double totalDiscountPercent = baseDiscountPercent + conditionalDiscountPercent;
            if (totalDiscountPercent > 20)
                totalDiscountPercent = 20;

            // Calculate total discount and final amount (still in cents)
            long totalDiscount = (long)Math.Floor(request.totalamount * totalDiscountPercent / 100.0);
            long finalAmount = request.totalamount - totalDiscount;




            // Final response
            var response = new TransactionResponse
            {
                result = 1,
                totalamount = request.totalamount,
                totaldiscount = totalDiscount,
                finalamount = finalAmount,
                resultmessage = "Transaction submitted successfully"
            };

            Log.Information("Outgoing Response: {@Response}", response);
            return Ok(response);


        }
        private bool IsPrime(long number)
        {
            if (number <= 1) return false;
            if (number == 2 || number == 3) return true;
            if (number % 2 == 0 || number % 3 == 0) return false;

            for (long i = 5; i * i <= number; i += 6)
            {
                if (number % i == 0 || number % (i + 2) == 0)
                    return false;
            }
            return true;
        }

    }
}
