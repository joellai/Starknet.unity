using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public class GetNewAccountSignatures : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        string low_case_public_key = "0x52EC4743A9294ECC9CBDD2A2F927403BFC8A4A68BBF8B5C38E547A33482605E";
        string low_case_private_key = "0x43A3E7CF3AB6C69318DD93FF366E9E220D4F0F6F3BA3FA3AF033D994843E07A";
        //The privatekey and public key must be hex

        var sigInput = new SignatureAccountDeployInput()
        {
            private_key = low_case_private_key,
            public_key = low_case_public_key,
            nonce = "0",
            max_fee = "68167000749830",
            salt = low_case_public_key

        };

        Signatures sigs = StarknetBridge.get_deploy_account_signature(sigInput);

        print("r signature " + sigs.r_sig + " s signature " + sigs.s_sig);
    }

}
