// ***  Config: Change these variables for your environment.  *** //
global.hostName = "instatech-test.azurewebsites.net";

// Set to true to enable dev tools for debugging. (Note: Server target is also set in index.js based on this.)
global.debug = true;

// A service URL that will respond to a GET request with the current version.
var versionUrl = "";

if (process.platform == "linux")
{
    versionURL = "https://" + global.hostName + "/Services/Get_Linux_Client_Version.cshtml";
}
else if (process.platform == "darwin") {
    versionURL = "https://" + global.hostName + "/Services/Get_Mac_Client_Version.cshtml";
}
else if (process.platform == "win32") {
    versionURL = "https://" + global.hostName + "/Services/Get_Win_CP_Client_Version.cshtml";
}

// The URLs of the application's current version per OS.
var downloadURLMac = "https://" + global.hostName + "/Downloads/InstaTech_CP.dmg";
var downloadURLLinux = "https://" + global.hostName + "/Downloads/InstaTech_CP.AppImage";
var downloadURLWin = "https://" + global.hostName + "/Downloads/InstaTech_CP.exe";

const electron = require('electron');
const app = electron.app;
const os = require("os");
const fs = require("fs");
const https = require("https");
const http = require("http");

// Prevent window from being garbage collected.
let mainWindow;

function createMainWindow() {
    const win = new electron.BrowserWindow({
		width: 300,
        height: 250,
        show: false,
        title: "InstaTech",
        icon: `file://${__dirname}/Assets/InstaTech.ico`
    });
    win.setMenuBarVisibility(global.debug);
    win.setResizable(global.debug);
    win.setMaximizable(global.debug);
    if (global.debug) {
        win.setBounds({
            x: 50,
            y: 50,
            width: 1200,
            height: 600
        });
        win.webContents.openDevTools();
    }
    win.loadURL(`file://${__dirname}/index.html`);
    win.on('closed', function () { app.quit() });
    win.on('ready-to-show', function () {
        win.show();
        checkForUpdates();
    });
	return win;
}
function checkForUpdates() {
    try {
        https.get("https://" + hostName);
    }
    catch (ex) {
        versionUrl = versionUrl.replace("https", "http");
        downloadURLLinux = downloadURLLinux.replace("https", "http");
        downloadURLMac = downloadURLMac.replace("https", "http");
    }
    // Version couldn't be identified.
    if (!versionUrl) {
        return;
    }
    // Check for updates.
    https.get(versionURL, function (res) {
        res.on("data", function (ver) {
            if (ver != electron.app.getVersion() && ver != "0.0.0.0") {
                electron.dialog.showMessageBox({
                    type: "question",
                    title: "Update Available",
                    message: "A new version is available.  Would you like to install it now?",
                    buttons: ["Yes", "No"],
                    defaultId: 0,
                    cancelId: 1
                }, function (selection) {
                    if (selection == 0) {
                        var downloadURL;
                        switch (process.platform) {
                            case "linux":
                                downloadURL = downloadURLLinux;
                                break;
                            case "darwin":
                                downloadURL = downloadURLMac;
                                break;
                            case "win32":
                                downloadURL = downloadURLWin;
                            default:
                                downloadURL = "";
                        }
                        var fileName = downloadURL.split("/")[downloadURL.split("/").length - 1];
                        if (fs.existsSync(os.tmpdir() + "/" + fileName)) {
                            fs.unlinkSync(os.tmpdir() + "/" + fileName);
                        };
                        var downloadWin = new electron.BrowserWindow({
                            width: 300,
                            height: 150,
                            show: true,
                            title: "InstaTech Update",
                            icon: `file://${__dirname}/Assets/InstaTech.ico`
                        });
                        downloadWin.setMenuBarVisibility(false);
                        downloadWin.loadURL(`file://${__dirname}/downloading.html`);
                        https.get(downloadURL, function (result) {
                            var stream = fs.createWriteStream(os.tmpdir() + "/" + fileName);
                            result.pipe(stream);
                            stream.on("finish", function () {
                                stream.close();
                                electron.shell.openExternal(os.tmpdir() + "/" + fileName);
                                electron.app.exit(0);
                            });
                        });
                    }
                })
            }
        });
    });
}
function deleteFolderRecursive(path) {
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

function cleanupTempFiles() {
    // Remove previous session's temp files (if any).
    if (fs.existsSync(os.tmpdir() + "/InstaTech/")) {
        deleteFolderRecursive(os.tmpdir() + "/InstaTech/");
    };
}
app.on('window-all-closed', () => {
	if (process.platform !== 'darwin') {
		app.quit();
	}
});

app.on('activate', () => {
	if (!mainWindow) {
		mainWindow = createMainWindow();
	}
});

app.on('ready', () => {
    mainWindow = createMainWindow();
    cleanupTempFiles();
});