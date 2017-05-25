window.onerror = function (message, source, lineno, colno, error) {
    var fs = require("fs");
    var os = require("os");
    var jsonError = {
        "Type": "Error",
        "Timestamp": new Date().toString(),
        "Message": message,
        "Source": source,
        "StackTrace": "Line: " + lineno + " Col: " + colno,
        "Error": error
    };
    fs.appendFile(os.tmpdir() + "/InstaTech_CP_Logs.txt", JSON.stringify(jsonError) + "\r\n");
    // This is required to ignore random Electron renderer error.
    if (capturing) {
        getCapture();
        return true;
    }
};

// ***  Config: hostName is set in main.js.  *** //

// Server host name.
var hostName = require("electron").remote.getGlobal("hostName");

// Websocket service path.
var wsPath = "wss://" + hostName + "/Services/Remote_Control_Socket.cshtml";
// File transfer service path.
var ftPath = "https://" + hostName + "/Services/File_Transfer.cshtml";
// Remote control path.
var rcPath = "https://" + hostName + "/Remote_Control/";

const robot = require("robotjs");
const electron = require('electron');
const fs = require("fs");
const os = require("os");
const buf = require("buffer");
var win = electron.remote.getCurrentWindow();
var fr = new FileReader();
var socket;
var capturing = false;
var lastMouseMove;
var ctx;
var imgData;
var video;
var img;
var byteSuffix;
var lastFrame;
var croppedFrame;
var tempCanvas = document.createElement("canvas");
var boundingBox;
var sendFullScreenshot = true;
var totalWidth = 0;
var totalHeight = 0;
// Offsets are the left and top edge of the screen, in case multiple monitor setups
// create a situation where the edge of a monitor is in the negative.  This must
// be converted to a 0-based max left/top to render images on the canvas properly.
var offsetX = 0;
var offsetY = 0;

