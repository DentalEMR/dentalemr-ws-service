# dentalemr-ws-service
A websocket (SignalR) Windows service


# DemrService
A Windows Service / Console app that hosts a Kestrel websocket (SignalR) server:

## Binary Invoker

Endpoint: `/demr`

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
