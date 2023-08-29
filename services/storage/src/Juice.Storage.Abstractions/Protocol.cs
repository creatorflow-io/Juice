﻿namespace Juice.Storage.Abstractions
{
    public enum Protocol
    {
        Smb = 0,
        VirtualDirectory = 1,
        LocalDisk = 2,
        Ftp = 3,
        //S3 = 4,
        NotSupported = 9999
    }
}
