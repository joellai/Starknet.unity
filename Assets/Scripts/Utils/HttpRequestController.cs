using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(-10)]
public class HttpRequestController : MonoBehaviour
{
    private float timeoutDuration = 5f;

    public readonly string getNonceUrl =
        "https://alpha4.starknet.io/feeder_gateway/get_nonce?contractAddress=0x033f3c75cb6c3558085173d4494540e4cdb2f0d2e19c4e168f7bcea0e7d20b73&blockNumber=pending";
    public static HttpRequestController Instance { get; private set; }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }


    private class UnityWebRequestException : System.Exception
    {
        public UnityWebRequestException(string message)
            : base(message)
        {
        }
    }

    public async Task<GeneralRequestResult> SendHttpRequest(
        string path,
        RequestType type = RequestType.GET,
        object postData = null,
        Dictionary<string, string> headersFields = null,
        float timeout = 5
    )
    {
        UnityWebRequest request = new();


        switch (type)
        {
            case RequestType.GET:
                request = CreateRequest(path, RequestType.GET, postData, headersFields, timeout);
                break;
            case RequestType.POST:
                request = CreateRequest(path, RequestType.POST, postData, headersFields, timeout);
                break;
        }

        //var postRequest = CreateRequest(path, RequestType.POST, postData);
        using (request)
        {
            try
            {
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
#if UNITY_EDITOR
                    // Debug.Log(request.downloadHandler.text);
#endif
                    return new GeneralRequestResult() { state = request.result, result = request.downloadHandler.text };
                }
                else
                {
#if UNITY_EDITOR
                    // print(request.downloadHandler.text);
#endif

                    return new GeneralRequestResult() { state = request.result, result = request.downloadHandler.text };
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new GeneralRequestResult() { state = request.result, result = request.downloadHandler.text };
            }
        }
    }

    private UnityWebRequest CreateRequest(
        string path,
        RequestType type = RequestType.GET,
        object data = null,
        Dictionary<string, string> headersFields = null,
        float timeout = 5f
    )
    {
        // print("i am create request");
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


        request.timeout = (int)(timeout);


        return request;
    }

    public IEnumerator SendHttpRequestCoroutine(string path, RequestType type = RequestType.GET, object postData = null,
        Dictionary<string, string> headersFields = null, Action<GeneralRequestResult> onComplete = null,
        Action<GeneralRequestResult> onFailComplete = null)
    {
        UnityWebRequest request;

        switch (type)
        {
            case RequestType.GET:
                request = CreateRequest(path, RequestType.GET, postData, headersFields);
                break;
            case RequestType.POST:
                request = CreateRequest(path, RequestType.POST, postData, headersFields);
                break;
            default:
                yield break;
        }

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // Debug.Log(request.downloadHandler.text);
            onComplete?.Invoke(new GeneralRequestResult()
                { state = request.result, result = request.downloadHandler.text });
        }
        else
        {
            Debug.LogError(request.error);
            onFailComplete?.Invoke(new GeneralRequestResult() { state = request.result, result = request.error });
        }
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
