"use strict";

// see https://docs.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-3.1

document.getElementById("executeButton").disabled = true;

var connection = new signalR.HubConnectionBuilder()
    .withUrl("http://127.0.0.1:5000/demr")
    .configureLogging(signalR.LogLevel.Information)
    .withAutomaticReconnect()
    .build();

connection.onreconnecting((error) => {
    console.assert(connection.state === signalR.HubConnectionState.Reconnecting);

    document.getElementById("executeButton").disabled = true;
    console.error(`RECONNECTING: Connection lost due to error "${error}".`);
});

connection.onreconnected((connectionId) => {
    console.assert(connection.state === signalR.HubConnectionState.Connected);

    document.getElementById("executeButton").disabled = false;
    console.error(`RECONNECTED: Connected with connectionId "${connectionId}".`);
});

connection.onclose((error) => {
    console.assert(connection.state === signalR.HubConnectionState.Disconnected);

    document.getElementById("executeButton").disabled = true;
    console.log(`CLOSED due to error "${error}". Will attempt to start again in 10000.`);

    setTimeout(() => start(), 10000);
});

connection.on("InvokeBinarySucceeded", (exitCode, stdOut, stdErr) => {
    document.getElementById("exitCode").innerText = exitCode;
    document.getElementById("stdOut").innerText = stdOut;
    document.getElementById("stdErr").innerText = stdErr;
});

connection.on("InvokeBinaryExceptioned", (exceptionMessage) => {
    document.getElementById("exceptionMessage").innerText = exceptionMessage;
});

async function start() {
    try {
        await connection.start();
        console.assert(connection.state === signalR.HubConnectionState.Connected);
        console.log("STARTED successful.");
        document.getElementById("executeButton").disabled = false;
    } catch (error) {
        console.assert(connection.state === signalR.HubConnectionState.Disconnected);
        console.log(`STARTED failed: "${error}". Will attept to START again in 10000ms.`);
        setTimeout(() => start(), 10000);
    }
};

document.getElementById("executeButton").addEventListener("click", function (event) {
    document.getElementById("exitCode").innerText = '';
    document.getElementById("stdOut").innerText = '';
    document.getElementById("stdErr").innerText = '';

    connection.invoke("InvokeBinary", "C:\\Users\\rm\\source\\repos\\DemrService\\publish\\TestConsoleApp.exe", "john Doe 1/1/2011").catch(function (err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});

start();