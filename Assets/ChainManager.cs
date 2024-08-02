using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Utils;

namespace ChainConnection
{
    public class ChainManager : MonoBehaviour
    {
        public static ChainManager Instance;
        private HttpRequestController _httpRequestController;
        private static string _chainBaseUrl = "https://free-rpc.nethermind.io/sepolia-juno/v0_7";
        private static string classHash = "0x07f473748dc223965c6636fe2e07b24b849e6be5592d94a934d1416711091cd3";

        /// <summary>
        /// Multiplier for estimate fee
        /// </summary>
        private static float _feeEstimateMultiplier = 1.5f;

        #region ChainStatusStr

        private const string ACCEPTEDONL2 = "ACCEPTED_ON_L2";
        private const string ACCEPTEDONL1 = "ACCEPTED_ON_L1";
        private const string HTTPERROR = "HTTPERROR";
        private const string PENDING = "PENDING";
        private const string RECEIVED = "RECEIVED";
        private const string REVERTED = "REVERTED";
        private const string SUCCEEDED = "SUCCEEDED";
        private const string TRANSACTIONNOTFOUND = "TRANSACTION_NOT_FOUND";
        private const string RETRY = "RETRY";
        private const string ILLEGALRESPONSE = "ILLEGALRESPONSE";
        private const string REJECTED = "REJECTED";

        #endregion

        #region Chain Relative Constant Value

        /// <summary>
        /// Unit is ms
        /// </summary>
        private int _checkInterval = 3000;

        private int _timeBetweenCheckStatusAndReceipt = 5000;
        private int _prolongIntervalTimeSpan = 5000;

        #endregion

        // Start is called before the first frame update
        void Start()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _httpRequestController = HttpRequestController.Instance;
        }

        #region TestExample

        /// <summary>
        /// Generate open zeppelin wallet and deploy, please add relative info to finish the method
        /// </summary>
        public async void TestDeployNewAccount()
        {
            var keyPair = StarknetBridge.generate_key_pair();

            var publicKey_hex = FormatBigIntStrToHexStr(keyPair.public_key);
            ;
            var privateKey_hex = FormatBigIntStrToHexStr(keyPair.private_key);

            // this must be bigint string rather than hex
            var selfContractAddress = StarknetBridge.get_open_zeppelin_contract_address(publicKey_hex, classHash);
            var selfContractAddressHex = FormatBigIntStrToHexStr(selfContractAddress);


            var res = await TransferEthToNewAccount("sender public key", "sender private key",
                "sender contract address", "receiver contract address", 1);

            if (!res)
            {
                return;
            }

            print("transfer eth success");
            var deployRes = await DeployNewAccount(publicKey_hex, privateKey_hex, selfContractAddressHex, 1);
            print("deploy res " + deployRes);
        }


        public async void ManualCheckTransaction()
        {
            var transHash = "0x662bae222d1c9cfbda4351c3b6f5a6ca5426ea1a4cdc9ed4d4aab9609d722ad";

            var end = await RequestTransactionReceiptStatus(transHash);

            print("end of transaction " + JsonConvert.SerializeObject(end));
        }

        public async void ManualCheckTransactionReceipt()
        {
            var transHash = "0x1ce184c85fcb6e0fa42843b07d23d6056ee6c20bd621c229ae02132b6db0551";

            var count = 0;
            var maxCount = 2;
            while (true && count < maxCount)
            {
                var transactionStatus =
                    await RequestTransactionReceiptStatus(transHash);

                print("check status " + transactionStatus);
                switch (transactionStatus)
                {
                    case RETRY:
                        break;
                    case SUCCEEDED:
                        return;
                    // break;
                    case REVERTED:
                        return;
                    case ILLEGALRESPONSE:
                        break;
                    default:
                        Debug.Log("UnknownStatus " + transactionStatus);
                        break;
                }

                count++;

                await Task.Delay(1000);
            }
        }

        #endregion

