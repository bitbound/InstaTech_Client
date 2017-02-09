// ***  Config: Change these variables for your environment.  *** //
global.hostName = "";

// Set to true to enable dev tools for debugging. (Note: Server target is also set in index.js based on this.)
global.debug = false;

// A service URL that will respond to a GET request with the current version.
var versionUrl;
if (process.platform == "win32")
{
    versionURL = "https://" + global.hostName + "/Services/Get_CP_Client_Version.cshtml";
}
else if (process.platform == "linux")
{
    versionURL = "https://" + global.hostName + "/Services/Get_Linux_Client_Version.cshtml";
}

// The URLs of the application's current version per OS.
var downloadURLWindows = "https://" + global.hostName + "/Downloads/InstaTech_CP.exe";
var downloadURLMac = "";
var downloadURLLinux = "https://" + global.hostName + "/Downloads/InstaTech_CP.AppImage";

const electron = require('electron');

const app = electron.app;
const os = require("os");
const fs = require("fs");
const https = require("https");

// Prevent window from being garbage collected.
let mainWindow;
let workerWindow;

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
    });
	return win;
}
function checkForUpdates() {
    // Check for updates.
    https.get(versionURL, function (res) {
        res.on("data", function (ver) {
            if (ver != electron.app.getVersion()) {
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
                            case "win32":
                                downloadURL = downloadURLWindows;
                                break;
                            case "linux":
                                downloadURL = downloadURLLinux;
                                break;
                            case "darwin":
                                downloadURL = downloadURLMac;
                                break;
                            default:
                                downloadURL = "";
                        }
                        var fileName = downloadURL.split("/")[downloadURL.split("/").length - 1];
                        if (!fs.existsSync(os.tmpdir() + "\\" + fileName)) {
                            fs.unlinkSync(os.tmpdir() + "\\" + fileName);
                        };
                        https.get(downloadURL, function (result) {
                            var stream = fs.createWriteStream(os.tmpdir() + "\\" + fileName);
                            result.pipe(stream);
                            stream.on("finish", function () {
                                stream.close();
                                electron.shell.openExternal(os.tmpdir() + "\\" + fileName);
                                electron.remote.app.exit(0);
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
    if (fs.existsSync(os.tmpdir() + "\\InstaTech\\")) {
        deleteFolderRecursive(os.tmpdir() + "\\InstaTech\\");
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
    workerWindow = new electron.BrowserWindow({
        show: false,
        skipTaskbar: true,
        title: "InstaTech Worker",
    });
    workerWindow.loadURL(`file://${__dirname}/worker.html`);
    checkForUpdates();
    cleanupTempFiles();
});