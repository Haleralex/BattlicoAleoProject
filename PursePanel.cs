using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using BB.Core;
using BB.Crypto;
using Brawlers.Server.WebSocketClient;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Server;
using Server.Database;
using TMPro;
using UI.MVVM.Models;
using UI.MVVM.ViewModels;
using UI.MVVM.Views;
using UniRx;
using UnityEngine;
using Account = Nethereum.Web3.Accounts.Account;

namespace BB.UI.Panels
{
    public class PursePanel : UISystemPanel
    {
        public event System.Action BalanceUpdated;

        [SerializeField] private WalletView view;
        [SerializeField] private TransactionsHistory history;
        [SerializeField] private TextMeshProUGUI inputAddress;
        [SerializeField] private TextMeshProUGUI balance;
        [SerializeField] private TextMeshProUGUI frozenBalance;
        [SerializeField] private TMP_InputField outputAddress;
        [SerializeField] private TMP_InputField amountToWithDraw;
        [SerializeField] private PopupSuccess popupSuccessAddressCopied;
        [SerializeField] private CollectingMenuControllers controllers;

        private CompositeDisposable _disposable = new();
        private BigInteger currentCryptoBalance;
        private bool _requestOnAutoSendingWasSent = false;
        private bool OnMessageReceivedBlockator = false;


        private IEnumerator RequestOnAutoSendingToolBarOff()
        {
            yield return new WaitForSeconds(10);
            _requestOnAutoSendingWasSent = false;
        }
        /// <summary>
        /// Display on user interface user's wallet address
        /// </summary>
        /// <param name="fullForm"></param>
        /// <returns></returns>
        private string CreateShortFormCryptoAddress(string fullForm)
        {
            var builder = new StringBuilder();
            var firstPart = fullForm.Substring(0, 3);
            var lastPart = fullForm.Substring(fullForm.Length - 4, 3);
            builder.Append(firstPart);
            builder.Append("...");
            builder.Append(lastPart);
            return builder.ToString();
        }
        /// <summary>
        /// Awake implemetation
        /// </summary>
        public override async void DisableImplementation()
        {
            var model = new WalletModel(history);
            var viewModel = new WalletViewModel(model);
            view.Init(viewModel);
            view.Show();

            while (controllers.WebSocketController is null)
                await UniTask.Yield();

            controllers.WebSocketController.OnMessageReceive.Subscribe(OnMessageReceived).AddTo(_disposable);

            frozenBalance.text = CurrentProperties.Instance.CachedUserInfo.FrozenGoldAmount == "" ? "0" : BigIntegerToString(CurrentProperties.Instance.CachedUserInfo.FrozenGoldAmount);

            var message = CryptoMessageFormer("GetHistory", "");

            RequestSender.Instance.SendCryptoMessage(message, cb =>
            {
                TrySetInfosToViews(cb.ResponseMessage);
            });
        }

        /// <summary>
        /// Method to set current info about user's transactions on user interface
        /// </summary>
        /// <param name="callback"></param>
        private void TrySetInfosToViews(string callback)
        {
            var splitted = callback.Split("#");
            var transactions = new List<Transaction>();
            foreach (var split in splitted)
            {
                if (string.IsNullOrEmpty(split.Trim()))
                    continue;
                try
                {
                    var transaction = JsonConvert.DeserializeObject<Transaction>(split);
                    transactions.Add(transaction);
                }
                catch (System.Exception e)
                {

                }
            }
            history.SetInfosToViews(transactions);
        }

        /// <summary>
        /// callback to handle crypto message (usually callback message) from server
        /// </summary>
        /// <param name="socketMessage"></param>
        private void OnMessageReceived(WebSocketMessage socketMessage)
        {
            if (socketMessage.operation is SendOperation.CryptoOperation)
            {
                UpdateCurrentUI();
                if (!OnMessageReceivedBlockator)
                {
                    OnMessageReceivedBlockator = true;
                    var message = CryptoMessageFormer("GetHistory", "");
                    RequestSender.Instance.SendCryptoMessage(message, cb =>
                    {
                        TrySetInfosToViews(cb.ResponseMessage);
                        Invoke(nameof(UnblockMessageReceiver), 6);
                        view.UnblockSendButton();
                    });
                }
            }
        }

        private void UnblockMessageReceiver()
        {
            OnMessageReceivedBlockator = false;
        }