        /// <summary>
        /// Request the estimate fee
        /// </summary>
        /// <param name="requestData">All params for send to chain</param>
        /// <returns></returns>
        public async Task<string> RequestEstimateFee(object requestData)
        {
            print("start request max fee");

            var postBody = new StarknetChainPostBodyStruct()
            {
                method = "starknet_estimateFee",
                jsonrpc = "2.0",
                id = 3,
                Params = new
                {
                    request = new List<object>()
                    {
                        requestData
                    },
                    block_id = "pending",
                    simulation_flags = new string[] { "SKIP_VALIDATE" },
                },
            };

            GeneralRequestResult response = (await _httpRequestController.SendHttpRequest(
                _chainBaseUrl,
                RequestType.POST,
                postBody
            ));

            print(JsonConvert.SerializeObject(response));

            if (response.state == UnityWebRequest.Result.Success)
            {
                if (TryParseGasJson(response.result, out var gasDatas))
                {
                    // Request successeed
                    var finalEstimateFee = ConvertOverallFeeByMultiplierHexStr(gasDatas[0].overall_fee);
                    return finalEstimateFee;
                }
                else
                {
                    Debug.LogError("Unknown Error from request gas");
                    // request fail
                    return null;
                }
            }
            else
            {
                Debug.LogError("Some Internet Error Happens");
                return null;
            }
        }

        #region Make invoke transaction

        /// <summary>
        /// Estimate fee default value for estimate fee post body
        /// </summary>
        private const string _estimateMaxFee = "0x0";

        /// <summary>
        /// This method is used for make transaction, invoke the starknet method is starknet_addInvokeTransaction
        /// </summary>
        /// <param name="extraParams">Params used to calculate the signature, passing all contents below, excluding the number of parameters</param>
        /// <param name="selfPrivateKey">The private key of the sender</param>
        /// <param name="selfContractAddress">The contract address of the sender</param>
        /// <param name="cairoVersion">Cairo version, default is 1</param>
        /// <param name="selector">Name of the entry of the destination contract address as a string</param>
        /// <param name="selectorHex">Hex value of the entry string of the destination contract address</param>
        /// <param name="toContractAddress">The destination contract address</param>
        /// <param name="chainId">Chain ID as an int, 1 is the main net, 4 is Katana, 5 is the test net</param>
        /// <returns>(bool, string) - (state, transactionHash)</returns>
        public async Task<(bool, string)> MakeInvokeTransaction(List<string> extraParams,
            string selfPrivateKey, string selfContractAddress, int cairoVersion, string selector, string selectorHex,
            string toContractAddress, int chainId)
        {
            // Get nonce
            string nonce = await RequestNonce(selfContractAddress);

            // Construct post body calldata
            var sigCallDataCount = extraParams.Count.ToString();
            List<string> postCallData = ConstructPostCallDataPrefix(cairoVersion, toContractAddress, selectorHex,
                sigCallDataCount);
            postCallData.AddRange(extraParams);
            var postBodyHex = ConvertCallDataItemIntoHex(postCallData);

            // construct estimate fee param data
            var requestParams = new TransactionRequestParams()
            {
                type = "INVOKE",
                sender_address = selfContractAddress,
                calldata = postBodyHex,
                version = "0x1",
                signature = new List<string>(),
                nonce = nonce,
                max_fee = _estimateMaxFee
            };

            var maxFee = await RequestEstimateFee(requestParams);

            if (string.IsNullOrEmpty(maxFee))
            {
                Debug.LogError("Request Max Fee Fail");
                return (false, null);
            }

            Signatures sigs = GetSignature(
                privateKey: selfPrivateKey,
                contractAddress: selfContractAddress,
                to: toContractAddress,
                nonce: nonce,
                methodName: selector,
                calldata: extraParams,
                maxFee: maxFee,
                cairoVersion: cairoVersion,
                chainId: chainId);

            var rSigHex = NumConverter.ConvertBigIntStrToHexStr(sigs.r_sig);
            var sSigHex = NumConverter.ConvertBigIntStrToHexStr(sigs.s_sig);

            var starknetPostBody = new StarknetChainPostBodyStruct()
            {
                method = "starknet_addInvokeTransaction",
                jsonrpc = "2.0",
                Params = new
                {
                    invoke_transaction = new
                    {
                        requestParams.sender_address,
                        requestParams.calldata,
                        requestParams.type,
                        max_fee = maxFee,
                        requestParams.version,
                        signature = new[] { rSigHex, sSigHex },
                        requestParams.nonce
                    },
                },
                id = 0
            };

            var transactionResult = await MakeTransactionOnChain(starknetPostBody);

            if (transactionResult.Contains("error"))
            {
                Debug.LogError(transactionResult);
                return (false, null);
            }

            return (true, transactionResult);

            // //Check transaction status
            // var transactionConfirmed =
            //     await TrackCheckTransactionStatus(transactionResult);
            //
            // if (transactionConfirmed)
            // {
            //     return true;
            // }
            //
            // Debug.LogError("Transaction eth fail " + transactionResult);
            // return false;
        }

