using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Utils;

public class GetGeneralSignatures : MonoBehaviour
{
    private float timeoutDuration = 5f;

    public void Start()
    {
        string privateKey = "YOUR PRIVATE KEY";
        string contractAddress = "YOUR CONTRACT ADDRESS";
        string to = "TARGET CONTRACT ADDRESS";
        string nonce = "NONCE";
        string selector = "ENTRY NAME";
        // Note the total length is not included in the calldata
        List<string> callData = new() { "YOUR CALL DATA" };
        string max_fee = "MAX_FEE";
        Signatures sigs = GetSignature(privateKey: privateKey,
            contractAddress: contractAddress,
            to: to,
            nonce: nonce,
            methodName: selector,
            calldata: callData,
            maxFee: max_fee);

        Debug.Log(sigs.s_sig);
        Debug.Log(sigs.r_sig);

        // The MakeTransaction method is to make the normal transaction to chain, the implementation is as simple as possible
        // so you may want to implement wit your own http request version
        // MakeTransaction();
    }

    /// <summary>
    /// This method calculate the calldata signatures for calldata
    /// </summary>
    /// <returns></returns>
    public Signatures GetSignature(string privateKey, string contractAddress, string to, string nonce, string maxFee,
        string methodName, List<string> calldata)
    {
        string[] callData = calldata.ToArray();

        callData = NumConverter.ConvertBigIntStrArrToHexStrArr(callData);

        var sigInput = new SignatureInput()
        {
            private_key = NumConverter.ConvertBigIntStrToHexStr(privateKey),
            self_contract_address = NumConverter.ConvertBigIntStrToHexStr(contractAddress),
            to = NumConverter.ConvertBigIntStrToHexStr(to),
            nounce = NumConverter.ConvertBigIntStrToHexStr(nonce),
            max_fee = maxFee,
            high = "123"
        };

        var sendCallData = ProcessStrArr(callData);

        var res = StarknetBridge.get_general_signature(sigInput, sendCallData, callData.Length, methodName);

        //var res = RustBridge.get_signature(sigInput);
        print("r= " + res.r_sig + " s= " + res.s_sig);

        for (int i = 0; i < sendCallData.Length; i++)
        {
            Marshal.FreeHGlobal(sendCallData[i]);
        }

        return res;
    }

    public static IntPtr[] ProcessStrArr(string[] input)
    {
        //string[] inputStrings = { "Hello, ", "world!", " This is a test." };

        IntPtr[] stringPointers = new IntPtr[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            stringPointers[i] = Marshal.StringToHGlobalAnsi(input[i]);
        }

        return stringPointers;
    }

    public async Task<string> MakeTransaction(string to, string selectorHex, string contractAddress,
        List<string> sigCallData, Signatures sigs, string nonce, string max_fee)
    {
        List<string> postBody = new()
        {
            "1",
            to,
            selectorHex,
            sigCallData.Count.ToString(),
        };
        postBody.AddRange(sigCallData);

        //print(JsonConvert.SerializeObject(postBody));

        object makeTransactionBody = new
        {
            type = "INVOKE_FUNCTION",
            sender_address = contractAddress,
            calldata = postBody.ToArray(),
            signature = new string[] { sigs.r_sig, sigs.s_sig },
            nonce = nonce,
            max_fee = max_fee,
            version = "0x1"
        };
        print("start transaction");
        string addTransactionUrl = "https://alpha4.starknet.io/gateway/add_transaction";

        var requestRes = await SendHttpRequest(path: addTransactionUrl, type: RequestType.POST, postBody);

        print(requestRes.state);

        // Network error or backend no response
        if (requestRes.state != UnityWebRequest.Result.Success)
        {
            return "error" + requestRes.result;
        }

        var res = JsonConvert.DeserializeObject<MakeTransactionResponse>(requestRes.result);

        if (res.code == "TRANSACTION_RECEIVED" && res.transaction_hash != null)
        {
            print("return transaction hash");
            // Return trasaction hash
            return res.transaction_hash;
        }
        else
        {
            // There is error
            print("return error message");
            return "error : " + res.message;
        }
    }

    public async Task<GeneralRequestResult> SendHttpRequest(
        string path,
        RequestType type = RequestType.GET,
        object postData = null,
        Dictionary<string, string> headersFields = null
    )
    {
        UnityWebRequest request = new();


        switch (type)
        {
            case RequestType.GET:
                request = CreateRequest(path, RequestType.GET, postData, headersFields);
                break;
            case RequestType.POST:
                request = CreateRequest(path, RequestType.POST, postData, headersFields);
                break;
        }
        //var postRequest = CreateRequest(path, RequestType.POST, postData);


        await request.SendWebRequest();


        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log(request.downloadHandler.text);
            //DebugManager.Instance.LogInfo(request.downloadHandler.text);
            return new GeneralRequestResult() { state = request.result, result = request.downloadHandler.text };
        }
        else
        {
            print(request.downloadHandler.text);
            //DebugManager.Instance.LogError(request.downloadHandler.text);

            return new GeneralRequestResult() { state = request.result, result = request.downloadHandler.text };
        }
    }

    private UnityWebRequest CreateRequest(
        string path,
        RequestType type = RequestType.GET,
        object data = null,
        Dictionary<string, string> headersFields = null
    )
    {
        print("i am create request");
        var request = new UnityWebRequest(path, type.ToString());


        if (data != null)
        {
            var bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        }

        request.downloadHandler = new DownloadHandlerBuffer();


        request.SetRequestHeader("Content-Type", "application/json");


        if (headersFields != null)
        {
            foreach (var field in headersFields)
            {
                request.SetRequestHeader(field.Key, field.Value);
            }
        }


        request.timeout = (int)(timeoutDuration);


        return request;
    }
}

public enum RequestType
{
    GET = 0,
    POST = 1,
    PUT = 2,
    DELETE = 3
}

public class GeneralRequestResult
{
    public UnityWebRequest.Result state;
    public string result;
}

[Serializable]
public class MakeTransactionResponse
{
    public string code;
    public string data;
    public string message;
    public string transaction_hash;
}