"use strict";

// see https://docs.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-3.1

document.getElementById("executeButton").disabled = true;

var connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5001/demr")
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

connection.on("InvokeBinarySucceeded", (exitCode, stdOut, stdErr, transactionId) => {
    console.log('invokesucceeded');
    document.getElementById("exitCode").innerText = exitCode;
    document.getElementById("stdOut").innerText = stdOut;
    document.getElementById("stdErr").innerText = stdErr;
    document.getElementById("transactionId").innerText = transactionId;
});

connection.on("InvokeBinaryExceptioned", (exceptionMessage, transactionId) => {
    document.getElementById("exceptionMessage").innerText = exceptionMessage;
    document.getElementById("transactionId").innerText = transactionId;
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
    document.getElementById("transactionId").innerText = "";

    var transactionId = (Math.floor(Math.random() * (100000 - 0))).toString();
    console.log("Calling program transactionId " + transactionId);
    connection.invoke("InvokeBinaryAsync", "C:\\Users\\rm\\source\\repos\\DemrService\\publish\\TestConsoleApp.exe", "john Doe 1/1/2011", transactionId).then(function () {
        console.info('then');
    }).catch(function (err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});

start();


function reqListener() {
    console.log(this.responseText);
}

var oReq = new XMLHttpRequest();
oReq.addEventListener("load", reqListener);
var path = 'c:\\program files\\cool\\foo.exe';
oReq.open("GET", encodeURI("https://localhost:5001/api/RetrieveFile/test?path=" + path + "&isdir=false"));
oReq.send();



var saveData = (function () {
    var a = document.createElement("a");
    document.body.appendChild(a);
    a.style = "display: none";
    return function (blob, fileName) {
        var url = window.URL.createObjectURL(blob);
        a.href = url;
        a.download = fileName;
        a.click();
        window.URL.revokeObjectURL(url);
    };
}());

function listener() {
    var blob = this.response;
    saveData(blob, "foo.zip");
}

var oReq = new XMLHttpRequest();
oReq.responseType = "blob";
oReq.addEventListener("load", listener);
//var path = 'c:\\users\\rm\\Downloads\\ubuntu-16.04.3-desktop-amd64.iso';
//oReq.open("GET", encodeURI("https://localhost:5001/api/RetrieveFile/?path=" + path + "&isdir=false"));
var path = 'c:\\users\\rm\\Downloads\\x\\';
oReq.open("GET", encodeURI("https://localhost:5001/api/RetrieveFile/?path=" + path + "&isdir=true"));
oReq.send();