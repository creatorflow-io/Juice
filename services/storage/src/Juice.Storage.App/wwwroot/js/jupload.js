
var FileExistsBehavior;
(function (FileExistsBehavior) {
    FileExistsBehavior[FileExistsBehavior["RaiseError"] = 0] = "RaiseError";
    FileExistsBehavior[FileExistsBehavior["Replace"] = 1] = "Replace";
    FileExistsBehavior[FileExistsBehavior["AscendedCopyNumber"] = 2] = "AscendedCopyNumber";
    FileExistsBehavior[FileExistsBehavior["Resume"] = 3] = "Resume";
})(FileExistsBehavior || (FileExistsBehavior = {}));

var Progress = (function () {
    function Progress(percent, bps, totalTime, remaining) {
        this.percent = percent;
        this.bps = bps;
        this.totalTime = totalTime;
        this.remaining = remaining;
    }

    Object.defineProperty(Progress.prototype, "message", {
        get: function () {
            return this.remaining > 0 ? `Uploaded ${this.percent}% ~ ${this.toHumanPackageSize(this.bps)}/s remaining ${this.remaining} miliseconds`
                : `Uploaded ${this.percent}% ~ ${this.toHumanPackageSize(this.bps)}/s`;
        },
        enumerable: true,
        configurable: true
    });

    Progress.prototype.toHumanPackageSize = function (bytes, si) {
        if (si === void 0) { si = true; }
        var thresh = si ? 1000 : 1024;
        if (Math.abs(bytes) < thresh) {
            return bytes + ' B';
        }
        var units = si
            ? ['kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB']
            : ['KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB'];
        var u = -1;
        do {
            bytes /= thresh;
            ++u;
        } while (Math.abs(bytes) >= thresh && u < units.length - 1);
        return bytes.toFixed(1) + ' ' + units[u];
    }

    return Progress;
}());

var factory = function ($) {
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

        options = options || {};

        let that = this;

        that._file = file;

        initUpload.call(that, file, options)
            .then((result) => {
                that._initializedUpload = JSON.parse(result);
                that._startUploadTime = new Date();
                that._totalTransferSize = result.PackageSize - result.Offset;

                console.log("File initialized", that._initializedUpload);
                return that._initializedUpload;
            }, (response) => { _retry.call(that, "Failed to init: " + response.responseText); })
            .then((result) => {
                if (result) {
                    if (typeof that.onupload === "function") {
                        try {
                            that.onupload(result);
                        } catch (e) {
                            console.error("Failed to process onupload event", e);
                        }
                    }
                    doUpload.call(that, file, result.Offset, result.SectionSize);
                }
            }, (response) => { _retry.call(that, "Failed to upload: " + response.responseText); })
            .catch(function (error) {
                console.debug("catch", error);
                _retry.call(that, error);
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

    var initUpload = function (file, options) {

        if (typeof this.endpoint === "undefined" || !this.endpoint) {
            throw "The endpoint must be configured.";
        }

        let filePath = options.filePath || file.name;
        let fileExistsBehavior = typeof options.fileExistsBehavior === "undefined" ?
            FileExistsBehavior.AscendedCopyNumber
            : options.fileExistsBehavior;

        return $.ajax({
            type: "POST",
            url: `${this.endpoint}/init`,
            crossDomain: true,
            data: {
                filePath: filePath,
                fileExistsBehavior: fileExistsBehavior,
                fileSize: file.size,
                uploadId: this.uploadId
            }
        });
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
                        try {
                            let progress = _calcProgress.call(that, evt);
                            that.onprogress(progress);
                        } catch (e) {
                            console.debug("Progress erorr", e);
                        }
                    };
                }
                return xhr;
            },
            beforeSend: function (xhr) {
                try {
                    xhr.setRequestHeader("X-UploadId", uploadId);
                    xhr.setRequestHeader("X-Offset", offset);
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
                } else if (xhr.status === 204) {
                    that._triedCount = 0; // reset tried count
                    let offset = parseInt(xhr.getResponseHeader("X-Offset"));
                    doUpload.call(that, file, offset, sectionSize);
                } else {
                    _retry.call(that, xhr.responseText);
                }
            }
        });
    }

    var resumeUpload = function (isManual) {
        let that = this;
        that._abort = false;

        let file = that._file;

        initUpload.call(that, file, { fileExistsBehavior: FileExistsBehavior.Resume })
            .then((result) => {
                that._initializedUpload = JSON.parse(result);
                if (isManual) {
                    // update start upload time and total transfer size if manual resume.
                    that._startUploadTime = new Date();
                    that._totalTransferSize = result.PackageSize - result.Offset;
                }
                console.log("File resume initialized", that._initializedUpload);
                return that._initializedUpload;
            }, (response) => { _retry.call(that, "Failed to init resume: " + response.responseText); })
            .then((result) => {
                if (result) {
                    doUpload.call(that, file, result.Offset, result.SectionSize);
                }
            }, (response) => { _retry.call(that, "Failed to upload: " + response.responseText); })
            .catch(function (error) {
                console.debug("catch", error);
                _retry.call(that, error);
            });
    }

    var _success = function (uploadId) {
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

        console.error("Upload error", error);
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

        if (typeof that.onabort === "function") {
            try { that.onabort(that._initializedUpload); } catch (e) { console.error("Failed to process onabort event"); }
        }
    }

    var _calcProgress = function (e) {
        let that = this;
        let _loaded = e.position || e.loaded;
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

    return JUpload;

};

(function (root, factory) {
    if (typeof define === 'function' && define.amd) {
        // AMD. Register as an anonymous module.
        define(['jquery'], function ($) {
            return (root.JUpload = factory($));
        });
    } else {
        // Browser globals
        root.JUpload = factory($);
    }
}(typeof self !== 'undefined' ? self : this, factory));

export default factory(window.$)

export { FileExistsBehavior }
