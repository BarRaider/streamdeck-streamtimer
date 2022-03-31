document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    showHideSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            showHideSettings(payload.settings);
        }
    });
});

function showHideSettings(payload) {
    console.log("Show Hide Settings Called");
    setHourglass("none");
    setSoundOnEndSettings("none");

    // Date/Time
    showHideTimeOnly("none");
    showHideDateTime("");
    if (payload['countdownTimeOnly']) {
        showHideTimeOnly("");
        showHideDateTime("none");
    }

    if (payload['hourglassMode']) {
        setHourglass("");
    }
    if (payload['playSoundOnEnd']) {
        setSoundOnEndSettings("");
    }
}

function setHourglass(displayValue) {
    var dvHourglassSettings = document.getElementById('dvHourglassSettings');
    dvHourglassSettings.style.display = displayValue;
}

function setSoundOnEndSettings(displayValue) {
    var dvSoundOnEndSettings = document.getElementById('dvSoundOnEndSettings');
    dvSoundOnEndSettings.style.display = displayValue;
}

function showHideTimeOnly(displayValue) {
    console.log("showHideTimeOnly Called", displayValue);
    var dvTimeOnly = document.getElementById('dvTimeOnly');
    dvTimeOnly.style.display = displayValue;
}

function showHideDateTime(displayValue) {
    console.log("showHideDateTime Called", displayValue);
    var dvDateTime = document.getElementById('dvDateTime');
    dvDateTime.style.display = displayValue;
}