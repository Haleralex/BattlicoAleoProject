using Microsoft.AspNetCore.Mvc;
using System.Text;
using BigInteger = System.Numerics.BigInteger;
using Newtonsoft.Json;
using RestBBCrypto.KafkaConsumer.LowLevel;
using AleoCSharpLibrary;

namespace RestBBCrypto.Controllers
{
    public class CryptoOperationsPerformerAleo
    {
        private RequestSender _requestSender = new();

        private string defaultMainAddress = "";
        private string defaultMainPrivateKey = "";

        private BigInteger TOKEN_DECIMALS = 6;

        private Action<string, string, CryptoOperationForm> _onGetResponse;


        public BigInteger Fee => TOKEN_DECIMALS * 10;


        public CryptoOperationsPerformerAleo(Action<string, string, CryptoOperationForm> responseCallback)
        {
            _onGetResponse = responseCallback;
        }
        /// <summary>
        /// The method is called when a request for an operation with a crypto wallet arrives
        /// </summary>
        /// <param name="messageId">Message id</param>
        /// <param name="operationForm">Formed request</param>
        public async void OnGetOperation(string messageId, CryptoOperationForm operationForm)
        {
            string id = messageId;
            var form = operationForm;

            Console.WriteLine();

            if (operationForm.operation_type == "Sending")
            {
                var response =
                    await WithDrawTokens(
                        operationForm.user_guid_id,
                        operationForm.user_desirable_address_to_withdraw_to,
                        operationForm.user_desirable_amout_to_withdraw);

                _onGetResponse(id, response.Value!, form);
            }
            else if (operationForm.operation_type == "GetHistory")
            {
                _requestSender.GetDatabaseField(TableName.users, DataBaseFieldName.transactions_history, operationForm.user_guid_id,
                     cb =>
                     {

                         _onGetResponse(id, cb, form);
                     });
            }
            else
            {
                var response = await AutoSendingToMainAccount(
                    operationForm.user_guid_id,
                    operationForm.user_public_key,
                    operationForm.user_private_key);

                _onGetResponse(id, response.Value!, form);
            }
        }

        /// <summary>
        /// Method sends amount tokens from main account to senderAddress
        /// </summary>
        /// <param name="guid_id">UserID</param>
        /// <param name="senderAddress">Sender address</param>
        /// <param name="amount">Amount of tokens</param>
        /// <returns></returns>
        [HttpGet("WithDrawTokens {guid_id} {senderAddress} {amount}")]
        private async Task<ActionResult<string>> WithDrawTokens(string guid_id, string senderAddress, string amount)
        {
            try
            {
                await Console.Out.WriteLineAsync($"Stated withdraw from {guid_id} to {senderAddress}");
                var gold = await GetGold(guid_id);
                var bigIntegerGold = BigInteger.Parse(gold);
                var bigIntegerAmount = BigInteger.Parse(amount) - Fee;

                if (bigIntegerGold < bigIntegerAmount)
                {
                    await Console.Out.WriteLineAsync(
                        $"failed bigIntegerGold = {bigIntegerGold}   bigIntegerAmount = {bigIntegerAmount} ");
                    return "failed";
                }
                else
                {
                    await Console.Out.WriteLineAsync(
                        $" bigIntegerGold = {bigIntegerGold}   bigIntegerAmount = {bigIntegerAmount} ");
                }

                Console.WriteLine("Real transaction started");
                var transactionHash = await SendToken(defaultMainAddress, defaultMainPrivateKey, senderAddress,
                    bigIntegerAmount);
                Console.WriteLine("Real transaction finished");

                var goldAfterTransaction = await GetGold(guid_id);
                var bigIntegerGoldAfterTransaction = BigInteger.Parse(goldAfterTransaction);
                var newGold = (bigIntegerGoldAfterTransaction - BigInteger.Parse(amount)).ToString();

                await SetGold(newGold, guid_id);

                Transaction transaction = new Transaction(DateTime.UtcNow, bigIntegerAmount,
                    TransactionType.Sending, senderAddress, transactionHash);
                transaction.SuccessTransaction();
                var jsonTransaction = transaction.ToJSON();

                await SetNewTransaction(jsonTransaction, guid_id);
                await Console.Out.WriteLineAsync($"Finished withdraw from {guid_id} to {senderAddress}");
                return newGold;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e);
                return "failed";
            }
        }
        /// <summary>
        /// When user's wallet have tokens, funds are automatically redirected to the main account
        /// </summary>
        /// <param name="guid_id">UserID</param>
        /// <param name="senderAddress">Sender address</param>
        /// <param name="senderPrivateKey">Sender private key</param>
        /// <returns></returns>
        [HttpGet("AutoSendingToMainAccount {guid_id} {senderAddress} {senderPrivateKey}")]
        private async Task<ActionResult<string>> AutoSendingToMainAccount(string guid_id, string senderAddress,
            string senderPrivateKey)
        {
            await Console.Out.WriteLineAsync($"Stated AutoSending from {guid_id}");
            var gold = await GetGold(guid_id);
            var bigIntegerGold = BigInteger.Parse(gold);

            var senderBalance = AleoHandler.GetBalance(senderAddress);

            if (senderBalance != 0)
            {
                try
                {
                    await CheckExistenceTokenToFee(senderAddress, senderBalance);

                    var transactionHash =
                        await SendToken(senderAddress, senderPrivateKey, defaultMainAddress, senderBalance);
                    var newGold = (bigIntegerGold + senderBalance).ToString();

                    await SetGold(newGold, guid_id);

                    Transaction transaction = new Transaction(DateTime.UtcNow, senderBalance,
                        TransactionType.Receiving, "", transactionHash);
                    transaction.SuccessTransaction();
                    var jsonTransaction = transaction.ToJSON();

                    await SetNewTransaction(jsonTransaction, guid_id);
                    await Console.Out.WriteLineAsync($"Finished AutoSending from {guid_id}");
                    return newGold;
                }
                catch (Exception ex)
                {
                    await Console.Out.WriteLineAsync($"Finished but not done AutoSending from {guid_id} {ex}");
                    return "failed";
                }
            }
            else
            {
                await Console.Out.WriteLineAsync($"Finished but not done AutoSending from {guid_id}");
                return gold;
            }
        }

