// const FileExistsBehavior = require( './FileExistsBehavior.js');
// const Progress = require( './Progress.js');

// const $ = require('jquery');

import FileExistsBehavior from './FileExistsBehavior';
import Progress from './Progress';
import $ from 'jquery';

function JUpload(endpoint) {
    this._endpoint = endpoint || "/storage";
}

Object.defineProperty(JUpload.prototype, "uploadId", {
    get: function () { return this._initializedUpload ? this._initializedUpload.UploadId : undefined; },
    enumerable: true,
    configurable: true
});

Object.defineProperty(JUpload.prototype, "endpoint", {
    get: function () { return this._endpoint; },
    enumerable: true,
    configurable: true
});

JUpload.prototype.upload = function (file, options) {

    options = $.extend({
        "metadata": {},
        "fileExistsBehavior": FileExistsBehavior.AscendedCopyNumber,
        "sectionSize": 5000000 // default to chunked upload
    }, options || {});

    if(typeof options.metadata === "object") {
        options.metadata = JSON.stringify(options.metadata);
    }

    let that = this;

    that._file = file;

    
    firstUpload.call(that, file, options)
    .then((completed, offset, result) => {
        that._initializedUpload = result;

        console.log("File initialized", that._initializedUpload);
        if(completed){
            _successWithoutReport.call(that);
        }else{
            if (typeof that.onupload === "function") {
                try {
                    that.onupload(result);
                } catch (e) {
                    console.error("Failed to process onupload event", e);
                }
            }
            _startTrackingProgress.call(that, result.PackageSize - offset);

            doUpload.call(that, file, offset, result.SectionSize);
        }
    }, (response) => { 
        switch(response.status){
            case 500:
                _error.call(that, "Failed to upload: " + response.responseText);
                break;
            case 401:
            case 403:
                _error.call(that, response.responseText);
                break;
            default:
                _retry.call(that, "Failed to upload: " + response.responseText); 
        }
    });
}

JUpload.prototype.exists = function (filePath) {

    if (typeof this.endpoint === "undefined" || !this.endpoint) {
        throw "The endpoint must be configured.";
    }
    return $.ajax({
        type: "POST",
        url: `${this.endpoint}/exists`,
        crossDomain: true,
        data: {
            filePath: filePath
        }
    });
}

JUpload.prototype.resume = function (uploadId, file) {
    let that = this;

    if (!that._initializedUpload) {
        if (!uploadId) {
            throw "uploadId is required.";
        }

        that._initializedUpload = { UploadId: uploadId };
    }
    if (!that._file) {
        if (!file) {
            throw "file is required.";
        }
        that._file = file;
    }

    resumeUpload.call(that, true);
}

JUpload.prototype.abort = function () {
    let that = this;
    if (that._xhr) {
        console.log("Aborting upload");
        that._xhr.abort();
    }
}

function toIsoString(date) {
    var tzo = -date.getTimezoneOffset(),
        dif = tzo >= 0 ? '+' : '-',
        pad = function(num) {
            return (num < 10 ? '0' : '') + num;
        };

    return date.getFullYear() +
        '-' + pad(date.getMonth() + 1) +
        '-' + pad(date.getDate()) +
        'T' + pad(date.getHours()) +
        ':' + pad(date.getMinutes()) +
        ':' + pad(date.getSeconds()) +
        '.' + date.getMilliseconds() +
        dif + pad(Math.floor(Math.abs(tzo) / 60)) +
        ':' + pad(Math.abs(tzo) % 60);
}

var initUpload = function (file, options) {

    if (typeof this.endpoint === "undefined" || !this.endpoint) {
        throw "The endpoint must be configured.";
    }
    let relativePath = file.webkitRelativePath || file.name;
    let filePath = options.filePath || relativePath;
    let fileExistsBehavior = typeof options.fileExistsBehavior === "undefined" ?
        FileExistsBehavior.AscendedCopyNumber
        : options.fileExistsBehavior;

    return $.ajax({
        type: "POST",
        url: `${this.endpoint}/init`,
        crossDomain: true,
        data: {
            filePath: filePath,
            contentType: file.type,
            correlationId: options.correlationId,
            metadata: options.metadata,
            fileExistsBehavior: fileExistsBehavior,
            fileSize: file.size,
            originalFilePath: relativePath,
            lastModifiedDate: toIsoString(file.lastModifiedDate),
            uploadId: this.uploadId
        }
    });
}

