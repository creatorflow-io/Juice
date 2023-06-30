
// comment import to run in non-module mode
 import {JUpload, FileExistsBehavior} from "../dist/JUpload.js";

console.log(JUpload, FileExistsBehavior);

var uploader = new JUpload("https://localhost:44368/storage1");

function clear() {
    history.pushState({ UploadId: "" }, "Upload ", "?");
    document.getElementById("uploadId").value = "";
}

uploader.onsuccess = function (upload, progress) {
    console.log("success", upload);
    document.getElementById("message").innerHTML = progress.message;
    clear();
}


uploader.onerror = function (upload, message) {
    console.log("error", upload, message);
    document.getElementById("message").innerHTML = message;
}


uploader.onprogress = function (progress) {
    console.log("progress", progress.message);
    document.getElementById("message").innerHTML = progress.message;
}

uploader.onupload = function (upload) {
    let that = this;

    document.getElementById("resume").disabled = true;
    history.pushState(upload, "Upload " + upload.Name, "?uploadId=" + upload.UploadId);
    document.getElementById("uploadId").value = upload.UploadId;

    setTimeout(function () {
        that.abort();
    }, 1000);
}

uploader.onabort = function (upload) {
    console.log("abort", upload);
    document.getElementById("resume").disabled = false;
    document.getElementById("message").innerHTML = "Upload aborted. Click Resume to continue.";
}

document.getElementById("file").onchange = function (event) {
    if (this.files[0]) {

        let uploadId = document.getElementById("uploadId").value;
        if (!uploadId) {

            uploader.upload(this.files[0]
                //, { fileExistsBehavior: FileExistsBehavior.RaiseError }
            );
        }
        else {
            uploader.resume(uploadId, this.files[0]);
        }
    }
}

document.getElementById("clear").onclick = clear;
document.getElementById("resume").onclick = function () {
    uploader.resume();
};


const params = new URLSearchParams(window.location.search);
if (params.has("uploadId")) {
    document.getElementById("uploadId").value = params.get("uploadId");
}