        public async Task<string> MakeTransactionOnChain(StarknetChainPostBodyStruct starknetPostBodyStruct)
        {
            var response = await HttpRequestController.Instance.SendHttpRequest(
                path: _chainBaseUrl,
                type: RequestType.POST,
                starknetPostBodyStruct
            );

            // Network error or backend no response
            if (response.state != UnityWebRequest.Result.Success)
            {
                return "error : " + response.result;
            }

            // The backend return result
            if (KatanaResultProcess(response.result, out JToken result))
            {
                // Debug.Log("MakeTransactionOnChain result: " + JsonConvert.SerializeObject(response));
                JObject jObject = result.Value<JObject>();
                // print(result["transaction_hash"]);
                return (string)jObject["transaction_hash"];
            }
            else
            {
                JObject jObject = result.Value<JObject>();
                return "error : " + jObject["code"] + jObject["message"];
            }
        }

        #endregion

        #region Transfer Eth for new account

        /// <summary>
        /// Example demo, try with your own test wallet
        /// </summary>
        /// <param name="selfPublicKey"></param>
        /// <param name="selfPrivateKey"></param>
        /// <param name="selfContractAddress"></param>
        /// <param name="receiveContractAddress"></param>
        /// <param name="cairoVersion"></param>
        /// <returns></returns>
        public async Task<bool> TransferEthToNewAccount(string selfPublicKey,
            string selfPrivateKey, string selfContractAddress, string receiveContractAddress, int cairoVersion)
        {
            var u256Prefix = "0x0";
            var ethDefaultAmount = "0x38d7ea4c68000";

            List<string> sigCallData = new()
            {
                receiveContractAddress,
                ethDefaultAmount,
                u256Prefix,
            };

            // Get nonce
            string nonce = await RequestNonce(selfPublicKey);

            string selector = "Target Entry Name";
            string selectorHex = "Target Entry Name Hex Str";
            var toContransctAddress = "Target Contract address";

            // 构建invoke data
            var sigCallDataCount = sigCallData.Count.ToString();

            List<string> postCallData = ConstructPostCallDataPrefix(cairoVersion, toContransctAddress, selectorHex,
                sigCallDataCount);
            postCallData.AddRange(sigCallData);
            var postBodyHex = ConvertCallDataItemIntoHex(postCallData);

            // construct estimate fee param data
            var requestParams = new TransactionRequestParams()
            {
                type = "INVOKE",
                sender_address = selfContractAddress,
                calldata = postBodyHex,
                version = "0x1",
                signature = new List<string>(),
                nonce = nonce,
                max_fee = "0x0"
            };

            var maxFee = await RequestEstimateFee(requestParams);

            if (string.IsNullOrEmpty(maxFee))
            {
                Debug.LogError("Request Max Fee Fail");
                return false;
            }

            print("max fee" + maxFee);
            Signatures sigs = GetSignature(privateKey: selfPrivateKey,
                contractAddress: selfContractAddress,
                to: toContransctAddress,
                nonce: nonce,
                methodName: selector,
                calldata: sigCallData,
                maxFee: maxFee,
                cairoVersion: cairoVersion,
                chainId: 5);


            var RSigHex = NumConverter.ConvertBigIntStrToHexStr(sigs.r_sig);
            var SSigHex = NumConverter.ConvertBigIntStrToHexStr(sigs.s_sig);

            print(RSigHex + "   " + SSigHex);

            var starknetPostBody = new StarknetChainPostBodyStruct()
            {
                method = "starknet_addInvokeTransaction",
                jsonrpc = "2.0",
                Params = new
                {
                    invoke_transaction = new
                    {
                        requestParams.sender_address,
                        requestParams.calldata,
                        requestParams.type,
                        max_fee = maxFee,
                        requestParams.version,
                        signature = new[] { RSigHex, SSigHex },
                        requestParams.nonce
                    },
                },
                id = 0
            };

            print("transfer eth " + JsonConvert.SerializeObject(starknetPostBody));

            var transactionResult = await MakeTransactionOnChain(starknetPostBody);

            if (transactionResult.Contains("error"))
            {
                Debug.LogError(transactionResult);
                return false;
            }

            //Check transaction status
            var transactionConfirmed =
                await CheckChainTransactionSucceed(transactionResult);

            if (transactionConfirmed == TransactionExecutionStatus.Succeeded)
            {
                return true;
            }

            Debug.LogError("Transaction eth fail " + transactionResult);
            return false;
        }