        private async void UpdateCurrentUI()
        {
            var gold = (await GetGold(UserManager.UserProfile.guid_id)).Trim();
            var frozenGold = (await GetFrozenGold(UserManager.UserProfile.guid_id)).Trim();
            UserManager.CurrentUser.SetField("gold", gold);
            UserManager.CurrentUser.SetField("frozen_gold", frozenGold);

            float currentBalance = float.Parse(CurrentProperties.Instance.CachedUserInfo.GoldAmount);
            balance.text = $"{currentBalance * 0.01f:0.00}";

            frozenBalance.text = BigIntegerToString(CurrentProperties.Instance.CachedUserInfo.FrozenGoldAmount == "" ? "0" : CurrentProperties.Instance.CachedUserInfo.FrozenGoldAmount);
        }
        /// <summary>
        /// Execute when user enter purse panel
        /// </summary>
        public override async void EnterImplementation()
        {
            inputAddress.text = "id: " + CreateShortFormCryptoAddress(CurrentProperties.Instance.CachedUserInfo.InputWallet);
            float currentBalance = float.Parse(CurrentProperties.Instance.CachedUserInfo.GoldAmount);
            balance.text = $"{currentBalance * 0.01f:0.00}";

            frozenBalance.text = CurrentProperties.Instance.CachedUserInfo.FrozenGoldAmount == "" ? "0" : BigIntegerToString(CurrentProperties.Instance.CachedUserInfo.FrozenGoldAmount);

            if (!_requestOnAutoSendingWasSent)
            {
                var message = CreateCryptoMessage(TransactionType.Receiving, "");

                RequestSender.Instance.SendCryptoMessage(message, cb =>
                {
                    Debug.Log("Crypto receiving: " + cb);
                });
                _requestOnAutoSendingWasSent = true;
                StartCoroutine(RequestOnAutoSendingToolBarOff());
            }
        }
        /// <summary>
        /// Execute when user exit purse panel
        /// </summary>
        public override void ExitImplementation()
        {

        }

        public override void UpdateImplementation()
        {

        }

        /// <summary>
        /// Methdo to withdraw tokens. Input data received through the user interface
        /// </summary>
        public void WithDrawTokens()
        {
            var amount = StringToBigInteger(amountToWithDraw.text);

            var message = CreateCryptoMessage(TransactionType.Sending, amount);
            view.BlockSendButton();
            RequestSender.Instance.SendCryptoMessage(message, cb => { });
        }
        /// <summary>
        /// Method form crypto message for sending to server
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private string CreateCryptoMessage(TransactionType operation, string amount)
        {
            var form = CryptoMessageFormer(operation.ToString(), amount);
            return form;
        }

        private string StringToBigInteger(string text)
        {
            var builder = new StringBuilder(text);
            builder.Append("00");
            text = builder.ToString();
            if (text.Contains(','))
            {
                var indexOfPoint = text.IndexOf(',');
                text = text.Replace(",", "");
                text = text.Insert(indexOfPoint + 2, ",");
                return text.Split(',')[0];
            }
            else if (text.Contains('.'))
            {
                var indexOfPoint = text.IndexOf('.');
                text = text.Replace(".", "");
                text = text.Insert(indexOfPoint + 2, ".");
                return text.Split('.')[0];
            }
            else
            {
                return text;
            }
        }

        private string BigIntegerToString(string text)
        {
            var builder = new StringBuilder(text);

            while (builder.Length < 3)
            {
                builder.Append("0");
            }

            builder.Insert(builder.Length - 2, ".");

            return builder.ToString();
        }

        /// <summary>
        /// Method execute by tapping on user's token address
        /// </summary>
        public void CopyToClipboard()
        {
            TextEditor textEditor = new TextEditor
            {
                text = CurrentProperties.Instance.CachedUserInfo.InputWallet
            };
            textEditor.SelectAll();
            textEditor.Copy();
            popupSuccessAddressCopied.Popup(true);
        }
        /// <summary>
        /// Method execute by tapping on custom token address
        /// </summary>
        public void CopySmartContractToClipboard()
        {
            TextEditor textEditor = new TextEditor
            {
                text = DEFAULT_TOKEN_ADDRESS
            };
            textEditor.SelectAll();
            textEditor.Copy();
        }

        private string CryptoMessageFormer(string operationType, string amount)
        {
            var form = new CryptoOperationForm()
            {
                operation_type = operationType,
                user_guid_id = UserManager.UserProfile.guid_id,
                user_public_key = CurrentProperties.Instance.CachedUserInfo.InputWallet,
                user_private_key = CurrentProperties.Instance.CachedUserInfo.PrivateKeyWallet,
                user_desirable_amout_to_withdraw = amount,
                user_desirable_address_to_withdraw_to = outputAddress.text
            };
            var jsonForm = JsonConvert.SerializeObject(form);

            return jsonForm;
        }

        private async Task<string> GetGold(string guid_id)
        {
            bool done = false;
            string gold = "";
            RequestSender.Instance.GetDatabaseField(TableName.users, DataBaseFieldName.gold, guid_id,
                cb =>
                {
                    gold = cb.ResponseMessage;
                    done = true;
                });

            while (!done)
            {
                await Task.Yield();
            }

            return gold;
        }

        private async Task<string> GetFrozenGold(string guid_id)
        {
            bool done = false;
            string gold = "";
            RequestSender.Instance.GetDatabaseField(TableName.users, "frozen_gold", guid_id,
                cb =>
                {
                    gold = cb.ResponseMessage;
                    done = true;
                });

            while (!done)
            {
                await Task.Yield();
            }

            return gold;
        }
    }


    [System.Serializable]
    public class CryptoOperationForm
    {
        public string operation_type;

        public string user_guid_id;

        public string user_public_key;

        public string user_private_key;

        public string user_desirable_amout_to_withdraw;

        public string user_desirable_address_to_withdraw_to;
    }
}