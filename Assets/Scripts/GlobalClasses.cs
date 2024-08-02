using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


[StructLayout(LayoutKind.Sequential)]
public struct KeyPair
{
    public string public_key;
    public string private_key;

    public KeyPair(string pub, string priv)
    {
        this.public_key = pub;
        this.private_key = priv;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Signatures
{
    public string r_sig;
    public string s_sig;

    public Signatures(string r_sig, string s_sig)
    {
        this.r_sig = r_sig;
        this.s_sig = s_sig;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SignatureDeployInput
{
    public string private_key;
    public string public_key;
    public string nonce;
    public string max_fee;
    public string salt;
}

[StructLayout(LayoutKind.Sequential)]
public struct SignatureInput
{
    public string private_key;
    public string self_contract_address;
    public string to;
    public string nonce;
    public string max_fee;
    
}


[StructLayout(LayoutKind.Sequential)]
public struct SignatureAccountDeployInput
{
    public string private_key;
    public string public_key;
    public string nonce;
    public string max_fee;
    public string salt;
}