        #endregion

        #region Deploy New Account

        public async Task<string> DeployNewAccount(string selfPublicKey,
            string selfPrivateKey, string selfContractAddress, int cairoVersion)
        {
            print("I am deploying account");

            string low_case_public_key = selfPublicKey.ToLower();
            string low_case_private_key = selfPrivateKey.ToLower();
            string nonce = "0x0";

            // string max_fee = "0x45915c8822400";
            var requestParams = new
            {
                type = "DEPLOY_ACCOUNT",
                contract_address_salt = low_case_public_key,
                class_hash = classHash,
                constructor_calldata = new string[] { low_case_public_key },
                max_fee = "0x0",
                version = "0x1",
                nonce = nonce,
                signature = new string[]
                {
                }
            };

            var maxFee = await RequestEstimateFee(requestParams);
            //The privatekey and public key must be hex
            var sigInput = new SignatureDeployInput()
            {
                private_key = low_case_private_key,
                public_key = low_case_public_key,
                nonce = nonce,
                max_fee = maxFee,
                salt = low_case_public_key
            };


            Signatures sigs = StarknetBridge.get_open_zeppelin_deploy_account_signature(sigInput, 5, classHash);

            var postBody = new StarknetDeployAccountBody()
            {
                type = "DEPLOY_ACCOUNT",
                contract_address_salt = low_case_public_key,
                class_hash = classHash,
                constructor_calldata = new string[] { low_case_public_key },
                max_fee = maxFee,
                version = "0x1",
                nonce = nonce,
                signature = new string[]
                {
                    NumConverter.ConvertBigIntStrToHexStr(sigs.r_sig.ToLower()),
                    NumConverter.ConvertBigIntStrToHexStr(sigs.s_sig.ToLower())
                }
            };

            StarknetChainPostBodyStruct starknetChainPostBody = new StarknetChainPostBodyStruct()
            {
                method = "starknet_addDeployAccountTransaction",
                jsonrpc = "2.0",
                id = 0,
                Params = new { deploy_account_transaction = postBody },
            };

            print("start deploy");

            print("deploy params body " + JsonConvert.SerializeObject(starknetChainPostBody));

            // print("remote " + remoteUrl);

            var response = await HttpRequestController.Instance.SendHttpRequest(
                path: _chainBaseUrl,
                type: RequestType.POST,
                starknetChainPostBody
            );

            // Network error or backend no response
            if (response.state != UnityWebRequest.Result.Success)
            {
                return "error : " + response.result;
            }

            var responseResult = JObject.Parse(response.result);

            // Error check
            if (responseResult["error"] != null)
            {
                var error = responseResult["error"];
                return "error : " + error["message"];
            }

            // Success with transaction hash
            var transactionHash = responseResult["result"]?["transaction_hash"]?.ToString();
            var res = await CheckChainTransactionSucceed(transactionHash);

            if (res == TransactionExecutionStatus.Succeeded)
            {
                return "success";
            }

            return "fail";
        }

        #endregion

        #region Check Transaction Status

