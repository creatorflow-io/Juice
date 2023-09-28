## Juice upload library
<div align="center">
  <a href="https://github.com/creatorflow-io/juice-upload">
    <img src="https://avatars.githubusercontent.com/u/107674950" alt="Cfio logo" width="200" height="200">
  </a>
</div>

### Build library
`npm run build`

### Run demo
`npm run serve`

### Api
Supported methods:
- upload
- resume
- abort
- exists

Supported events:
- onupload
- onsuccess
- onerror
- onprogress
- onabort

### Usage

Embed javascript library
```html
<script src="~/lib/juice-js/upload/dist/jupload.js" asp-append-version="true"></script>
```

Or import library in your own js file

```javascript
import {JUpload, FileExistsBehavior} from "~/lib/juice-js/upload/dist/jupload.js"; 

// enum FileExistsBehavior{RaiseError: 0,    Replace: 1,    AscendedCopyNumber: 2,    Resume: 3}
// class UploadConfiguration{ UploadId, Name, Offset, SectionSize, PackageSize, Exists}
// class Progress(percent, bps, totalTime, remaining)

// Init uploader with upload endpoint
window.uploader = new JUpload("/storage1");

// Init upload events
uploader.onsuccess = function (upload, progress) {
    //upload: UploadConfiguration
    console.log("success", upload);
}

uploader.onerror = function (upload, message) {
    //upload: UploadConfiguration
    console.log("error", upload, message);
}

uploader.onprogress = function (progress) {
    //progress: Progress
    console.log("progress", progress.message);
}

// NOTE: onupload event will only be called on the large file, after the first part is uploaded.
// The smaller file will be completed in single request so it only fires onsuccess/onerror event
uploader.onupload = function (upload) {
    //upload: UploadConfiguration
    console.log("start", upload.Name, upload.UploadId);
    document.getElementById("uploadId").value = upload.UploadId; // set upload id to handle resume later
}

uploader.onabort = function (upload) {
    //upload: UploadConfiguration
    console.log("abort", upload);
}

// Handle file input event to upload or resume
// The sectionSize option will be used only for first request, after that it will be replaced by the sectionSize in the response headers
document.getElementById("file").onchange = function (event) {
    if (this.files[0]) {
        let uploadId = document.getElementById("uploadId").value;
        if (!uploadId) {
        
            uploader.upload(this.files[0]
                //, { 
                //    fileExistsBehavior: FileExistsBehavior.RaiseError, 
                //    filePath: "foo/bar/"+this.files[0].name,
                //    correlationId: "1234",
                //    metadata: {
                //        "key1": "value1",
                //        "key2": "value2"
                //    },
                //    sectionSize: 5000000
                //  } // option
            );
        }
        else {
            // check exists
            // uploader.exists(this.files[0].name);
            uploader.resume(uploadId, this.files[0]);
        }
    }
}
document.getElementById("abort").onclick = function(){
    uploader.abort();
}
```