        /// <summary>
        /// The availability of the main network tokens to pay the commission for transfers is checked.
        /// </summary>
        /// <param name="recipientAddress">Recepient address</param>
        /// <param name="amount">Amount of tokens</param>
        /// <returns></returns>
        private async Task CheckExistenceTokenToFee(string recipientAddress, BigInteger amount)
        {
            var balance = AleoHandler.GetBalance(recipientAddress, "credits");

            var realGasPrice = 5 * TOKEN_DECIMALS;

            await Console.Out.WriteLineAsync($"balance = {(BigInteger)balance} realGasPrice = {realGasPrice}");
            if ((BigInteger)balance < realGasPrice*2)
            {
                AleoHandler.TransferFromMainAccount(recipientAddress, (realGasPrice * 2).ToString(), "credits");
            }
        }


        private async Task<string> SendToken(string senderAddress, string senderPrivateKey, string recipientAddress,
            BigInteger amount)
        {
            var senderBalance = AleoHandler.GetBalance(senderAddress);
            
            if (senderBalance < amount)
            {
                throw new Exception("Insufficient balance.");
            }

            var transactionHash = AleoHandler.Transfer(recipientAddress, amount.ToString(), senderPrivateKey);

            return transactionHash;
        }

        /// <summary>
        /// Set gold amount in database
        /// </summary>
        /// <param name="gold"></param>
        /// <param name="guid_id"></param>
        /// <returns></returns>
        private async Task SetGold(string gold, string guid_id)
        {
            bool done = false;

            _requestSender.UpdateDatabaseField(TableName.users, "gold", guid_id, gold,
                cb => { done = true; });

            while (!done)
            {
                await Task.Yield();
            }
        }
        /// <summary>
        /// Get gold amount from database
        /// </summary>
        /// <param name="guid_id"></param>
        /// <returns></returns>
        private async Task<string> GetGold(string guid_id)
        {
            bool done = false;
            string gold = string.Empty;

            _requestSender.GetDatabaseField(TableName.users, DataBaseFieldName.gold, guid_id,
                cb =>
                {
                    gold = cb;
                    done = true;
                });

            while (!done)
            {
                await Task.Yield();
            }

            return gold;
        }
        /// <summary>
        /// Set transactions history in database
        /// </summary>
        /// <param name="newTransaction"></param>
        /// <param name="guid_id"></param>
        /// <returns></returns>
        private async Task SetNewTransaction(string newTransaction, string guid_id)
        {
            Console.WriteLine("Set new transaction to history: " + newTransaction);

            bool done = false;
            string transactions = string.Empty;

            _requestSender.GetDatabaseField(TableName.users, "transactions_history", guid_id,
                cb =>
                {
                    transactions = cb;
                    done = true;
                });

            while (!done)
            {
                await Task.Yield();
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(transactions);
            builder.Append('#');
            builder.Append(newTransaction);

            done = false;

            _requestSender.UpdateDatabaseField(TableName.users, "transactions_history", guid_id,
                builder.ToString(),
                cb => { done = true; });

            while (!done)
            {
                await Task.Yield();
            }
        }
    }
}