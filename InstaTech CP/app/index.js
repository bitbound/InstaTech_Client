window.onerror = function (message, source, lineno, colno, error) {
    var fs = require("fs");
    var os = require("os");
    fs.appendFile(os.tmpdir() + "/InstaTech_CP_Errors.txt", new Date().toString() + "\t" +  message + "\r\n");
    // This is required to ignore random Electron renderer error.
    if (capturing && useWebSocket) {
        worker.webContents.executeJavaScript("getCapture()");
        return true;
    }
};

// ***  Config: Change these variables for your environment.  *** //
// global.debug is set in main.js.

// Websocket protocol.
var wsProtocol;
// HTTP protocol.
var httpProtocol;
// Server host name.
var hostname;
// Websocket service path.
var wsPath;
// File transfer service path.
var ftPath;
if (require("electron").remote.getGlobal("debug")) {
    wsProtocol = "ws://";
    httpProtocol = "http://";
    hostname = "localhost:52422";
    wsPath = "/Services/Remote_Control_Socket.cshtml";
    ftPath = "/Services/FileTransfer.cshtml";
} else {
    wsProtocol = "wss://";
    httpProtocol = "https://";
    hostname = "instatech.org";
    wsPath = "/Demo/Services/Remote_Control_Socket.cshtml";
    ftPath = "/Demo/Services/FileTransfer.cshtml";
}

const robot = require("robotjs");
const electron = require('electron');
const fs = require("fs");
const os = require("os");
const buf = require("buffer");
var win = electron.remote.getCurrentWindow();
var worker = electron.remote.BrowserWindow.fromId(2);
var rtcConnection;
var fr = new FileReader();
var socket;
var capturing = false;
var totalWidth = 0;
var totalHeight = 0;
var lastMouseMove;
var videoQuality = .75;
var useWebSocket = false;

