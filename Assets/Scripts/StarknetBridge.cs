using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public static class StarknetBridge
{
    /// <summary>
    /// This method auto generate private key and public key
    /// </summary>
    /// <returns>Bigint String</returns>
    [DllImport("starknet_bridge")]
    public static extern KeyPair generate_key_pair();

    /// <summary>
    /// This method to generate user contract address, 
    /// for now, you should put the generated public key to generate relative contract address
    /// </summary>
    /// <param name="public_key">It must be hex, like "0xsdkhf54iu45..."</param>
    /// <returns>Bigint string</returns>
    [DllImport("starknet_bridge")]
    public static extern string get_contract_address(string public_key);

    /// <summary>
    /// This method is to get the signaturse for deploy the new account on Starknet test chain
    /// </summary>
    /// <param name="input">The relative fields like public key and private key must be lower case hex string, like "0xsdkhf54iu45...",
    /// more info please check example
    /// <returns>Signatures</returns>
    [DllImport("starknet_bridge")]
    public static extern Signatures get_deploy_account_signature(SignatureAccountDeployInput input);

    /// <summary>
    /// This method is to get the signatures for make transaction
    /// </summary>
    /// <param name="input">The relative fields like public key and private key must be lower case hex string, like "0xsdkhf54iu45...",
    /// more info please check example
    /// </param>
    /// <returns>Signatures</returns>
    [DllImport("starknet_bridge")]
    public static extern Signatures get_general_signature(SignatureInput input, IntPtr[] strings, int count, string selector);


}