function openWebSocket() {
    try {
        socket = new WebSocket(wsPath);
    }
    catch (ex) {
        wsPath = wsPath.replace("wss:", "ws:");
        ftPath = ftPath.replace("https:", "http:");
        rcPath = rcPath.replace("https:", "http:");
        try {
            socket = new WebSocket(wsPath);
            electron.remote.dialog.showMessageBox({
                type: "question",
                title: "Connection Not Secure",
                message: "A secure connection couldn't be established.  SSL is not configured properly on the server.  Do you want to proceed with an unencrypted connection?",
                buttons: ["Yes", "No"],
                defaultId: 0,
                cancelId: 1
            }, function (selection) {
                if (selection == 1) {
                    electron.remote.app.exit(0);
                }
            })
        }
        catch (e) {
            electron.remote.dialog.showMessageBox({
                type: "error",
                title: "Connection Failed",
                message: "Unable to connect to server."
            })
            return;
        }
    }
    socket.onopen = function (e) {
        var request = {
            "Type": "ConnectionType",
            "ConnectionType": "ClientApp"
        };
        socket.send(JSON.stringify(request));
    };
    socket.onclose = function (e) {
        capturing = false;
        $("#inputAgentStatus").val("Not Connected");
        $("#inputAgentStatus").css("color", "black");
        $("#sectionMain").hide();
        $("#sectionNewSession").show();
    };
    socket.onerror = function (e) {
        capturing = false;
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
                beginScreenCapture();
                break;
            case "RefreshScreen":
                sendBounds();
                sendFullScreenshot = true;
                break;
            case "FrameReceived":
                getCapture();
                break;
            case "FileTransfer":
                if (!fs.existsSync(os.tmpdir() + "\\InstaTech\\")) {
                    fs.mkdirSync(os.tmpdir() + "\\InstaTech\\");
                };
                var retrievalCode = jsonMessage.RetrievalCode;
                var request = {
                    "Type": "Download",
                    "RetrievalCode": retrievalCode
                };
                $.post(ftPath, JSON.stringify(request), function (data) {
                    fs.writeFileSync(os.tmpdir() + "\\InstaTech\\" + jsonMessage.FileName, buf.Buffer.from(data, "base64"));
                    $("#inputFilesTransferred").val(fs.readdirSync(os.tmpdir() + "\\InstaTech\\").length);
                    showTooltip($("#inputFilesTransferred"), "left", "black", "File downloaded.");
                });
                break;
            case "SendClipboard":
                electron.clipboard.writeText(atob(jsonMessage.Data));
                showTooltip($("#inputFilesTransferred"), "left", "black", "Clipboard data set.");
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
                // Modifier down.
                if (jsonMessage.Alt)
                {
                    robot.keyToggle("alt", "down");
                }
                else if (jsonMessage.Ctrl) {
                    robot.keyToggle("control", "down");
                }
                else if (jsonMessage.Shift) {
                    robot.keyToggle("shift", "down");
                }
                // Mouse down.
                if (jsonMessage.Button == "Left") {
                    robot.mouseToggle("down", "left");
                }
                else if (jsonMessage.Button == "Right") {
                    robot.mouseToggle("down", "right");
                }
                // Modifier up.
                if (jsonMessage.Alt) {
                    robot.keyToggle("alt", "up");
                }
                else if (jsonMessage.Ctrl) {
                    robot.keyToggle("control", "up");
                }
                else if (jsonMessage.Shift) {
                    robot.keyToggle("shift", "up");
                }
                break;
            case "MouseUp":
                robot.moveMouse(Math.round(jsonMessage.PointX * totalWidth + offsetX), Math.round(jsonMessage.PointY * totalHeight + offsetY));
                // Modifier down.
                if (jsonMessage.Alt) {
                    robot.keyToggle("alt", "down");
                }
                else if (jsonMessage.Ctrl) {
                    robot.keyToggle("control", "down");
                }
                else if (jsonMessage.Shift) {
                    robot.keyToggle("shift", "down");
                }
                // Mouse up.
                if (jsonMessage.Button == "Left") {
                    robot.mouseToggle("up", "left");
                }
                else if (jsonMessage.Button == "Right") {
                    robot.mouseToggle("up", "right");
                }
                // Modifier up.
                if (jsonMessage.Alt) {
                    robot.keyToggle("alt", "up");
                }
                else if (jsonMessage.Ctrl) {
                    robot.keyToggle("control", "up");
                }
                else if (jsonMessage.Shift) {
                    robot.keyToggle("shift", "up");
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
                    var baseKey = jsonMessage.Key.toLowerCase();
                    if (baseKey.startsWith("{"))
                    {
                        baseKey = baseKey.slice(0);
                    }
                    if (baseKey.endsWith("}"))
                    {
                        baseKey = baseKey.slice(0), baseKey.length - 1;
                    }
                    var modifiers = jsonMessage.Modifiers;

                    // Rename base key for RobotJS syntax.
                    if (baseKey.length > 1) {
                        baseKey = baseKey.replace("arrow", "");
                        baseKey = baseKey.replace("ctrl", "control");
                    }
                    robot.keyTap(baseKey, modifiers);
                }
                catch (ex)
                {
                    var fs = require("fs");
                    fs.appendFile(os.tmpdir() + "/InstaTech_CP_Logs.txt", "Missing keybind for " + jsonMessage.Key + "\r\n");
                }
                break;
            case "PartnerClose":
                capturing = false;
                if (socket) {
                    socket.close();
                }
                break;
            case "PartnerError":
                capturing = false;
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
        "Width": totalWidth,
        "Height": totalHeight
    };
    socket.send(JSON.stringify(request));
};
function beginScreenCapture() {
    capturing = true;
    sendBounds();
    showTooltip($("#inputAgentStatus"), "left", "green", "An agent is now viewing your screen.");
    $("#inputAgentStatus").val("Connected");
    $("#inputAgentStatus").css("color", "green");
    getCapture();
};