        /// <summary>
        /// Check transaction confirmed on chain
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns></returns>
        public async Task<TransactionExecutionStatus> CheckChainTransactionSucceed(string transactionHash)
        {
            print("Check transaction hash " + transactionHash);

            // check transaction is legal
            if (!IsLegalTransactionHash(transactionHash))
            {
                return TransactionExecutionStatus.IllegalTransactionHash;
            }

            // check status 
            var checkStatusInterval = _checkInterval;
            var statusReceived = false;
            // var notFoundMaxTimes = 50;
            // var currentRetryNotFound = 0;
            while (!statusReceived)
            {
                await Task.Delay(checkStatusInterval);

                var transactionStatus =
                    await RequestTransactionStatus(transactionHash);

                switch (transactionStatus)
                {
                    case SUCCEEDED:
                        return TransactionExecutionStatus.Succeeded;
                    case RECEIVED:
                        // chain receive，跳出循环
                        statusReceived = true;
                        break;
                    case REVERTED:
                        // 此时已经Reverted
                        // statusReceived = true;
                        // break;
                        return TransactionExecutionStatus.Reverted;
                    case PENDING:
                        // 继续循环,chain接收到transaction，但是还没有确认
                        break;
                    case REJECTED:
                        // The transaction was received by the mempool but failed validation in the sequencer.
                        // reject的transaction hash永久失效
                        return TransactionExecutionStatus.Rejected;
                    case TRANSACTIONNOTFOUND:
                        // chain繁忙，chain还没有接收到transaction
                        // currentRetryNotFound++;
                        break;
                    case HTTPERROR:
                        // 网络链接错误
                        // 跳出loop等待网络恢复
                        checkStatusInterval = _prolongIntervalTimeSpan;
                        break;
                    default:
                        return TransactionExecutionStatus.Unknown;
                }
            }

            await Task.Delay(_timeBetweenCheckStatusAndReceipt);

            var checkInterval = _checkInterval;
            // check receipe
            while (true)
            {
                await Task.Delay(checkInterval);

                var transactionStatus =
                    await RequestTransactionReceiptStatus(transactionHash);

                print("check status " + transactionStatus);
                switch (transactionStatus)
                {
                    case SUCCEEDED:
                        return TransactionExecutionStatus.Succeeded;
                    case REVERTED:
                        return TransactionExecutionStatus.Reverted;
                    case RETRY:
                        break;
                    case ILLEGALRESPONSE:
                        break;
                    case TRANSACTIONNOTFOUND:
                        break;
                    case HTTPERROR:
                        // 延长重试时间等待网络恢复
                        checkInterval = _prolongIntervalTimeSpan;
                        break;
                    default:
                        Debug.Log("UnknownStatus " + transactionStatus);
                        break;
                }
            }
        }

        // public async Task<bool> ResumeCheckChainTransactionSucceed(string transactionHash)
        // {
        // }

        #endregion

        #region Chain Status Request

        /// <summary>
        /// Check Transaction Status
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns></returns>
        public async Task<string> RequestTransactionStatus(string transactionHash)
        {
            StarknetChainPostBodyStruct postBody = new StarknetChainPostBodyStruct()
            {
                method = "starknet_getTransactionStatus",
                jsonrpc = "2.0",
                id = 1,
                Params = new
                {
                    transaction_hash = transactionHash
                },
            };

            print("Check transaction post body " + JsonConvert.SerializeObject(postBody));

            var response =
                await HttpRequestController.Instance.SendHttpRequest(path: _chainBaseUrl, RequestType.POST, postBody);

            print("check transaction resposse" + JsonConvert.SerializeObject(response));
            if (response.state == UnityWebRequest.Result.Success)
            {
                CheckTransactionStatus(response.result, out var status);
                return status;
            }
            else
            {
                Debug.LogError("Some Internet Error Happens");
                return HTTPERROR;
            }
        }

