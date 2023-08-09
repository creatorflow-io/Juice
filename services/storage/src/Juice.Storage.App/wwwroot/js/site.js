// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

//import * as JUpload from "../lib/creatorflow-io/juice-upload/src/jupload.js";

console.log(JUpload);

window.uploader = new JUpload("/storage1");
let uploader1 = new JUpload("/storage");

setTimeout(function () {
    console.clear();
    console.log(FileExistsBehavior);
    console.log(uploader.endpoint, uploader1.endpoint);
}, 1500);

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
