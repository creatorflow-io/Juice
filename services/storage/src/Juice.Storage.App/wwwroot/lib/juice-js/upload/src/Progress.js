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

export default Progress;