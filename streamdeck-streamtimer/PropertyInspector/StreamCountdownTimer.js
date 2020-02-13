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
    setSoundOnEndSettings("none");
    if (payload['hourglassMode']) {
        setHourglass("");
    }

    if (payload['streamathonMode']) {
        setStreamathon("");
    }

    if (payload['playSoundOnEnd']) {
        setSoundOnEndSettings("");
    }
}

function setHourglass(displayValue) {
    var dvHourglassSettings = document.getElementById('dvHourglassSettings');
    dvHourglassSettings.style.display = displayValue;
}

function setStreamathon(displayValue) {
    var dvStreamathonIncrement = document.getElementById('dvStreamathonIncrement');
    var dvStreamathonMessage = document.getElementById('dvStreamathonMessage');
    dvStreamathonIncrement.style.display = displayValue;
    dvStreamathonMessage.style.display = displayValue;
}

function setSoundOnEndSettings(displayValue) {
    var dvSoundOnEndSettings = document.getElementById('dvSoundOnEndSettings');
    dvSoundOnEndSettings.style.display = displayValue;
}

function openSaveFilePicker(title, filter, propertyName) {
    console.log("openSaveFilePicker called: ", title, filter, propertyName);
    var payload = {};
    payload.property_inspector = 'loadsavepicker';
    payload.picker_title = title;
    payload.picker_filter = filter;
    payload.property_name = propertyName;
    sendPayloadToPlugin(payload);
}
