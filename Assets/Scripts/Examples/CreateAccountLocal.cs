using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

public class CreateAccountLocal : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var keypair = StarknetBridge.generate_key_pair();
        var contractAddress = StarknetBridge.get_contract_address(FormatBigIntStrToHexStr(keypair.public_key));

        print("public key is " + FormatBigIntStrToHexStr(keypair.public_key));
        print("private key is " + FormatBigIntStrToHexStr(keypair.private_key));
        print("contract address " + contractAddress);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// This method convert the bigInt string into Hex string
    /// </summary>
    /// <returns>Example "0x89ISd6565f..."</returns>
    public string FormatBigIntStrToHexStr(string bigIntStr)
    {
        if (bigIntStr.StartsWith("0x"))
        {
            return bigIntStr;
        }
        else
        {
            BigInteger bigint = BigInteger.Parse(bigIntStr);

            return "0x" + bigint.ToString("X");
        }

    }
}