var firstUpload = function (file, options) {

    if (typeof this.endpoint === "undefined" || !this.endpoint) {
        throw "The endpoint must be configured.";
    }
    let that = this;
    let relativePath = file.webkitRelativePath || file.name;
    let filePath = options.filePath || relativePath;
    let fileExistsBehavior = typeof options.fileExistsBehavior === "undefined" ?
        FileExistsBehavior.AscendedCopyNumber
        : options.fileExistsBehavior;
    let sectionSize = typeof options.sectionSize === "undefined" ?
        5000000: options.sectionSize;

    let data = new FormData();

    let end = Math.min(sectionSize, file.size);
    let section = file.size <= sectionSize ? file: file.slice(0, end);

    data.append("filePath", filePath);
    data.append("contentType", file.type);
    data.append("correlationId", options.correlationId);
    data.append("metadata", options.metadata);
    data.append("fileExistsBehavior", fileExistsBehavior);
    data.append("fileSize", file.size);
    data.append("originalFilePath", relativePath);
    data.append("lastModifiedDate", toIsoString(file.lastModifiedDate));

    data.append("file", section);

    console.debug("Upload section", file.size, sectionSize);
    var defer = $.Deferred();
    // store request to abort after
    that._xhr = $.ajax({
        type: "POST",
        url: `${that.endpoint}/upload`,
        crossDomain: true,
        cache: false,
        contentType: false,
        processData: false,
        data: data,
        xhr: function () {
            var xhr = new XMLHttpRequest();
            if (xhr.upload && typeof that.onprogress === "function") {
                xhr.upload.onprogress = function (evt) {
                    that._sectionLoaded = evt.position || evt.loaded;
                    let progress = _calcProgress.call(that);
                    if (typeof that.onprogress === "function") {
                        try { that.onprogress(progress); } catch (e) { console.error("Failed to process onprogress event"); }
                    }
                };
            }
            return xhr;
        },
        beforeSend: function (xhr) {
            try {
                xhr.setRequestHeader("x-offset", 0);
            }
            catch (e) {
                console.log("Failed to set headers before send");
                console.error(e);
                _error.call(that, e);
            }
        },
        complete: function (xhr, statusText) {
            console.debug(xhr, statusText);
            if (statusText === "abort") {
                _abort.call(that);
            }
            else if(xhr.status === 200 || xhr.status === 204){
                console.debug("Upload section completed", xhr.status, xhr.responseText, xhr.getResponseHeader("x-completed"), xhr.getAllResponseHeaders());
                let completed = xhr.getResponseHeader("x-completed")!=null && JSON.parse(xhr.getResponseHeader("x-completed").toLowerCase());
                let offset = xhr.getResponseHeader("x-offset")!=null ? parseInt(xhr.getResponseHeader("x-offset")): null;
                let configuration = xhr.responseText ? JSON.parse(xhr.responseText): null;
                defer.resolve(completed, offset, configuration);
            }else {
                defer.reject(xhr);
            }
        },
        error: function(xhr, statusText, error){
            defer.reject(xhr);
        }
    });
    return defer;
}

var doUpload = function (file, offset, sectionSize) {
    let that = this;
    let uploadId = that._initializedUpload.UploadId;

    //checking for complete
    if (offset === file.size) {
        try {
            _success.call(that, uploadId)
                .then(function (result) {
                    if (typeof that.onsuccess === "function") {
                        let overallProgress = _calcOverallProgress.call(that);
                        that.onsuccess(that._initializedUpload, overallProgress);
                    }
                })
                .catch(function (response) {
                    _retry.call(that, response.responseText);
                });
        } catch (e) {
            console.error("Failed to report complete to server", e);
            _retry.call(that, "Failed to report complete to server");
        }
        return;
    }

    that._sectionOffset = offset;
    that._sectionLoaded = 0;

    let data = new FormData();
    let end = Math.min(offset + sectionSize, file.size);
    let section = file.slice(offset, end);

    data.append("file", section);

    console.debug("Upload section", section, offset, end, sectionSize, offset + sectionSize);

    // store request to abort after
    that._xhr = $.ajax({
        type: "POST",
        url: `${that.endpoint}/upload`,
        crossDomain: true,
        cache: false,
        contentType: false,
        processData: false,
        data: data,
        xhr: function () {
            var xhr = new XMLHttpRequest();
            if (xhr.upload && typeof that.onprogress === "function") {
                xhr.upload.onprogress = function (evt) {
                    that._sectionLoaded = evt.position || evt.loaded;
                };
            }
            return xhr;
        },
        beforeSend: function (xhr) {
            try {
                xhr.setRequestHeader("x-uploadid", uploadId);
                xhr.setRequestHeader("x-offset", offset);
            }
            catch (e) {
                console.log("Failed to set headers before send");
                console.error(e);
                _error.call(that, e);
            }
        },
        complete: function (xhr, statusText) {
            console.debug(xhr, statusText);
            if (statusText === "abort") {
                _abort.call(that);
            }
            else if(xhr.status === 200 || xhr.status === 204){
                console.debug("Upload section completed", xhr.status, xhr.responseText, xhr.getResponseHeader("x-completed"), xhr.getAllResponseHeaders());
                let completed = xhr.getResponseHeader("x-completed")!=null && JSON.parse(xhr.getResponseHeader("x-completed").toLowerCase());
                if (completed) {
                    // server has completed the upload, so the client can stop uploading and no need to report success.
                    _successWithoutReport.call(that);
                }else{
                    let offset = xhr.getResponseHeader("x-offset");
                    that._triedCount = 0; // reset tried count
                    doUpload.call(that, file, offset, sectionSize);
                } 
            }else {
                _retry.call(that, xhr.responseText);
            }
        }
    });
}

