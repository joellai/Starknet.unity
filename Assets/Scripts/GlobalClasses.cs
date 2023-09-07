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