        /// <summary>
        /// 返回status
        /// </summary>
        /// <param name="responseResult"></param>
        /// <param name="status">can be null</param>
        /// <returns></returns>
        public bool CheckTransactionStatus(string responseResult, out string status)
        {
            try
            {
                JObject jsonResponse = JObject.Parse(responseResult);

                if (jsonResponse["result"] != null)
                {
                    var result = jsonResponse["result"];
                    var finalityStatus = result["finality_status"];
                    if (finalityStatus != null)
                    {
                        string finalStatusStr = finalityStatus.Value<string>();
                        status = finalStatusStr;
                        switch (finalStatusStr)
                        {
                            case RECEIVED:
                            {
                                return true;
                            }
                            case REJECTED:
                            {
                                return false;
                            }
                            case ACCEPTEDONL2:
                            {
                                var executionStatus = result["execution_status"];
                                if (executionStatus == null) return false;
                                string executionStatusStr = executionStatus.Value<string>();
                                status = executionStatusStr;

                                if (status == SUCCEEDED)
                                {
                                    return true;
                                }

                                // another one must be reverted
                                return false;
                            }
                            case ACCEPTEDONL1:
                            {
                                var executionStatus = result["execution_status"];
                                if (executionStatus == null) return false;
                                string executionStatusStr = executionStatus.Value<string>();
                                status = executionStatusStr;

                                if (status == SUCCEEDED)
                                {
                                    return true;
                                }

                                // another one must be reverted
                                return false;
                            }
                        }
                    }
                }
                else if (jsonResponse["error"] != null)
                {
                    var errorJToken = jsonResponse["error"];

                    status = errorJToken.ToString();

                    if (errorJToken["code"] != null)
                    {
                        if (errorJToken["code"].Value<int>() == 29)
                        {
                            status = TRANSACTIONNOTFOUND;
                        }
                    }

                    return false;
                }
                else
                {
                    Debug.LogError("Unknown Error");
                    status = null;
                    return false;
                }
            }
            catch (JsonException e)
            {
                // parse jobject error
                Debug.LogError(e.Message);
                status = null;
                return false;
            }

            // guardian error
            status = null;
            return false;
        }

        #endregion

        #region FinalReceiptRequest

        /// <summary>
        /// Check Chain receipt
        /// </summary>
        /// <param name="transactionHash"></param>
        /// <returns>ILLEGALRESPONSE,SUCCEED,REVERT,RETRY,TRANSACTIONNOTFOUND</returns>
        public async Task<string> RequestTransactionReceiptStatus(string transactionHash)
        {
            StarknetChainPostBodyStruct postBody = new StarknetChainPostBodyStruct()
            {
                method = "starknet_getTransactionReceipt",
                jsonrpc = "2.0",
                id = 0,
                Params = new
                {
                    transaction_hash = transactionHash
                },
            };

            var response =
                await HttpRequestController.Instance.SendHttpRequest(path: _chainBaseUrl, RequestType.POST, postBody);

            print("receipt result " + JsonConvert.SerializeObject(response.result));
            if (response.state == UnityWebRequest.Result.Success)
            {
                var status = CheckReceiptStatus(response.result, out var errReason);

                return status;
            }
            else
            {
                Debug.LogError("Some Internet Error Happens");
                return HTTPERROR;
            }
        }

        public string CheckReceiptStatus(string responseResult, out string errorMessage)
        {
            print(JsonConvert.SerializeObject(responseResult));
            var responseResultJObject = JObject.Parse(responseResult);
            var receiptDetail = responseResultJObject["result"];
            print("receipt detail " + receiptDetail);
            if (receiptDetail != null)
            {
                var executionStatus = receiptDetail["execution_status"];
                errorMessage = null;
                if (executionStatus != null)
                {
                    var executionStatusStr = executionStatus.ToString();
                    if (executionStatusStr == SUCCEEDED)
                    {
                    }
                    else if (executionStatusStr == REVERTED)
                    {
                        errorMessage = receiptDetail["revert_reason"]?.ToString();
                        Debug.LogError(errorMessage);
                    }

                    return executionStatusStr;
                }
            }

            if (responseResultJObject["error"] != null)
            {
                var errorStr = responseResultJObject["error"].ToString();
                errorMessage = errorStr;
                return TRANSACTIONNOTFOUND;
            }

            // json is not legal
            errorMessage = ILLEGALRESPONSE;
            return ILLEGALRESPONSE;
        }

        #endregion

        #region MakeTransactionOnChainHandler

        /// <summary>
        /// General Make Transaction handler
        /// </summary>
        /// <param name="transactionBody"></param>
        /// <param name="selfPublicKey"></param>
        /// <param name="selfPrivateKey"></param>
        /// <param name="selfContractAddress"></param>
        /// <param name="cairoVersion"></param>
        /// <param name="selector"></param>
        // public async void MakeTransactionOnChain(Dictionary<string, string> transactionBody, string selfPublicKey,
        //     string selfPrivateKey, string selfContractAddress, int cairoVersion,string selector)
        // {
        //     
        //     
        //     
        // }