var resumeUpload = function (isManual) {
    let that = this;
    that._abort = false;

    let file = that._file;
    let initOptions = typeof this.uploadId !=="undefined" && this.uploadId 
        ? { fileExistsBehavior: FileExistsBehavior.Resume }: {};
    initUpload.call(that, file, initOptions)
        .then((result) => {
            that._initializedUpload = result;
            if (isManual) {
                // update start upload time and total transfer size if manual resume.
                _startTrackingProgress.call(that, result.PackageSize - result.Offset);
            }

            console.log("File resume initialized", that._initializedUpload);
            if (result) {
                doUpload.call(that, file, result.Offset, result.SectionSize);
            }
        }, (response) => { _retry.call(that, "Failed to init resume: " + response.responseText); });
}

var _successWithoutReport = function () {
    let that = this;
    _stopTrackingProgress.call(that);
    if (typeof that.onsuccess === "function") {
        let overallProgress = _calcOverallProgress.call(that);
        that.onsuccess(that._initializedUpload, overallProgress);
    }
}

var _success = function (uploadId) {
    _stopTrackingProgress.call(this);

    return $.ajax({
        type: "PUT",
        url: `${this.endpoint}/complete`,
        crossDomain: true,
        data: {
            uploadId: uploadId
        }
    });
}

var _error = function (error) {
    let that = this;
    _stopTrackingProgress.call(that);

    console.error("Upload error", error);
    if(typeof that.uploadId !== "undefined" && that.uploadId){
        try {
            $.ajax({
                type: "PUT",
                url: `${this.endpoint}/failure`,
                crossDomain: true,
                data: {
                    uploadId: that.uploadId
                }
            });
        } catch (e) {
            console.warn("Failed to report error to server");
        }
    }
    if (typeof that.onerror === "function") {
        that.onerror(that._initializedUpload, error);
    }
}

var _retry = function (error) {
    let that = this;
    that._triedCount = that._triedCount || 0; // retry to upload for 3 times
    if (that._triedCount < 3) {
        that._triedCount++;
        console.debug("Retry after 500ms", error);

        setTimeout(function () {
            resumeUpload.call(that);
        }, 500);
    } else {
        _error.call(that, error);
    }
}

var _abort = function () {
    let that = this;
    that._abort = true;

    _stopTrackingProgress.call(that);

    if (typeof that.onabort === "function") {
        try { that.onabort(that._initializedUpload); } catch (e) { console.error("Failed to process onabort event"); }
    }
}

var _startTrackingProgress = function (totalTransferSize) {
    let that = this;
    that._startUploadTime = new Date();
    that._totalTransferSize = totalTransferSize;
    _stopTrackingProgress.call(that);
    console.debug("Start tracking progress");
    that._progressTimer = setInterval(function () {
        try{
            let progress = _calcProgress.call(that);
            if (typeof that.onprogress === "function") {
                try { that.onprogress(progress); } catch (e) { console.error("Failed to process onprogress event"); }
            }
        }catch(e){
            console.error("Failed to track progress", e);
        }
    }, 1000);
}

var _stopTrackingProgress = function () {
    let that = this;
    try{
        if (that._progressTimer) {
            console.debug("Stop tracking progress");
            clearInterval(that._progressTimer);
            that._progressTimer = null;
        }
    }catch(e){
        console.error("Failed to stop tracking progress", e);
    }
}

var _calcProgress = function () {
    let that = this;
    let _loaded = that._sectionLoaded || 0;
    let _total = that._file.size;
    let _sectionOffset = that._sectionOffset || 0;
    let _last = that._lastProgressCalc || new Date();

    let totalLoaded = _sectionOffset + _loaded;

    let _lastLoaded = that._lastTotalLoaded || totalLoaded;
    that._lastTotalLoaded = totalLoaded;

    let loadedDelta = totalLoaded - _lastLoaded;

    let _time = (new Date() - _last); //miliseconds
    that._lastProgressCalc = new Date();
    let _start = that._startUploadTime || new Date();

    let totalTime = new Date() - _start;

    if (_time > 0 && _total > 0) {
        let percent = Math.round((totalLoaded * 100.0 / _total) * 10) / 10;
        let speed = loadedDelta / _time; // bytes/miliseconds
        let remaining = speed > 0 ? Math.round((_total - (totalLoaded)) / speed) : -1;
        return new Progress(percent, speed * 1000, totalTime, remaining);
    }
    return new Progress(0, 0, totalTime, -1);
}

var _calcOverallProgress = function () {
    let that = this;
    let _start = that._startUploadTime || new Date();
    let _total = that._totalTransferSize || that._file.size;
    let totalTime = new Date() - _start;
    if (totalTime > 0) {
        let speed = _total / totalTime; // bytes/miliseconds
        return new Progress(100, speed * 1000, totalTime, 0);
    }
    return new Progress(0, 0, totalTime, -1);
}

export { JUpload, FileExistsBehavior, Progress };