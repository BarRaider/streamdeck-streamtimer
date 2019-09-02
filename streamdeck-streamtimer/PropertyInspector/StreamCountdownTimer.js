document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    showHideSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            var payload = jsonObj.payload;
            showHideSettings(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            showHideSettings(payload.settings);
        }
    });
});

function showHideSettings(payload) {
    console.log("Show Hide Settings Called");
    setHourglass("none");
    setStreamathon("none");
    if (payload['hourglassMode']) {
        setHourglass("");
    }

    if (payload['streamathonMode']) {
        setStreamathon("");
    }
}

function setHourglass(displayValue) {
    var dvHourglassColor = document.getElementById('dvHourglassColor');
    var dvHourglassMessage = document.getElementById('dvHourglassMessage');
    dvHourglassColor.style.display = displayValue;
    dvHourglassMessage.style.display = displayValue;
}

function setStreamathon(displayValue) {
    var dvStreamathonIncrement = document.getElementById('dvStreamathonIncrement');
    var dvStreamathonMessage = document.getElementById('dvStreamathonMessage');
    dvStreamathonIncrement.style.display = displayValue;
    dvStreamathonMessage.style.display = displayValue;
}