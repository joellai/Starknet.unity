<p align="center">
  <img src="/Documentation/Image/logo.png" alt="Starknet.unity logo" width="200px"/>
</p>

# Starknet.unity

This project is a package for unity, it provides the CSharp with the Starknet interaction capability .

- Easy to use like Starknet.js
- Direct interaction with your Starknet contract
- Multiple platforms supported

## Installation

1. Download the latest from release
2. Import the package file (.unitypackage) from `Asset -> Import Package` in unity editor
3. Now the Starknet.unity package is in your Assets

**Note: Most time the release page may not be updated timely, please check the source code for more detail.**

## How to use

You can check the sample scripts in `Scripts -> Examples`

In any script, use a static class `StarknetBridge` to access the required methods. Currently, two methods, generate_key_pair, and get_contract_address, have been implemented.

- `generate_key_pair() -> KeyPair`

  > Generate the public key and private key

- `get_contract_address(string publickey) -> BigInt String`

  > Generate the contract address with the relative public key

- `get_deploy_account_signature(SignatureAccountDeployInput input) -> Signatures`

  > Generate signatures for deployment of new account

## Updating Road Map

| Milestone                       | Detail                                                                        | Status |
| ------------------------------- | ----------------------------------------------------------------------------- | ------ |
| Basic Account Created           | Generate the key pair and relative contract address                           | ✔️     |
| Generate Deploy Signatures      | Create the relative signature for deploying new account                       | ✔️     |
| Support the new ARGENT protocal | Update the new call data structure (**Sep/18/2023**) to correct the signature | ✔️     |
| Android Support                 | Support the Android Build                                                     | ✔️     |
| Deploy the Account on Chain     | Use the csharp script to deploy the created account                           | ✔️     |
| Generate Transaction signatures | Generate transaction signatures for calling contract                          | ✔️     |
| Relative RPC handlers           | Make the request from CSharp side                                             | ✔️     |
| Example Added                   | The example for make transaction                                              | ✔️     |
| IOS Support                     | Support the IOS Build                                                         |  ✔️    |

## Contributors

|                                          | Detail                                         |
| ---------------------------------------- | ---------------------------------------------- |
| [@iLAYER](https://github.com/iLAYER-ORG) | The organizer                                  |
| [@Joel Lai](https://github.com/joellai)  | Unity script coding and overwrite Starknet API |
| [@Bill Lee](https://github.com/tgyf007)  | Starknet RPC request and response digging      |

## License

Licensed under the [MIT license](https://github.com/joellai/Starknet.unity/blob/main/LICENSE).
