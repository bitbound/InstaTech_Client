window.onerror = function (message, source, lineno, colno, error) {
    var fs = require("fs");
    var os = require("os");
    fs.appendFile(os.tmpdir() + "/InstaTech_CP_Errors.txt", new Date().toString() + "\t" + message + "\r\n");
    // This is required to ignore random Electron renderer error.
    mainWindow.webContents.send("screen-capture", null);
    return true;
};
const electron = require('electron');
const robot = require("robotjs");
const fs = require("fs");
const os = require("os");
var jpegQuality = 75;
var win = electron.remote.getCurrentWindow();
var mainWindow = electron.remote.BrowserWindow.fromId(1);
var ctx;
var fr = new FileReader();
var imgData;
var video;
var videoQuality = .75;
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

function getCapture() {
    video = document.getElementById("videoScreen");
    if (video.src == "") {
        ctx.canvas.width = Math.round(totalWidth * videoQuality);
        ctx.canvas.height = Math.round(totalHeight * videoQuality);
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
        croppedFrame = new Blob([electron.nativeImage.createFromDataURL(ctx.canvas.toDataURL()).toJpeg(jpegQuality), new Uint8Array(4)]);
    }
    else {
        getChangedPixels(imgData, lastFrame);
    }
    lastFrame = imgData;
    if (croppedFrame == null) {
        mainWindow.webContents.send("screen-capture", null);
    } else {
        fr = new FileReader();
        fr.onload = function () {
            mainWindow.webContents.send("screen-capture", this.result);
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
        byteSuffix = new Uint8Array(4);
        var strLeft = String(left);
        var strTop = String(top);
        while (strLeft.length < 4) {
            strLeft = "0" + strLeft;
        }
        while (strTop.length < 4) {
            strTop = "0" + strTop;
        }
        byteSuffix[0] = strLeft.slice(0, 2);
        byteSuffix[1] = strLeft.slice(2);
        byteSuffix[2] = strTop.slice(0, 2);
        byteSuffix[3] = strTop.slice(2);
        boundingBox = {
            x: left,
            y: top,
            width: right - left,
            height: bottom - top
        }
        tempCanvas.width = boundingBox.width;
        tempCanvas.height = boundingBox.height;
        tempCanvas.getContext("2d").drawImage(ctx.canvas, boundingBox.x, boundingBox.y, boundingBox.width, boundingBox.height, 0, 0, boundingBox.width, boundingBox.height);
        croppedFrame = new Blob([electron.nativeImage.createFromDataURL(tempCanvas.toDataURL()).toJpeg(jpegQuality), byteSuffix]);
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

$(document).ready(function () {
    ctx = document.getElementById("canvasScreen").getContext("2d");
    $.each(electron.screen.getAllDisplays(), function (index, element) {
        totalWidth += element.bounds.width;
        totalHeight = Math.max(totalHeight, element.bounds.height);
        offsetX = Math.min(element.bounds.x, offsetX);
        offsetY = Math.min(element.bounds.y, offsetY);
    });
});