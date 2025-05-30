﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.ToolPackage;

internal class ToolPackageException : GracefulException
{
    public ToolPackageException()
    {
    }

    public ToolPackageException(string message) : base(message)
    {
    }

    public ToolPackageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
