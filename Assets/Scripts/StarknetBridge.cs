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
    /// This M
    /// </summary>
    /// <param name="public_key">It must be hex, like "0xsdkhf54iu45..."</param>
    /// <returns>Example "0x89ISd6565f..."</returns>
    [DllImport("starknet_bridge")]
    public static extern string get_contract_address(string public_key);

}
