const electron = require('electron');

// To avoid update running twice.
if (require('electron-squirrel-startup')) return;

// Set to true to enable dev tools for debugging. (Note: Server target is in index.js)
var debug = false;

///<reference path="typings/index.d.ts" />
const app = electron.app;
const os = require("os");
const fs = require("fs");
const https = require("https");

// prevent window being garbage collected
let mainWindow;
let workerWindow;

function createMainWindow() {
    const win = new electron.BrowserWindow({
		width: 300,
        height: 250,
        show: false,
        title: "InstaTech",
        icon: `file://${__dirname}/Assets/InstaTech Logo.ico`
    });
    win.setMenuBarVisibility(debug);
    win.setResizable(debug);
    win.setMaximizable(debug);
    if (debug) {
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
    https.get({
        hostname: 'instatech.org',
        path: '/Services/GetCPClientVersion.cshtml',
        method: "GET",
        headers: {
            "Access-Control-Allow-Origin": "*"
        },
    }, function (res) {
        res.on("data", function (ver) {
            if (ver != electron.app.getVersion()) {
                electron.dialog.showMessageBox({
                    type: "question",
                    title: "Update Available",
                    message: "A new version is available.  Would you like to go to the InstaTech site to download it?",
                    buttons: ["Yes", "No"],
                    defaultId: 0,
                    cancelId: 1
                }, function (selection) {
                    if (selection == 0) {
                        electron.shell.openExternal("https://instatech.org/Downloads");
                    }
                })
            }
        });
    });
}

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