function reconnect() {
    $("#inputSessionID").val("");
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
        "box-shadow": "10px 5px 5px rgba(0,0,0,.2)"
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
function getCapture() {
    video = document.getElementById("videoScreen");
    if (video.src == "") {
        navigator.webkitGetUserMedia({
            audio: false,
            video: {
                mandatory: {
                    chromeMediaSource: 'desktop',
                    minWidth: Math.round(totalWidth),
                    maxWidth: Math.round(totalWidth),
                    minHeight: Math.round(totalHeight),
                    maxHeight: Math.round(totalHeight),
                }
            }
        }, function (stream) {
            // Success callback.
            video.src = URL.createObjectURL(stream);
            captureImage();
        }, function () {
            // Error callback.
            throw "Unable to capture screen.";
        });
    }
    else {
        captureImage();
    }
}

function captureImage() {
    ctx.drawImage(document.getElementById("videoScreen"), 0, 0);
    imgData = ctx.getImageData(0, 0, ctx.canvas.width, ctx.canvas.height).data;
    if (sendFullScreenshot || lastFrame == undefined) {
        sendFullScreenshot = false;
        croppedFrame = new Blob([electron.nativeImage.createFromDataURL(ctx.canvas.toDataURL()).toJpeg(100), new Uint8Array(6)]);
    }
    else {
        getChangedPixels(imgData, lastFrame);
    }
    lastFrame = imgData;
    if (croppedFrame == null) {
        window.setTimeout(captureImage, 50);
    } else {
        fr = new FileReader();
        fr.onload = function () {
            socket.send(dataURItoBlob(this.result));
        };
        fr.readAsDataURL(croppedFrame);
    }
}
function getChangedPixels(newImgData, oldImgData) {
    var left = totalWidth + 1;
    var top = totalHeight + 1;
    var right = -1;
    var bottom = -1;
    // Check RGBA value for each pixel.
    for (var counter = 0; counter < newImgData.length - 4; counter += 4) {
        if (newImgData[counter] != lastFrame[counter] ||
            newImgData[counter + 1] != lastFrame[counter + 1] ||
            newImgData[counter + 2] != lastFrame[counter + 2] ||
            newImgData[counter + 3] != lastFrame[counter + 3]) {
            // Change was found.
            var pixel = counter / 4;
            var row = Math.floor(pixel / ctx.canvas.width);
            var column = pixel % ctx.canvas.width;
            if (row < top) {
                top = row;
            }
            if (row > bottom) {
                bottom = row;
            }
            if (column < left) {
                left = column;
            }
            if (column > right) {
                right = column;
            }
        }
    }
    if (left < right && top < bottom) {
        // Bounding box is valid.

        left = Math.max(left - 20, 0);
        top = Math.max(top - 20, 0);
        right = Math.min(right + 20, totalWidth);
        bottom = Math.min(bottom + 20, totalHeight);

        // Byte array that indicates top left coordinates of the image.
        byteSuffix = new Uint8Array(6);
        var strLeft = String(left);
        var strTop = String(top);
        while (strLeft.length < 6) {
            strLeft = "0" + strLeft;
        }
        while (strTop.length < 6) {
            strTop = "0" + strTop;
        }
        byteSuffix[0] = strLeft.slice(0, 2);
        byteSuffix[1] = strLeft.slice(2, 4);
        byteSuffix[2] = strLeft.slice(4);
        byteSuffix[3] = strTop.slice(0, 2);
        byteSuffix[4] = strTop.slice(2, 4);
        byteSuffix[5] = strTop.slice(4);
        boundingBox = {
            x: left,
            y: top,
            width: right - left,
            height: bottom - top
        }
        tempCanvas.width = boundingBox.width;
        tempCanvas.height = boundingBox.height;
        tempCanvas.getContext("2d").drawImage(ctx.canvas, boundingBox.x, boundingBox.y, boundingBox.width, boundingBox.height, 0, 0, boundingBox.width, boundingBox.height);
        croppedFrame = new Blob([electron.nativeImage.createFromDataURL(tempCanvas.toDataURL()).toJpeg(100), byteSuffix]);
    }
    else {
        croppedFrame = null;
    }
}
function ArrBuffToString(buffer) {
    return String.fromCharCode.apply(null, new Uint16Array(buffer));
}

function StringToArrBuff(strData) {
    var buff = new ArrayBuffer(strData.length * 2); // 2 bytes for each char
    var buffView = new Uint16Array(buf);
    for (var i = 0; i < strData.length; i++) {
        buffView[i] = str.charCodeAt(i);
    }
    return buff;
}
function openViewer() {
    electron.shell.openExternal(rcPath);
}
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

window.onclick = function(){
    if ($("#divMenu").is(":visible") && !$("#imgMenu").is(":hover") && !$("#divMenu").is(":hover")) {
        toggleMenu();
    };
}


///* Entry point. *///
$(document).ready(function () {
    ctx = document.getElementById("canvasScreen").getContext("2d");
    $.each(electron.screen.getAllDisplays(), function (index, element) {
        totalWidth += element.bounds.width;
        totalHeight = Math.max(totalHeight, element.bounds.height);
        offsetX = Math.min(element.bounds.x, offsetX);
        offsetY = Math.min(element.bounds.y, offsetY);
    });
    ctx.canvas.width = Math.round(totalWidth);
    ctx.canvas.height = Math.round(totalHeight);
    openWebSocket();
});
