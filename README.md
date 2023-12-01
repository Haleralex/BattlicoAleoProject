
# Battlico + ALEO

A brief description of what this project does and who it's for:
The project was created to combine the Batliko project with the ALEO crypto project


1) Special token in testnet3 (Aleo testnet) was created - battlico_token.aleo
2) Token was minted - 1000000000 tokens (1000000000000000u64)
3) SnarkOS was installed on server
4) Created shell scripts:
    a) transfer
    b) balance getter
    c) new account creator
5) Created C# wrapper for shell commands executor
6) The wrapper used with CryproOperationPerformer on server

The following part explains how to use

1) On client we install game-app
2) In game we enter menu -> PursePanel
3) When we entered in PursePanel - execute checking on funds existing on user's crypto-wallet 
4) On PursePanel we can execute WithDrawTokens method with receiver address and amount of tokens
5) All methods from client sending to server when CryproOperationPerformer handle request