        #endregion

        #region RequestNonceHandler

        public async Task<string> RequestNonce(string userContractAddress)
        {
            print("start request nonce");

            StarknetChainPostBodyStruct postBody = new StarknetChainPostBodyStruct()
            {
                method = "starknet_getNonce",
                jsonrpc = "2.0",
                id = 0,
                Params = new { contract_address = userContractAddress, block_id = "pending" },
            };

            GeneralRequestResult response = (await _httpRequestController.SendHttpRequest(
                _chainBaseUrl,
                RequestType.POST,
                postBody
            ));

            print(_chainBaseUrl);

            print(JsonConvert.SerializeObject(response));

            if (response.state == UnityWebRequest.Result.Success)
            {
                if (KatanaResultProcess(response.result, out JToken jToken))
                {
                    string nonce = jToken.Value<string>();
                    print("Get nonce success " + nonce);
                    return nonce;
                }
                else
                {
                    print("request nonce error");
                    JObject jObject = jToken.Value<JObject>();
                    return "error : " + jObject["code"] + jObject["message"];
                }
            }
            else
            {
                Debug.LogError("Some Internet Error Happens");
                return "Some Internet Error Happens";
            }
        }

        public bool KatanaResultProcess(string katanaPossibleResponse, out JToken res)
        {
            try
            {
                // 尝试解析JSON响应
                JObject jsonResponse = JObject.Parse(katanaPossibleResponse);

                // 检查响应结构
                if (jsonResponse["result"] != null)
                {
                    res = jsonResponse["result"];
                    return true;
                }
                else if (jsonResponse["error"] != null)
                {
                    // 这是第二种结构
                    // JObject errorObject = jsonResponse["error"] as JObject;
                    //print();
                    Debug.Log("This is error :" + jsonResponse["error"]);
                    res = jsonResponse["error"] as JObject;

                    return false;
                }
                else
                {
                    res = null;
                    return false;
                }
            }
            catch (JsonException)
            {
                res = null;
                return false;
            }
        }

        #endregion


        #region ChainUtils

        /// <summary>
        /// Multiple multiplier with factor
        /// </summary>
        /// <param name="overallFee"></param>
        /// <returns></returns>
        public string ConvertOverallFeeByMultiplierHexStr(string overallFee)
        {
            var overallBigInt = NumConverter.ConvertHexStrToBigIntVer2(overallFee);

            var finalFeeBigInt = NumConverter.MultiplyFloat(overallBigInt, _feeEstimateMultiplier);

            var finalFeeHexStr = NumConverter.ConvertBigIntToHexStr(finalFeeBigInt);

            return finalFeeHexStr;
        }

