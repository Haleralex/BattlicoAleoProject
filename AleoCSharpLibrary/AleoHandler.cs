using System.Diagnostics;
using System.Text;
using System.Management.Automation;
using System.Numerics;
using System;
namespace AleoCSharpLibrary
{
    public class AleoHandler
    {
        private const string SENDER_PRIVATE_KEY = "";

        /// <summary>
        /// Method to create a new account in aleo testnet3
        /// </summary>
        /// <returns></returns>
        public static string CreateNewAccount()
        {
            string filePathTransactions = "..\\..\\..\\ShellScripts\\NewAccount.sh";
            string fileContentTransactions = File.ReadAllText(filePathTransactions);
            PSDataCollection<PSObject> response = ShellExecutor.Command(fileContentTransactions);
            var handledResponse = HandleCreateNewAccountResponse(response);
            return handledResponse;
        }

        /// <summary>
        /// Method to transfer tokens from main account
        /// </summary>
        /// <param name="targetAddress">Address where tokens are sent</param>
        /// <param name="amount">Number of tokens sent</param>
        /// <param name="appName">Name of the deployed smart contract</param>
        /// <returns></returns>
        public static string TransferFromMainAccount(string targetAddress, string amount, string appName = "battlico_token")
        {
            var handledResponse = Transfer(targetAddress, amount, SENDER_PRIVATE_KEY, appName);
            return handledResponse;
        }
        /// <summary>
        /// Method to transfer tokens from privateKey owner
        /// </summary>
        /// <param name="targetAddress">Address where tokens are sent</param>
        /// <param name="amount">Number of tokens sent</param>
        /// <param name="privateKey">Sender's private key</param>
        /// <param name="appName">Name of the deployed smart contract</param>
        /// <returns></returns>
        public static string Transfer(string targetAddress, string amount, string privateKey, string appName = "battlico_token")
        {
            string filePathTransactions = "..\\..\\..\\ShellScripts\\Transactions.sh";
            string fileContentTransactions = File.ReadAllText(filePathTransactions);
            fileContentTransactions = fileContentTransactions.Replace("TARGET_ADDRESS", targetAddress).Replace("AMOUNT", amount).Replace("SENDER_PRIVATE_KEY", privateKey).Replace("APP_NAME", appName);
            PSDataCollection<PSObject> response = ShellExecutor.Command(fileContentTransactions);
            var handledResponse = HandleTransferFromMainAccountResponse(response);
            return handledResponse;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetAddress">Address of the required account</param>
        /// <param name="appName">Name of the deployed smart contract</param>
        /// <returns></returns>
        public static BigInteger GetBalance(string targetAddress, string appName = "battlico_token")
        {
            string filePathBalances = "..\\..\\..\\ShellScripts\\Balances.sh";
            string fileContentBalances = File.ReadAllText(filePathBalances);
            fileContentBalances = fileContentBalances.Replace("ADDRESS_TO_GET_BALANCE", targetAddress).Replace("APP_NAME", appName);
            var response = ShellExecutor.Command(fileContentBalances);
            var handledResponse = HandleGetBalanceResponse(response);
            return handledResponse;
        }

        private static string HandleTransferFromMainAccountResponse(PSDataCollection<PSObject> response)
        {
            try
            {
                var respondString = response[0].ToString();
                var splittedRespond = respondString.Split(" ");
                int index = 0;
                for (int i = 0; i < splittedRespond.Length; i++)
                {
                    if (splittedRespond[i] == "Successfully")
                    {
                        index = i + 3; break;
                    }
                }
                string transactionID = splittedRespond[index];
                return transactionID;
            }
            catch (Exception ex)
            {
                return "Error " + ex.ToString();
            }
        }

        private static BigInteger HandleGetBalanceResponse(PSDataCollection<PSObject> response)
        {
            try
            {
                var responseHandledString = response[0].ToString().Replace("u64", "").Trim();
                responseHandledString = responseHandledString.Replace("\"", "");
                var bigIntegetResponse = BigInteger.Parse(responseHandledString);
                return bigIntegetResponse;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        private static string HandleCreateNewAccountResponse(PSDataCollection<PSObject> response)
        {
            try
            {
                var responseHandledString = response[0].ToString();
                return responseHandledString;
            }
            catch (Exception ex)
            {
                return "Error " + ex.ToString();
            }
        }
    }
}
