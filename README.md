# dentalemr-ws-service
A websocket (SignalR) Windows service


# DemrService
A Windows Service / Console app that hosts a Kestrel websocket (SignalR) server:

## Binary Invoker

- Endpoint: `http://127.0.0.1:5000/demr`

- CORS Setting (https://github.com/DentalEMR/dentalemr-ws-service/blob/master/DemrService/Startup.cs#L35): `https://localhost:44384`
(Note: cors setting set for TestWebApp (below). For production use change to origin of client hosting signalr client library.)


Server method: `BinaryExecuter(binaryPath, binaryArgs)` that executes the binary at `binarPath` with `binaryArgs` and
calls one of the two following client methods once the binary has completed execution:
1. InvokeBinarySucceeded (exitCode, binaryStdOut, binaryStdErr)
2. InvokeBinaryExceptioned(ex.Message)

# Test applications

## TestConsoleApp 
A simple console app that accepts arguments and outputs a string including arguments to stdout.

## TestWebApp
An example SignalR websocket client implementation that reliably reconnects.

# Copyright 
DentalEMR, Inc. All rights reserved.