        static bool TryParseGasJson(string json, out List<GasData> gasDataList)
        {
            gasDataList = null;
            JObject jObject = JObject.Parse(json);

            try
            {
                if (jObject["result"] != null)
                {
                    if (jObject["result"] is JArray array && array.Count > 0)
                    {
                        gasDataList = JsonConvert.DeserializeObject<List<GasData>>(array.ToString());
                        return true;
                    }
                }

                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }


        public static IntPtr[] FormatStringArr(string[] input)
        {
            IntPtr[] stringPointers = new IntPtr[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                stringPointers[i] = Marshal.StringToHGlobalAnsi(input[i]);
            }

            return stringPointers;
        }

        // Note : The sigcall data parameters, the total length is not included,
        // the sigcalldata only include the params below the total params
        public Signatures GetSignature(string privateKey, string contractAddress, string to, string nonce,
            string maxFee,
            string methodName, List<string> calldata, int cairoVersion, int chainId = 5)
        {
            string[] callData = calldata.ToArray();
            callData = NumConverter.ConvertBigIntStrArrToHexStrArr(callData);
            var sigInput = new SignatureInput()
            {
                private_key = NumConverter.ConvertBigIntStrToHexStr(privateKey),
                self_contract_address = NumConverter.ConvertBigIntStrToHexStr(contractAddress),
                to = NumConverter.ConvertBigIntStrToHexStr(to),
                nonce = NumConverter.ConvertBigIntStrToHexStr(nonce),
                max_fee = maxFee,
            };

            var sendCallData = FormatStringArr(callData);

            var res = StarknetBridge.get_general_signature(sigInput, sendCallData, callData.Length, methodName,
                cairoVersion,
                chainId);

            for (int i = 0; i < sendCallData.Length; i++)
            {
                Marshal.FreeHGlobal(sendCallData[i]);
            }

            return res;
        }


        /// <summary>
        /// Check if the string is legal transaction hash
        /// Check null, check hex and check start with
        /// </summary>
        /// <param name="transactionHashStr">Must be hex string and start with ‘0x’</param>
        /// <returns></returns>
        public static bool IsLegalTransactionHash(string transactionHashStr)
        {
            if (string.IsNullOrEmpty(transactionHashStr))
            {
                return false;
            }

            return transactionHashStr.StartsWith("0x") && IsHexString(transactionHashStr);
        }

        static bool IsHexString(string input)
        {
            string trimmedHash = input.Substring(2);
            Regex hexRegex = new Regex(@"[0-9A-Fa-f]+$");
            return hexRegex.IsMatch(trimmedHash);
        }

        public string FormatBigIntStrToHexStr(string bigIntStr)
        {
            if (bigIntStr.ToLower().StartsWith("0x"))
            {
                return bigIntStr.ToLower();
            }
            else
            {
                BigInteger bigint = BigInteger.Parse(bigIntStr);

                return ("0x" + bigint.ToString("X")).ToLower();
            }
        }

        public List<string> ConvertCallDataItemIntoHex(List<string> callData)
        {
            var res = new List<string>();
            foreach (var item in callData)
            {
                res.Add(NumConverter.ConvertBigIntStrToHexStr(item));
            }

            return res;
        }

        /// <summary>
        /// Based on the cairo version to add specific prefix
        /// </summary>
        /// <param name="cairoVersion"></param>
        /// <param name="to"></param>
        /// <param name="selectorHex"></param>
        /// <param name="sigCallDataCount"></param>
        /// <returns></returns>
        public static List<string> ConstructPostCallDataPrefix(int cairoVersion, string to, string selectorHex,
            string sigCallDataCount)
        {
            List<string> postCallDataPrefix = new();
            switch (cairoVersion)
            {
                case 0:
                    //Cairo 0
                    postCallDataPrefix = new()
                    {
                        "0x1",
                        to, // to contract = action contract
                        selectorHex, // entry hex
                        "0x0", // Fix
                        sigCallDataCount, // total count
                        sigCallDataCount, // total count
                    };
                    break;
                default:
                    //Cairo 1
                    postCallDataPrefix = new()
                    {
                        "0x1",
                        to, // to contract = action contract
                        selectorHex, // entry hex
                        sigCallDataCount, // total count
                    };
                    break;
            }

            return postCallDataPrefix;
        }

        #endregion
    }


    public class StarknetChainPostBodyStruct
    {
        public string method;
        public string jsonrpc;
        [JsonProperty("params")] public object Params;
        public int id;
    }


    public struct GasData
    {
        public string gas_consumed;

        public string gas_price;

        public string data_gas_consumed;

        public string data_gas_price;

        public string overall_fee;

        public string unit;
    }

    public class ChainTransactionStatus
    {
        public string receiptStatus;
        public string transactionHash;
        public string description;
        public Action<bool> successCallback;
        public Action<bool> failCallback;

        /// <summary>
        /// 如果正在请求
        /// </summary>
        public bool running = true;
    }

    public class TransactionRequestParams
    {
        public string sender_address;
        public object calldata;
        public string type;
        public string max_fee;
        public string version;
        public List<string> signature;
        public string nonce;
    }

    public enum TransactionExecutionStatus
    {
        // Starknet official use execution 
        Reverted,
        Succeeded,

        // starknet official use finality
        NotReceived,
        Received,
        Rejected,
        AcceptedOnL2,
        AcceptedOnL1,
        NotFound,

        HttpError,
        IllegalTransactionHash,
        Unknown,
    }

    [Serializable]
    public class StarknetDeployAccountBody
    {
        public string type;
        public string contract_address_salt;
        public string[] constructor_calldata;
        public string class_hash;
        public string max_fee;
        public string version;
        public string nonce;
        public string[] signature;
    }
}