// Offsets are the left and top edge of the screen, in case multiple monitor setups
// create a situation where the edge of a monitor is in the negative.  This must
// be converted to a 0-based max left/top to render images on the canvas properly.
var offsetX = 0;
var offsetY = 0;
function openWebSocket() {
    socket = new WebSocket(wsProtocol + hostname + wsPath);
    socket.onopen = function (e) {
        var request = {
            "Type": "ConnectionType",
            "ConnectionType": "ClientApp"
        };
        socket.send(JSON.stringify(request));
        initRTC();
    };
    socket.onclose = function (e) {
        capturing = false;
        if (rtcConnection.signalingState == "stable") {
            rtcConnection.close();
        }
        $("#inputAgentStatus").val("Not Connected");
        $("#inputAgentStatus").css("color", "black");
        $("#sectionMain").hide();
        $("#sectionNewSession").show();
    };
    socket.onerror = function (e) {
        capturing = false;
        if (rtcConnection.signalingState == "stable") {
            rtcConnection.close();
        }
        $("#sectionMain").hide();
        $("#sectionNewSession").show();
        console.log(e);
    };
    socket.onmessage = function (e) {
        var jsonMessage = JSON.parse(e.data);
        switch (jsonMessage.Type) {
            case "SessionID":
                $("#inputSessionID").val(jsonMessage.SessionID);
                break;
            case "CaptureScreen":
                if (jsonMessage.Source == "WebSocket") {
                    useWebSocket = true;
                }
                beginScreenCapture();
                break;
            case "RTCOffer":
                if (useWebSocket) {
                    var request = {
                        "Type": "RTCOffer",
                        "Status": "Denied"
                    };
                    socket.send(JSON.stringify(request));
                    return;
                }
                var offer = JSON.parse(atob(jsonMessage.Offer));
                rtcConnection.setRemoteDescription(offer, function () {
                    rtcConnection.createAnswer(function (answer) {
                        rtcConnection.setLocalDescription(answer, function () {
                            var request = {
                                "Type": "RTCAnswer",
                                "Answer": btoa(JSON.stringify(rtcConnection.localDescription)),
                            };
                            socket.send(JSON.stringify(request));
                        }, function () {
                            // Failure callback.
                            useWebSocket = true;
                            beginScreenCapture();
                        });
                    }, function (error) {
                        // Failure callback.
                        useWebSocket = true;
                        beginScreenCapture();
                    });
                }, function (error) {
                    // Failure callback.
                    useWebSocket = true;
                    beginScreenCapture();
                });
                break;
            case "RTCCandidate":
                if (rtcConnection != undefined) {
                    rtcConnection.addIceCandidate(new RTCIceCandidate(jsonMessage.Candidate));
                }
                break;
            case "RefreshScreen":
                sendBounds();
                worker.webContents.executeJavaScript("sendFullScreenshot = true;");
                break;
            case "FileTransfer":
                if (!fs.existsSync(os.tmpdir() + "\\InstaTech\\")) {
                    fs.mkdirSync(os.tmpdir() + "\\InstaTech\\");
                };
                var strPath = httpProtocol + hostname + ftPath;
                var retrievalCode = jsonMessage.RetrievalCode;
                var request = {
                    "Type": "Download",
                    "RetrievalCode": retrievalCode
                };
                $.post(strPath, JSON.stringify(request), function (data) {
                    fs.writeFileSync(os.tmpdir() + "\\InstaTech\\" + jsonMessage.FileName, buf.Buffer.from(data, "base64"));
                    $("#inputFilesTransferred").val(fs.readdirSync(os.tmpdir() + "\\InstaTech\\").length);
                    showTooltip($("#inputFilesTransferred"), "left", "black", "File downloaded.");
                });
                break;
            case "SendClipboard":
                electron.clipboard.writeText(atob(jsonMessage.Data));
                showTooltip($("#inputFilesTransferred"), "left", "black", "Clipboard data set.");
                break;
            case "ChangeImageQuality":
                worker.webContents.executeJavaScript('jpegQuality = ' + jsonMessage.Value);
                break;
            case "ChangeResolution":
                worker.webContents.executeJavaScript('videoQuality = ' + jsonMessage.Value);
                videoQuality = Number(jsonMessage.Value);
                rtcConnection.removeStream(rtcConnection.getLocalStreams()[0]);
                addRTCMedia();
                break;
            case "MouseMove":
                if (Date.now() - lastMouseMove < 500) {
                    return;
                }
                robot.moveMouse(Math.round(jsonMessage.PointX * totalWidth + offsetX), Math.round(jsonMessage.PointY * totalHeight + offsetY));
                lastMouseMove = Date.now();
                break;
            case "MouseDown":
                robot.moveMouse(Math.round(jsonMessage.PointX * totalWidth + offsetX), Math.round(jsonMessage.PointY * totalHeight + offsetY));
                if (jsonMessage.Button == "Left") {
                    robot.mouseToggle("down", "left");
                }
                else if (jsonMessage.Button == "Right") {
                    robot.mouseToggle("down", "right");
                }
                break;
            case "MouseUp":
                robot.moveMouse(Math.round(jsonMessage.PointX * totalWidth + offsetX), Math.round(jsonMessage.PointY * totalHeight + offsetY));
                if (jsonMessage.Button == "Left") {
                    robot.mouseToggle("up", "left");
                }
                else if (jsonMessage.Button == "Right") {
                    robot.mouseToggle("up", "right");
                }
                break;
            case "TouchMove":
                var mousePos = robot.getMousePos();
                robot.moveMouse(Math.round(jsonMessage.MoveByX * totalWidth + mousePos.x), Math.round(jsonMessage.MoveByY * totalWidth + mousePos.y));
                break;
            case "Tap":
                robot.mouseClick();
                break;
            case "TouchDown":
                robot.mouseToggle("down", "left");
                break;
            case "LongPress":
                robot.mouseClick("right");
                break;
            case "TouchUp":
                robot.mouseToggle("up", "left");
                break;
            case "KeyPress":
                try {
                    var baseKey = jsonMessage.Key;
                    var modifiers = [];
                    // Separate base key from modifiers (shift, control, alt).
                    while (baseKey.startsWith('+') || baseKey.startsWith('^') || baseKey.startsWith('%')) {
                        modifiers.push(baseKey.charAt(0).replace("+", "shift").replace("^", "control").replace("%", "alt"));
                        baseKey = baseKey.slice(1);
                    }
                    // Rename base key for RobotJS syntax.
                    baseKey = baseKey.toLowerCase();
                    if (baseKey.length > 1) {
                        baseKey = baseKey.replace("arrow", "");
                        baseKey = baseKey.replace("ctrl", "control");
                    }
                    robot.keyTap(baseKey, modifiers);
                }
                catch (ex)
                {
                    // TODO: Report missing keybind.
                }
                break;
            case "PartnerClose":
                capturing = false;
                rtcConnection.close();
                if (socket) {
                    socket.close();
                }
                break;
            case "PartnerError":
                capturing = false;
                rtcConnection.close();
                if (socket) {
                    socket.close();
                }
                break;
            default:
                break;
        };
    };
};
function sendBounds() {
    var request = {
        "Type": "Bounds",
        "Width": totalWidth * videoQuality, // Bounds are modified by videoQuality.
        "Height": totalHeight * videoQuality // Bounds are modified by videoQuality.
    };
    socket.send(JSON.stringify(request));
};
function beginScreenCapture() {
    capturing = true;
    sendBounds();
    showTooltip($("#inputAgentStatus"), "left", "green", "An agent is now viewing your screen.");
    $("#inputAgentStatus").val("Connected");
    $("#inputAgentStatus").css("color", "green");
    if (useWebSocket) {
        worker.webContents.executeJavaScript("getCapture()");   
    }
};
function initRTC() {
    rtcConnection = new RTCPeerConnection({
        iceServers: [
            {
                urls: [
                    "stun:play.after-game.net",
                    "stun:stun.stunprotocol.org",
                    "stun:stun.l.google.com:19302",
                    "stun:stun1.l.google.com:19302",
                    "stun:stun2.l.google.com:19302",
                    "stun:stun3.l.google.com:19302",
                    "stun:stun4.l.google.com:19302"
                ]
            }
        ]
    });
    rtcConnection.onicecandidate = function (evt) {
        if (evt.candidate) {
            socket.send(JSON.stringify({
                'Type': 'RTCCandidate',
                'Candidate': evt.candidate
            }));
        }
    };
    addRTCMedia();
}
function addRTCMedia() {
    navigator.webkitGetUserMedia({
        audio: false,
        video: {
            mandatory: {
                chromeMediaSource: 'desktop',
                minWidth: Math.round(totalWidth * videoQuality),
                maxWidth: Math.round(totalWidth * videoQuality),
                minHeight: Math.round(totalHeight * videoQuality),
                maxHeight: Math.round(totalHeight * videoQuality),
            }
        }
    }, function (stream) {
        // Success callback.
        if (rtcConnection.addTrack) {
            rtcConnection.addTrack(stream);
            $("#videoScreen")[0].src = URL.createObjectURL(stream);
        }
        else if (rtcConnection.addStream) {
            rtcConnection.addStream(stream);
            $("#videoScreen")[0].src = URL.createObjectURL(stream);
        }

    }, function () {
        // Failure callback.
        useWebSocket = true;
    })
}
function reconnect() {
    $("#inputSessionID").val("");
    rtcConnection = undefined;
    $("#sectionNewSession").hide();
    $("#sectionMain").show();
    openWebSocket();
}
function disconnectAgent() {
    if (socket.readyState == WebSocket.OPEN) {
        socket.close();
    }
}
function dataURItoBlob(dataURI) {
    var arrURI = dataURI.split(",");
    var byteString;
    if (arrURI.length > 1) {
        byteString = atob(arrURI[1])
    }
    else {
        byteString = atob(arrURI[0]);
    }
    var ab = new ArrayBuffer(byteString.length);
    var ia = new Uint8Array(ab);
    for (var i = 0; i < byteString.length; i++) {
        ia[i] = byteString.charCodeAt(i);
    }
    var blob = new Blob([ab]);
    return blob;
}
function openTransferredFiles() {
    var strPath = os.tmpdir() + "\\InstaTech\\";
    if (fs.existsSync(os.tmpdir() + "\\InstaTech\\")) {
        electron.shell.openItem(os.tmpdir() + "\\InstaTech\\");
    }
    else {
        showTooltip($("#inputFilesTransferred"), "left", "black", "No files available.");
    }
}
function stopScreenCapture() {
    capturing = false;
};
function copySessionID() {
    $("#inputSessionID")[0].select();
    electron.clipboard.writeText($("#inputSessionID").val());
    showTooltip($("#inputSessionID"), "left", "green", "Copied to clipboard!"); 
}
function showTooltip(objPlacementTarget, strPlacementDirection, strColor, strMessage) {
    if (objPlacementTarget instanceof jQuery) {
        objPlacementTarget = objPlacementTarget[0];
    }
    var divTooltip = document.createElement("div");
    divTooltip.innerText = strMessage;
    divTooltip.classList.add("tooltip");
    divTooltip.style.zIndex = 3;
    divTooltip.id = "tooltip" + String(Math.random());
    $(divTooltip).css({
        "position": "absolute",
        "background-color": "whitesmoke",
        "color": strColor,
        "border-radius": "10px",
        "padding": "5px",
        "border": "1px solid dimgray",
        "font-size": ".8em",
    });
    var rectPlacement = objPlacementTarget.getBoundingClientRect();
    switch (strPlacementDirection) {
        case "top":
            {
                divTooltip.style.top = Number(rectPlacement.top - 5) + "px";
                divTooltip.style.transform = "translateY(-100%)";
                divTooltip.style.left = rectPlacement.left + "px";
                break;
            }
        case "right":
            {
                divTooltip.style.top = rectPlacement.top + "px";
                divTooltip.style.left = Number(rectPlacement.right + 5) + "px";
                break;
            }
        case "bottom":
            {
                divTooltip.style.top = Number(rectPlacement.bottom + 5) + "px";
                divTooltip.style.left = rectPlacement.left + "px";
                break;
            }
        case "left":
            {
                divTooltip.style.top = rectPlacement.top + "px";
                divTooltip.style.right = document.body.clientWidth - rectPlacement.left + 5 + "px";
                break;
            }
        case "center":
            {
                divTooltip.style.top = Number(rectPlacement.bottom - (rectPlacement.height / 2)) + "px";
                divTooltip.style.left = Number(rectPlacement.right - (rectPlacement.width / 2)) + "px";
                divTooltip.style.transform = "translate(-50%, -50%)";
            }
        default:
            break;
    }
    $(document.body).append(divTooltip);
    window.setTimeout(function () {
        $(divTooltip).animate({ opacity: 0 }, 1000, function (tooltip) {
            $(divTooltip).remove();
        })
    }, strMessage.length * 50);
}
function toggleMenu() {
    $("#divMenu").slideToggle(200);
}
function deleteFolderRecursive (path) {
    if (fs.existsSync(path)) {
        fs.readdirSync(path).forEach(function (file, index) {
            var curPath = path + "/" + file;
            if (fs.lstatSync(curPath).isDirectory()) {
                deleteFolderRecursive(curPath);
            } else {
                fs.unlinkSync(curPath);
            }
        });
        fs.rmdirSync(path);
    }
};
function openAbout() {
    var about = new electron.remote.BrowserWindow({
        width: 400,
        height: 350,
        show: false,
        title: "About InstaTech",
        icon: `file://${__dirname}/Assets/InstaTech.ico`
    })
    about.setMenuBarVisibility(false);
    about.loadURL(`file://${__dirname}/about.html`);
    about.on('ready-to-show', function () {
        about.show();
        toggleMenu();
    });
};

electron.ipcRenderer.on("screen-capture", function (event, capture) {
    if (!capturing) {
        return;
    }
    if (capture == null) {
        window.setTimeout(function () {
            worker.webContents.executeJavaScript("getCapture()");
        }, 100);
        return;
    }
    socket.send(dataURItoBlob(capture));
    window.setTimeout(function () {
        worker.webContents.executeJavaScript("getCapture()");
    }, 100);
});
window.onclick = function(){
    if ($("#divMenu").is(":visible") && !$("#imgMenu").is(":hover") && !$("#divMenu").is(":hover")) {
        toggleMenu();
    };
}


///* Entry point. *///

$.each(electron.screen.getAllDisplays(), function (index, element) {
    totalWidth += element.bounds.width;
    totalHeight = Math.max(totalHeight, element.bounds.height);
    offsetX = Math.min(element.bounds.x, offsetX);
    offsetY = Math.min(element.bounds.y, offsetY);
});

openWebSocket();